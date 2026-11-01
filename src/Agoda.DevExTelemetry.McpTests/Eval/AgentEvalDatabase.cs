using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Agoda.DevExTelemetry.McpTests.Eval;

/// <summary>
/// Writes the planted-problems dataset to a SQLite file for a live agent eval (see tools/agent-eval/run.sh).
/// Explicit: it only runs when selected, and only with AGENT_EVAL_DB set to the file to create.
/// </summary>
[TestFixture]
[Explicit("Writes the agent-eval database; run by tools/agent-eval/run.sh")]
public class AgentEvalDatabase
{
    [Test]
    public async Task WriteDatabase_SeedsPlantedProblemsRelativeToNow()
    {
        var path = Environment.GetEnvironmentVariable("AGENT_EVAL_DB");
        if (string.IsNullOrWhiteSpace(path))
            Assert.Ignore("Set AGENT_EVAL_DB to the SQLite file to create");

        if (File.Exists(path)) File.Delete(path);
        var options = new DbContextOptionsBuilder<TelemetryDbContext>().UseSqlite($"Data Source={path}").Options;
        await using var db = new TelemetryDbContext(options);
        await db.Database.MigrateAsync();

        foreach (var entity in PlantedProblems.Build(DateTime.UtcNow))
        {
            switch (entity)
            {
                case TestRun run: db.TestRuns.Add(run); break;
                case BuildMetric metric: db.BuildMetrics.Add(metric); break;
            }
        }
        await db.SaveChangesAsync();
    }
}
