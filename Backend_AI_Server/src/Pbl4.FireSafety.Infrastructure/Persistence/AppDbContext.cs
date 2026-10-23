using Microsoft.EntityFrameworkCore;
using Pbl4.FireSafety.Domain.Entities;

namespace Pbl4.FireSafety.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext chính của hệ thống giám sát hỏa hoạn PBL4.
/// Quản lý việc ánh xạ các thực thể Domain xuống các bảng cơ sở dữ liệu MySQL.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Bảng lưu vết dữ liệu cảm biến đa phương thức dạng chuỗi thời gian (Time-Series).
    /// </summary>
    public DbSet<TelemetryLog> TelemetryLogs => Set<TelemetryLog>();

    /// <summary>
    /// Bảng lưu lịch sử các sự kiện cảnh báo cháy nổ (Class >= HighRisk).
    /// </summary>
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();

    /// <summary>
    /// Bảng lưu trạng thái heartbeat và kết nối hiện tại của từng thiết bị Edge ESP32.
    /// </summary>
    public DbSet<DeviceStatus> DeviceStatuses => Set<DeviceStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =========================================================================
        // 1. Cấu hình bảng TelemetryLogs (Dữ liệu chuỗi thời gian - Time-Series)
        // =========================================================================
        modelBuilder.Entity<TelemetryLog>(entity =>
        {
            entity.ToTable("TelemetryLogs");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.DeviceId)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(e => e.Smoke)
                  .HasPrecision(8, 4);

            entity.Property(e => e.Gas)
                  .HasPrecision(8, 2);

            entity.Property(e => e.Co)
                  .HasPrecision(8, 2);

            entity.Property(e => e.Temperature)
                  .HasPrecision(5, 2);

            entity.Property(e => e.RiskClass)
                  .IsRequired()
                  .HasConversion<int>();

            entity.Property(e => e.TimestampUtc)
                  .IsRequired()
                  .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // Tối ưu Index cho truy vấn Time-Series: Lọc theo thiết bị và sắp xếp/lấy khoảng thời gian
            entity.HasIndex(e => new { e.DeviceId, e.TimestampUtc })
                  .HasDatabaseName("IX_TelemetryLogs_DeviceId_TimestampUtc");

            // Index phụ theo TimestampUtc để truy vấn dọn dẹp dữ liệu cũ (cleanup jobs)
            entity.HasIndex(e => e.TimestampUtc)
                  .HasDatabaseName("IX_TelemetryLogs_TimestampUtc");
        });

        // =========================================================================
        // 2. Cấu hình bảng AlertEvents (Sự kiện cảnh báo hỏa hoạn)
        // =========================================================================
        modelBuilder.Entity<AlertEvent>(entity =>
        {
            entity.ToTable("AlertEvents");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.DeviceId)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(e => e.TriggerClass)
                  .IsRequired()
                  .HasConversion<int>();

            entity.Property(e => e.SensorConfidence)
                  .HasPrecision(5, 4);

            entity.Property(e => e.VisionConfidence)
                  .HasPrecision(5, 4)
                  .IsRequired(false);

            entity.Property(e => e.FcmSent)
                  .IsRequired()
                  .HasDefaultValue(false);

            entity.Property(e => e.Message)
                  .HasMaxLength(255)
                  .IsRequired();

            entity.Property(e => e.TimestampUtc)
                  .IsRequired()
                  .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // Index phục vụ truy vấn danh sách sự kiện gần nhất cho ứng dụng Mobile
            entity.HasIndex(e => new { e.DeviceId, e.TimestampUtc })
                  .HasDatabaseName("IX_AlertEvents_DeviceId_TimestampUtc");

            entity.HasIndex(e => e.TriggerClass)
                  .HasDatabaseName("IX_AlertEvents_TriggerClass");
        });

        // =========================================================================
        // 3. Cấu hình bảng DeviceStatuses (Trạng thái kết nối thiết bị)
        // =========================================================================
        modelBuilder.Entity<DeviceStatus>(entity =>
        {
            entity.ToTable("DeviceStatuses");

            // DeviceId là khóa chính (Primary Key) vì mỗi thiết bị là duy nhất
            entity.HasKey(e => e.DeviceId);
            entity.Property(e => e.DeviceId)
                  .HasMaxLength(50);

            entity.Property(e => e.IsOnline)
                  .IsRequired()
                  .HasDefaultValue(false);

            entity.Property(e => e.LastRiskClass)
                  .IsRequired()
                  .HasConversion<int>();

            entity.Property(e => e.LastSeenUtc)
                  .IsRequired();

            entity.Property(e => e.IpAddress)
                  .HasMaxLength(45) // Đủ chứa cả IPv4 và IPv6
                  .IsRequired(false);

            entity.Property(e => e.FirmwareVersion)
                  .HasMaxLength(30)
                  .HasDefaultValue("1.0.0");
        });
    }
}
