using System.Threading.Channels;

using Console.Api.Data;
using Console.Api.Domain;

using Microsoft.EntityFrameworkCore;

namespace Console.Api.Features.Provisioning;

public sealed class ProvisioningQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid jobId) => _channel.Writer.TryWrite(jobId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed class ProvisioningWorker(IServiceScopeFactory scopes, ProvisioningQueue queue, TimeProvider clock, ILogger<ProvisioningWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RecoverAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not recover provisioning jobs on startup");
        }

        await foreach (var jobId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ProvisioningEngine>().RunAsync(jobId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Provisioning job {JobId} crashed", jobId);
            }
        }
    }

    // Jobs interrupted by a restart are failed (re-run them from the UI); jobs still queued are picked up again.
    private async Task RecoverAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConsoleDbContext>();
        var stale = await db.Jobs.Where(j => j.Status == JobStatus.Running).ToListAsync(ct);
        foreach (var job in stale)
        {
            job.Status = JobStatus.Failed;
            job.FinishedAt = clock.GetUtcNow();
            var steps = job.GetSteps().Select(s => s.Status == StepStatus.Running ? s with { Status = StepStatus.Failed, Message = "Interrupted by a server restart. Run provisioning again; finished steps are skipped." } : s);
            job.SetSteps(steps);
            var clinic = await db.Clinics.FirstAsync(c => c.Id == job.ClinicId, ct);
            if (clinic.Status == ClinicStatus.Provisioning)
            {
                clinic.Status = ClinicStatus.Failed;
            }
        }

        await db.SaveChangesAsync(ct);
        foreach (var id in await db.Jobs.Where(j => j.Status == JobStatus.Queued).OrderBy(j => j.CreatedAt).Select(j => j.Id).ToListAsync(ct))
        {
            queue.Enqueue(id);
        }
    }
}
