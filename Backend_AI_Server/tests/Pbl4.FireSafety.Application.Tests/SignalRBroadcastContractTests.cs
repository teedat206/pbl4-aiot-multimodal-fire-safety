using Pbl4.FireSafety.Application.Contracts.Services;
using Pbl4.FireSafety.Application.DTOs;
using Pbl4.FireSafety.Domain.Enums;
using Xunit;

namespace Pbl4.FireSafety.Application.Tests;

public class SignalRBroadcastContractTests
{
    private sealed class FakeFireSafetyHubService : IFireSafetyHubService
    {
        public TelemetryBroadcastDto? LastTelemetry { get; private set; }
        public AlertBroadcastDto? LastAlert { get; private set; }
        public int TelemetryCount { get; private set; }
        public int AlertCount { get; private set; }

        public Task BroadcastTelemetryAsync(TelemetryBroadcastDto payload, CancellationToken ct = default)
        {
            LastTelemetry = payload;
            TelemetryCount++;
            return Task.CompletedTask;
        }

        public Task BroadcastAlertAsync(AlertBroadcastDto alert, CancellationToken ct = default)
        {
            LastAlert = alert;
            AlertCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task BroadcastTelemetryAsync_ShouldDeliverValidPayload()
    {
        // Arrange
        var fakeHub = new FakeFireSafetyHubService();
        var dto = new TelemetryBroadcastDto
        {
            DeviceId = "esp32-node-01",
            Smoke = 0.15f,
            Gas = 45f,
            Co = 12f,
            Temp = 28.5f,
            Class = (int)FireRiskClass.Safe,
            ClassLabel = "SAFE",
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        await fakeHub.BroadcastTelemetryAsync(dto);

        // Assert
        Assert.Equal(1, fakeHub.TelemetryCount);
        Assert.NotNull(fakeHub.LastTelemetry);
        Assert.Equal("esp32-node-01", fakeHub.LastTelemetry.DeviceId);
        Assert.Equal(0, fakeHub.LastTelemetry.Class);
        Assert.Equal("SAFE", fakeHub.LastTelemetry.ClassLabel);
        Assert.Equal(28.5f, fakeHub.LastTelemetry.Temp);
    }

    [Fact]
    public async Task BroadcastAlertAsync_ShouldDeliverHighPriorityAlertPayload()
    {
        // Arrange
        var fakeHub = new FakeFireSafetyHubService();
        var alertDto = new AlertBroadcastDto
        {
            AlertId = 101,
            DeviceId = "esp32-cam-01",
            TriggerClass = FireRiskClass.FireConfirmed,
            SensorConfidence = 0.95f,
            VisionConfidence = 0.98f,
            Message = "Phát hiện lửa và khói đồng thời từ camera và cảm biến",
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        await fakeHub.BroadcastAlertAsync(alertDto);

        // Assert
        Assert.Equal(1, fakeHub.AlertCount);
        Assert.NotNull(fakeHub.LastAlert);
        Assert.Equal(101, fakeHub.LastAlert.AlertId);
        Assert.Equal("esp32-cam-01", fakeHub.LastAlert.DeviceId);
        Assert.Equal(FireRiskClass.FireConfirmed, fakeHub.LastAlert.TriggerClass);
        Assert.Equal(0.98f, fakeHub.LastAlert.VisionConfidence);
    }

    [Theory]
    [InlineData(FireRiskClass.Safe, false)]
    [InlineData(FireRiskClass.LowRisk, false)]
    [InlineData(FireRiskClass.MediumRisk, false)]
    [InlineData(FireRiskClass.HighRisk, true)]
    [InlineData(FireRiskClass.FireConfirmed, true)]
    public void FusionResult_IsAlertTriggered_ShouldMatchThreshold(FireRiskClass riskClass, bool expectedAlert)
    {
        // Arrange
        var fusion = new FusionResult
        {
            FinalClass = riskClass,
            FinalConfidence = 0.9f,
            ClassLabel = riskClass.ToString(),
            Explanation = "Test"
        };

        // Assert
        Assert.Equal(expectedAlert, fusion.IsAlertTriggered);
    }
}
