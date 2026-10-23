using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Pbl4.FireSafety.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AlertEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DeviceId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    TriggerClass = table.Column<int>(type: "int", nullable: false),
                    SensorConfidence = table.Column<float>(type: "float", precision: 5, scale: 4, nullable: false),
                    VisionConfidence = table.Column<float>(type: "float", precision: 5, scale: 4, nullable: true),
                    FcmSent = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    Message = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertEvents", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DeviceStatuses",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    IsOnline = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    LastRiskClass = table.Column<int>(type: "int", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IpAddress = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "1.0.0")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceStatuses", x => x.DeviceId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TelemetryLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DeviceId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Smoke = table.Column<float>(type: "float", precision: 8, scale: 4, nullable: false),
                    Gas = table.Column<float>(type: "float", precision: 8, scale: 2, nullable: false),
                    Co = table.Column<float>(type: "float", precision: 8, scale: 2, nullable: false),
                    Temperature = table.Column<float>(type: "float", precision: 5, scale: 2, nullable: false),
                    RiskClass = table.Column<int>(type: "int", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryLogs", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_DeviceId_TimestampUtc",
                table: "AlertEvents",
                columns: new[] { "DeviceId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_TriggerClass",
                table: "AlertEvents",
                column: "TriggerClass");

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryLogs_DeviceId_TimestampUtc",
                table: "TelemetryLogs",
                columns: new[] { "DeviceId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryLogs_TimestampUtc",
                table: "TelemetryLogs",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertEvents");

            migrationBuilder.DropTable(
                name: "DeviceStatuses");

            migrationBuilder.DropTable(
                name: "TelemetryLogs");
        }
    }
}
