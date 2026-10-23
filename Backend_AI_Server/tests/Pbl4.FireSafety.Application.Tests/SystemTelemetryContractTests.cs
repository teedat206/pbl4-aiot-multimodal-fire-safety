using System.Text.Json;
using Pbl4.FireSafety.Domain.Models;
using Xunit;

namespace Pbl4.FireSafety.Application.Tests;

public class SystemTelemetryContractTests
{
    [Fact]
    public void SystemTelemetryReport_Serialization_MatchesExpectedContractJson()
    {
        // Arrange: Khởi tạo đối tượng với đúng giá trị mẫu
        var report = new SystemTelemetryReport
        {
            Timestamp = DateTime.Parse("2026-10-15T21:30:00Z").ToUniversalTime(),
            Status = "ONLINE",
            SensitivityMode = "STANDARD",
            AiResult = new AiInferenceResult
            {
                PredictedClass = 0,
                ClassLabel = "SAFE",
                YoloFireConfidence = 0.0,
                YoloSmokeConfidence = 1.8
            },
            Sensors = new SensorMetrics
            {
                Temp = 28.5,
                Humidity = 68.0,
                GasMq2 = 120.5,
                SmokeMq135 = 45.0,
                CoMq7 = 12.4
            }
        };

        // Act: Serialize sang JSON
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });

        // Assert: Parse lại và kiểm tra từng trường dữ liệu
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("ONLINE", root.GetProperty("status").GetString());
        Assert.Equal("STANDARD", root.GetProperty("sensitivity_mode").GetString());

        var ai = root.GetProperty("ai_result");
        Assert.Equal(0, ai.GetProperty("predicted_class").GetInt32());
        Assert.Equal("SAFE", ai.GetProperty("class_label").GetString());
        Assert.Equal(0.0, ai.GetProperty("yolo_fire_conf").GetDouble());
        Assert.Equal(1.8, ai.GetProperty("yolo_smoke_conf").GetDouble());

        var sensors = root.GetProperty("sensors");
        Assert.Equal(28.5, sensors.GetProperty("temp").GetDouble());
        Assert.Equal(68.0, sensors.GetProperty("humidity").GetDouble());
        Assert.Equal(120.5, sensors.GetProperty("gas_mq2").GetDouble());
        Assert.Equal(45.0, sensors.GetProperty("smoke_mq135").GetDouble());
        Assert.Equal(12.4, sensors.GetProperty("co_mq7").GetDouble());
    }

    [Fact]
    public void SystemTelemetryReport_Deserialization_ParsesSampleJsonCorrectly()
    {
        // Arrange: Chuỗi JSON mẫu từ đề bài
        var sampleJson = """
        {
          "timestamp": "2026-10-15T21:30:00Z",
          "status": "ONLINE",
          "sensitivity_mode": "STANDARD",
          "ai_result": {
            "predicted_class": 0,
            "class_label": "SAFE",
            "yolo_fire_conf": 0.0,
            "yolo_smoke_conf": 1.8
          },
          "sensors": {
            "temp": 28.5,
            "humidity": 68.0,
            "gas_mq2": 120.5,
            "smoke_mq135": 45.0,
            "co_mq7": 12.4
          }
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<SystemTelemetryReport>(sampleJson);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ONLINE", result.Status);
        Assert.Equal("STANDARD", result.SensitivityMode);
        Assert.Equal(0, result.AiResult.PredictedClass);
        Assert.Equal("SAFE", result.AiResult.ClassLabel);
        Assert.Equal(28.5, result.Sensors.Temp);
        Assert.Equal(68.0, result.Sensors.Humidity);
        Assert.Equal(120.5, result.Sensors.GasMq2);
        Assert.Equal(45.0, result.Sensors.SmokeMq135);
        Assert.Equal(12.4, result.Sensors.CoMq7);
    }
}
