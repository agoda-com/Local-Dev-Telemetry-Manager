namespace Agoda.DevExTelemetry.DbPerfTests;

public record PerfSeedOptions(
    int BuildMetrics = 20000,
    int TestRuns = 10000,
    int TestCasesPerRun = 8,
    int RawPayloads = 10000,
    int ProjectCardinality = 120,
    int RepositoryCardinality = 80,
    int BranchCardinality = 240,
    int PlatformCardinality = 6,
    int RandomSeed = 424242);
