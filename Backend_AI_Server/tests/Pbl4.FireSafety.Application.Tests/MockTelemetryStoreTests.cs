using Pbl4.FireSafety.Domain.Models;
using Pbl4.FireSafety.Infrastructure.Persistence;
using Xunit;

namespace Pbl4.FireSafety.Application.Tests;

public class MockTelemetryStoreTests
{
    [Fact]
    public void Add_SingleItem_CanBeRetrievedAsLatest()
    {
        // Arrange
        var store = new MockTelemetryStore();
        var report = new SystemTelemetryReport
        {
            Status = "ONLINE",
            Sensors = new SensorMetrics { Temp = 28.0 }
        };

        // Act
        store.Add(report);

        // Assert
        Assert.Equal(1, store.Count);
        Assert.NotNull(store.GetLatest());
        Assert.Equal(28.0, store.GetLatest()!.Sensors.Temp);
    }

    [Fact]
    public void Add_MoreThan60Items_KeepsOnlyLatest60ItemsFIFO()
    {
        // Arrange
        var store = new MockTelemetryStore();

        // Act: Thêm 75 bản ghi với nhiệt độ tăng dần từ 1 đến 75
        for (var i = 1; i <= 75; i++)
        {
            store.Add(new SystemTelemetryReport
            {
                Sensors = new SensorMetrics { Temp = i }
            });
        }

        // Assert: Số lượng tối đa luôn là 60
        Assert.Equal(60, store.Count);

        var recent = store.GetRecent(60);
        Assert.Equal(60, recent.Count);

        // Bản ghi cũ nhất còn lại phải là bản ghi thứ 16 (1..15 đã bị xóa theo FIFO)
        Assert.Equal(16.0, recent.First().Sensors.Temp);

        // Bản ghi mới nhất phải là bản ghi 75
        Assert.Equal(75.0, recent.Last().Sensors.Temp);
        Assert.Equal(75.0, store.GetLatest()!.Sensors.Temp);
    }
}
