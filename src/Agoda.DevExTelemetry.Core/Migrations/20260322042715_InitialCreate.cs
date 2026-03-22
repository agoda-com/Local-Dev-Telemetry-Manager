using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agoda.DevExTelemetry.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BuildMetrics",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", nullable: false),
                    CpuCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Hostname = table.Column<string>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    Os = table.Column<string>(type: "TEXT", nullable: false),
                    Branch = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", nullable: false),
                    Repository = table.Column<string>(type: "TEXT", nullable: false),
                    RepositoryName = table.Column<string>(type: "TEXT", nullable: false),
                    TimeTakenMs = table.Column<double>(type: "REAL", nullable: false),
                    MetricType = table.Column<string>(type: "TEXT", nullable: false),
                    BuildCategory = table.Column<string>(type: "TEXT", nullable: false),
                    ReloadType = table.Column<string>(type: "TEXT", nullable: true),
                    ToolVersion = table.Column<string>(type: "TEXT", nullable: true),
                    CommitSha = table.Column<string>(type: "TEXT", nullable: true),
                    IsDebuggerAttached = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExecutionEnvironment = table.Column<string>(type: "TEXT", nullable: false),
                    SourceEndpoint = table.Column<string>(type: "TEXT", nullable: false),
                    ExtraData = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RawPayloads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawPayloads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestRuns",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    RunId = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", nullable: false),
                    CpuCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Hostname = table.Column<string>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    Os = table.Column<string>(type: "TEXT", nullable: false),
                    Branch = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", nullable: false),
                    Repository = table.Column<string>(type: "TEXT", nullable: false),
                    RepositoryName = table.Column<string>(type: "TEXT", nullable: false),
                    TestRunner = table.Column<string>(type: "TEXT", nullable: false),
                    IsDebuggerAttached = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExecutionEnvironment = table.Column<string>(type: "TEXT", nullable: false),
                    TotalTests = table.Column<int>(type: "INTEGER", nullable: true),
                    PassedTests = table.Column<int>(type: "INTEGER", nullable: true),
                    FailedTests = table.Column<int>(type: "INTEGER", nullable: true),
                    SkippedTests = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalDurationMs = table.Column<double>(type: "REAL", nullable: true),
                    SourceEndpoint = table.Column<string>(type: "TEXT", nullable: false),
                    ExtraData = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TestRunId = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", nullable: true),
                    ClassName = table.Column<string>(type: "TEXT", nullable: true),
                    MethodName = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<double>(type: "REAL", nullable: true),
                    StartTime = table.Column<string>(type: "TEXT", nullable: true),
                    EndTime = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCases_TestRuns_TestRunId",
                        column: x => x.TestRunId,
                        principalTable: "TestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildMetrics_BuildCategory",
                table: "BuildMetrics",
                column: "BuildCategory");

            migrationBuilder.CreateIndex(
                name: "IX_BuildMetrics_ExecutionEnvironment",
                table: "BuildMetrics",
                column: "ExecutionEnvironment");

            migrationBuilder.CreateIndex(
                name: "IX_BuildMetrics_MetricType",
                table: "BuildMetrics",
                column: "MetricType");

            migrationBuilder.CreateIndex(
                name: "IX_BuildMetrics_ProjectName",
                table: "BuildMetrics",
                column: "ProjectName");

            migrationBuilder.CreateIndex(
                name: "IX_BuildMetrics_ReceivedAt",
                table: "BuildMetrics",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_ClassName",
                table: "TestCases",
                column: "ClassName");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_Status",
                table: "TestCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_TestRunId",
                table: "TestCases",
                column: "TestRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_ExecutionEnvironment",
                table: "TestRuns",
                column: "ExecutionEnvironment");

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_ProjectName",
                table: "TestRuns",
                column: "ProjectName");

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_ReceivedAt",
                table: "TestRuns",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_TestRunner",
                table: "TestRuns",
                column: "TestRunner");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildMetrics");

            migrationBuilder.DropTable(
                name: "RawPayloads");

            migrationBuilder.DropTable(
                name: "TestCases");

            migrationBuilder.DropTable(
                name: "TestRuns");
        }
    }
}
