using Pbl4.FireSafety.Application.Features.Decision;
using Pbl4.FireSafety.Domain.Enums;

namespace Pbl4.FireSafety.Application.Tests;

public class FusionDecisionServiceTests
{
    private readonly FusionDecisionService _service = new();

    [Fact]
    public void Evaluate_CameraOffline_FallsBackToSensorClass()
    {
        // Arrange
        var sensorClass = FireRiskClass.Safe;
        float sensorConf = 0.92f;

        // Act
        var result = _service.Evaluate(sensorClass, sensorConf, null, null);

        // Assert
        Assert.Equal(FireRiskClass.Safe, result.FinalClass);
        Assert.Equal(sensorConf, result.FinalConfidence);
        Assert.False(result.IsAlertTriggered);
    }

    [Fact]
    public void Evaluate_DualConfirmation_SensorHighAndVisionFire_ReturnsFireConfirmed()
    {
        // Arrange: Cả cảm biến lẫn camera đều phát hiện nguy cơ cao
        var sensorClass = FireRiskClass.HighRisk;
        float sensorConf = 0.88f;
        float visionFire = 0.85f;

        // Act
        var result = _service.Evaluate(sensorClass, sensorConf, visionFire, null);

        // Assert
        Assert.Equal(FireRiskClass.FireConfirmed, result.FinalClass);
        Assert.True(result.FinalConfidence >= 0.88f);
        Assert.True(result.IsAlertTriggered);
        Assert.Equal("FIRE CONFIRMED", result.ClassLabel);
    }

    [Fact]
    public void Evaluate_SensorMedium_VisionFire_ElevatesToHighRisk()
    {
        // Arrange: Cảm biến cảnh báo trung bình (Class 2) + Camera thấy ngọn lửa
        var sensorClass = FireRiskClass.MediumRisk;
        float sensorConf = 0.75f;
        float visionFire = 0.70f;

        // Act
        var result = _service.Evaluate(sensorClass, sensorConf, visionFire, null);

        // Assert
        Assert.Equal(FireRiskClass.HighRisk, result.FinalClass);
        Assert.True(result.IsAlertTriggered);
        Assert.Equal("HIGH RISK", result.ClassLabel);
    }

    [Fact]
    public void Evaluate_SensorMedium_VisionSmoke_ElevatesToHighRisk()
    {
        // Arrange: Cảm biến cảnh báo trung bình + Camera thấy khói đậm
        var sensorClass = FireRiskClass.MediumRisk;
        float sensorConf = 0.70f;
        float visionSmoke = 0.65f;

        // Act
        var result = _service.Evaluate(sensorClass, sensorConf, visionFireConfidence: null, visionSmoke);

        // Assert
        Assert.Equal(FireRiskClass.HighRisk, result.FinalClass);
        Assert.True(result.IsAlertTriggered);
    }

    [Fact]
    public void Evaluate_VisualOnly_VeryHighFireConfidence_ElevatesToHighRisk()
    {
        // Arrange: Cảm biến chưa có khói (Class 0) nhưng Camera thấy ngọn lửa rất rõ (> 0.80)
        var sensorClass = FireRiskClass.Safe;
        float sensorConf = 0.95f;
        float visionFire = 0.85f;

        // Act
        var result = _service.Evaluate(sensorClass, sensorConf, visionFire, null);

        // Assert: Cảnh báo sớm mức HighRisk để cứu nguy kịp thời
        Assert.Equal(FireRiskClass.HighRisk, result.FinalClass);
        Assert.True(result.IsAlertTriggered);
    }

    [Fact]
    public void Evaluate_VisualOnly_LowFireConfidence_SuppressesToLowRisk_PreventsFalseAlarm()
    {
        // Arrange: Camera thấy vệt sáng mờ nghi là lửa (< 0.60) nhưng cảm biến an toàn
        var sensorClass = FireRiskClass.Safe;
        float sensorConf = 0.95f;
        float visionFire = 0.50f;

        // Act
        var result = _service.Evaluate(sensorClass, sensorConf, visionFire, null);

        // Assert: Không báo động cháy giả mạo, hạ về LowRisk
        Assert.Equal(FireRiskClass.LowRisk, result.FinalClass);
        Assert.False(result.IsAlertTriggered);
    }

    [Fact]
    public void Evaluate_SensorHigh_VisionSafe_MaintainsSensorHighRisk()
    {
        // Arrange: Rò rỉ khí gas/khói nồng độ cao (Class 3) nhưng chưa có ngọn lửa nhìn thấy
        var sensorClass = FireRiskClass.HighRisk;
        float sensorConf = 0.90f;
        float visionFire = 0.05f;

        // Act
        var result = _service.Evaluate(sensorClass, sensorConf, visionFire, null);

        // Assert: Tôn trọng chỉ số cảm biến, không bỏ sót cháy âm ỉ hoặc rò gas
        Assert.Equal(FireRiskClass.HighRisk, result.FinalClass);
        Assert.True(result.IsAlertTriggered);
    }

    [Fact]
    public void Decide_TupleOverload_ReturnsMatchingResults()
    {
        // Arrange
        int sensorClass = (int)FireRiskClass.HighRisk;
        float sensorConf = 0.90f;
        float visionConf = 0.80f;

        // Act
        var (finalClass, finalConf) = _service.Decide(sensorClass, sensorConf, visionConf);

        // Assert
        Assert.Equal((int)FireRiskClass.FireConfirmed, finalClass);
        Assert.True(finalConf >= 0.90f);
    }
}
