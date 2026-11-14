using System.Text.Json;

using Console.Api.Data;
using Console.Api.Domain;

namespace Console.Api.Features.Audit;

// Stages an audit row on the same DbContext so it commits atomically with the write it describes.
public sealed class AuditLog(ConsoleDbContext db, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Add(string actor, string action, Guid? clinicId, object? details = null) =>
        db.Audit.Add(new AuditEntry
        {
            Id = Guid.NewGuid(),
            At = clock.GetUtcNow(),
            Actor = actor,
            Action = action,
            ClinicId = clinicId,
            DetailsJson = JsonSerializer.Serialize(details ?? new { }, Json),
        });
}
