using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Data;

public class PostgresTelemetryRepository : EfTelemetryRepository
{
    public PostgresTelemetryRepository(TelemetryDbContext db) : base(db) { }

    public override async Task RunMaintenanceAsync()
    {
        await Db.Database.ExecuteSqlRawAsync("VACUUM ANALYZE");
    }
}
