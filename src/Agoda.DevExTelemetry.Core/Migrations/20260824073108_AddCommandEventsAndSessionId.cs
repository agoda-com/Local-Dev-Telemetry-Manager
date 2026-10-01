using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agoda.DevExTelemetry.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandEventsAndSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "BuildMetrics",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommandEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", nullable: true),
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
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    Command = table.Column<string>(type: "TEXT", nullable: false),
                    ExitCode = table.Column<int>(type: "INTEGER", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    Signal = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorCount = table.Column<int>(type: "INTEGER", nullable: true),
                    TimeTakenMs = table.Column<double>(type: "REAL", nullable: false),
                    PackageManager = table.Column<string>(type: "TEXT", nullable: true),
                    PackageManagerVersion = table.Column<string>(type: "TEXT", nullable: true),
                    ColdInstall = table.Column<bool>(type: "INTEGER", nullable: true),
                    LockfileChanged = table.Column<bool>(type: "INTEGER", nullable: true),
                    MeasurementSource = table.Column<string>(type: "TEXT", nullable: true),
                    Prebundled = table.Column<bool>(type: "INTEGER", nullable: true),
                    DomContentLoadedMs = table.Column<double>(type: "REAL", nullable: true),
                    FirstContentfulPaintMs = table.Column<double>(type: "REAL", nullable: true),
                    SpooledAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CommitSha = table.Column<string>(type: "TEXT", nullable: true),
                    SourceEndpoint = table.Column<string>(type: "TEXT", nullable: false),
                    ExtraData = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommandEventNpmTimers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandEventId = table.Column<string>(type: "TEXT", nullable: false),
                    TimerName = table.Column<string>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandEventNpmTimers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandEventNpmTimers_CommandEvents_CommandEventId",
                        column: x => x.CommandEventId,
                        principalTable: "CommandEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildMetrics_SessionId",
                table: "BuildMetrics",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandEventNpmTimers_CommandEventId",
                table: "CommandEventNpmTimers",
                column: "CommandEventId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandEventNpmTimers_TimerName",
                table: "CommandEventNpmTimers",
                column: "TimerName");

            migrationBuilder.CreateIndex(
                name: "IX_CommandEvents_MeasurementSource",
                table: "CommandEvents",
                column: "MeasurementSource");

            migrationBuilder.CreateIndex(
                name: "IX_CommandEvents_Phase",
                table: "CommandEvents",
                column: "Phase");

            migrationBuilder.CreateIndex(
                name: "IX_CommandEvents_ProjectName",
                table: "CommandEvents",
                column: "ProjectName");

            migrationBuilder.CreateIndex(
                name: "IX_CommandEvents_ReceivedAt",
                table: "CommandEvents",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommandEvents_SessionId",
                table: "CommandEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandEvents_SourceEndpoint",
                table: "CommandEvents",
                column: "SourceEndpoint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandEventNpmTimers");

            migrationBuilder.DropTable(
                name: "CommandEvents");

            migrationBuilder.DropIndex(
                name: "IX_BuildMetrics_SessionId",
                table: "BuildMetrics");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "BuildMetrics");
        }
    }
}
