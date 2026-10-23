using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pbl4.FireSafety.Application.Options;
using Pbl4.FireSafety.Domain.Enums;
using Pbl4.FireSafety.Infrastructure.AI;
using Xunit;

namespace Pbl4.FireSafety.Application.Tests;

public class RandomForestServiceTests
{
    private readonly RandomForestOnnxService _service;

    public RandomForestServiceTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AiModelOptions
        {
            RandomForestOnnxPath = "models/non_existent_rf.onnx",
            EnableHotReload = false
        });

        _service = new RandomForestOnnxService(
            NullLogger<RandomForestOnnxService>.Instance,
            options);

        // Kích hoạt StartAsync để khởi tạo service
        _service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void Predict_WhenModelFileNotPresent_UsesFallbackGracefully()
    {
        // Act
        var result = _service.Predict(smoke: 0.05f, gas: 50f, co: 10f, temp: 25f);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Fallback-RuleBased", result.ModelSource);
        Assert.Equal(FireRiskClass.Safe, result.RiskClass);
        Assert.True(result.Confidence >= 0.8f);
    }

    [Theory]
    [InlineData(0.05f, 50f, 10f, 25f, FireRiskClass.Safe)]
    [InlineData(0.25f, 120f, 45f, 33f, FireRiskClass.LowRisk)]
    [InlineData(0.45f, 220f, 75f, 40f, FireRiskClass.MediumRisk)]
    [InlineData(0.65f, 380f, 130f, 48f, FireRiskClass.HighRisk)]
    [InlineData(0.95f, 650f, 250f, 65f, FireRiskClass.FireConfirmed)]
    public void Predict_DifferentEnvironmentalLevels_ReturnsExpectedRiskClasses(
        float smoke,
        float gas,
        float co,
        float temp,
        FireRiskClass expectedRiskClass)
    {
        // Act
        var result = _service.Predict(smoke, gas, co, temp);

        // Assert
        Assert.Equal(expectedRiskClass, result.RiskClass);
    }

    [Fact]
    public void Predict_ConcurrentCalls_AreThreadSafe()
    {
        // Act & Assert (chạy 50 tác vụ đồng thời)
        Parallel.For(0, 50, _ =>
        {
            var result = _service.Predict(smoke: 0.7f, gas: 400f, co: 150f, temp: 50f);
            Assert.NotNull(result);
            Assert.Equal(FireRiskClass.HighRisk, result.RiskClass);
        });
    }
}
