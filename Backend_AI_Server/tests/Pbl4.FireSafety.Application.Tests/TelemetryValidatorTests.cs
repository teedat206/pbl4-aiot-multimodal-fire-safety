using Pbl4.FireSafety.Application.DTOs;
using Pbl4.FireSafety.Application.Features.Telemetry;
using Xunit;

namespace Pbl4.FireSafety.Application.Tests;

public class TelemetryValidatorTests
{
    private readonly TelemetryValidator _validator = new();

    [Fact]
    public void Validate_ValidPayload_ReturnsTrue()
    {
        // Arrange
        var payload = new TelemetryPayloadDto
        {
            DeviceId = "esp32-cam-01",
            Smoke = 0.45f,
            Gas = 250.0f,
            Co = 80.0f,
            Temp = 28.5f
        };

        // Act
        var isValid = _validator.Validate(payload, out var error);

        // Assert
        Assert.True(isValid);
        Assert.Empty(error);
    }

    [Fact]
    public void Validate_NullPayload_ReturnsFalse()
    {
        // Act
        var isValid = _validator.Validate(null, out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("rỗng", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyDeviceId_ReturnsFalse(string deviceId)
    {
        // Arrange
        var payload = new TelemetryPayloadDto
        {
            DeviceId = deviceId,
            Smoke = 0.1f,
            Gas = 100f,
            Co = 20f,
            Temp = 25f
        };

        // Act
        var isValid = _validator.Validate(payload, out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("DeviceId", error);
    }

    [Fact]
    public void Validate_SmokeIsNaN_ReturnsFalse()
    {
        // Arrange
        var payload = new TelemetryPayloadDto
        {
            DeviceId = "esp32-01",
            Smoke = float.NaN,
            Gas = 100f,
            Co = 20f,
            Temp = 25f
        };

        // Act
        var isValid = _validator.Validate(payload, out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("NaN", error);
    }

    [Theory]
    [InlineData(-50.0f)]
    [InlineData(200.0f)]
    public void Validate_TemperatureOutOfPhysicalRange_ReturnsFalse(float temp)
    {
        // Arrange
        var payload = new TelemetryPayloadDto
        {
            DeviceId = "esp32-01",
            Smoke = 0.1f,
            Gas = 100f,
            Co = 20f,
            Temp = temp
        };

        // Act
        var isValid = _validator.Validate(payload, out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("Nhiệt độ", error);
    }
}
