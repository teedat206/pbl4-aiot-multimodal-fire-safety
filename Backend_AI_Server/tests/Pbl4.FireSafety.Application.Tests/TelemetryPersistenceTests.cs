using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pbl4.FireSafety.Domain.Entities;
using Pbl4.FireSafety.Domain.Enums;
using Pbl4.FireSafety.Infrastructure.Persistence;
using Xunit;

namespace Pbl4.FireSafety.Application.Tests;

public class TelemetryPersistenceTests
{
    [Fact]
    public async Task TelemetryLogs_ShouldPersistAllSixSensorComponentsInSqlite()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.EnsureSqliteSchemaUpToDate();

        var utcNow = DateTime.UtcNow;
        var entity = new TelemetryLog
        {
            DeviceId = "esp32-s3-001",
            Smoke = 12.5f,
            Gas = 140.25f,
            Co = 18.75f,
            Temperature = 29.4f,
            Humidity = 68.5f,
            Aqi = 42.1f,
            RiskClass = FireRiskClass.Safe,
            TimestampUtc = utcNow
        };

        // Act
        db.TelemetryLogs.Add(entity);
        await db.SaveChangesAsync();

        var saved = await db.TelemetryLogs.AsNoTracking().SingleAsync();

        // Assert
        Assert.Equal("esp32-s3-001", saved.DeviceId);
        Assert.Equal(12.5f, saved.Smoke);
        Assert.Equal(140.25f, saved.Gas);
        Assert.Equal(18.75f, saved.Co);
        Assert.Equal(29.4f, saved.Temperature);
        Assert.Equal(68.5f, saved.Humidity);
        Assert.Equal(42.1f, saved.Aqi);
        Assert.Equal(FireRiskClass.Safe, saved.RiskClass);
    }

    [Fact]
    public async Task EnsureSqliteSchemaUpToDate_ShouldUpgradeLegacyFourColumnTableWithoutDataLoss()
    {
        // Arrange: tạo bảng TelemetryLogs kiểu cũ chỉ có 4 thông số cảm biến
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = """
                CREATE TABLE "TelemetryLogs" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TelemetryLogs" PRIMARY KEY AUTOINCREMENT,
                    "DeviceId" TEXT NOT NULL,
                    "Smoke" REAL NOT NULL,
                    "Gas" REAL NOT NULL,
                    "Co" REAL NOT NULL,
                    "Temperature" REAL NOT NULL,
                    "RiskClass" INTEGER NOT NULL,
                    "TimestampUtc" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                INSERT INTO "TelemetryLogs" ("DeviceId", "Smoke", "Gas", "Co", "Temperature", "RiskClass")
                VALUES ('esp32-old', 1.0, 20.0, 5.0, 27.5, 0);
                """;
            await createCmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);

        // Act: nâng cấp schema 2 lần liên tiếp để kiểm tra tính idempotent
        db.EnsureSqliteSchemaUpToDate();
        db.EnsureSqliteSchemaUpToDate();

        db.TelemetryLogs.Add(new TelemetryLog
        {
            DeviceId = "esp32-s3-001",
            Smoke = 2.0f,
            Gas = 50.0f,
            Co = 10.0f,
            Temperature = 30.0f,
            Humidity = 70.0f,
            Aqi = 15.5f,
            RiskClass = FireRiskClass.Safe,
            TimestampUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Assert
        var rows = await db.TelemetryLogs.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal("esp32-old", rows[0].DeviceId);
        Assert.Equal(0f, rows[0].Humidity);
        Assert.Equal(0f, rows[0].Aqi);
        Assert.Equal("esp32-s3-001", rows[1].DeviceId);
        Assert.Equal(70.0f, rows[1].Humidity);
        Assert.Equal(15.5f, rows[1].Aqi);
    }
}
