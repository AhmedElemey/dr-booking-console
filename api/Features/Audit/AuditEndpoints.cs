using System.Text.Json;

using Console.Api.Common;
using Console.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace Console.Api.Features.Audit;

public sealed record AuditDto(Guid Id, DateTimeOffset At, string Actor, string Action, Guid? ClinicId, JsonElement Details);

public static class AuditEndpoints
{
    public static void MapAudit(this RouteGroupBuilder api) =>
        api.MapGet("audit", async (ConsoleDbContext db, Guid? clinicId, int? page, int? pageSize, CancellationToken ct) =>
        {
            var (p, size) = Paging.Normalize(page, pageSize);
            var query = db.Audit.AsNoTracking().AsQueryable();
            if (clinicId is { } id)
            {
                query = query.Where(a => a.ClinicId == id);
            }

            var result = await query.OrderByDescending(a => a.At).ToPageAsync(p, size, ct);
            return new PagedList<AuditDto>(
                result.Items.Select(a => new AuditDto(a.Id, a.At, a.Actor, a.Action, a.ClinicId, JsonDocument.Parse(a.DetailsJson).RootElement.Clone())).ToList(),
                result.Total,
                result.Page,
                result.PageSize);
        }).WithTags("Audit");
}
