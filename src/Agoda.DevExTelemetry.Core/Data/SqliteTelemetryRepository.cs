using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Data;

public class SqliteTelemetryRepository : EfTelemetryRepository
{
    public SqliteTelemetryRepository(TelemetryDbContext db) : base(db) { }

    public override async Task RunMaintenanceAsync()
    {
        await Db.Database.ExecuteSqlRawAsync("PRAGMA optimize");
        await Db.Database.ExecuteSqlRawAsync("VACUUM");
    }
}
