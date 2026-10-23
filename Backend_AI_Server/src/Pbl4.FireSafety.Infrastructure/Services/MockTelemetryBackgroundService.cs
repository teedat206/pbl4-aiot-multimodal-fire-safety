using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pbl4.FireSafety.Application.Contracts.Services;
using Pbl4.FireSafety.Domain.Models;

namespace Pbl4.FireSafety.Infrastructure.Services;

/// <summary>
/// BackgroundService chạy ngầm định kỳ mỗi 2 giây, tự sinh ngẫu nhiên số liệu cảm biến trong ngưỡng an toàn,
/// lưu trữ vào bộ nhớ đệm RAM (tối đa 60 bản ghi) và in trực tiếp lên màn hình Terminal.
/// </summary>
public class MockTelemetryBackgroundService : BackgroundService
{
    private readonly ILogger<MockTelemetryBackgroundService> _logger;
    private readonly IMockTelemetryStore _store;
    private readonly Random _random = new();

    public MockTelemetryBackgroundService(
        ILogger<MockTelemetryBackgroundService> logger,
        IMockTelemetryStore store)
    {
        _logger = logger;
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 MockTelemetryBackgroundService đã khởi động. Chu kỳ: 2 giây/lần.");

        // Sinh ngay 1 bản ghi đầu tiên khi khởi động
        GenerateAndPublish();

        // Sử dụng PeriodicTimer chuẩn của .NET (2 giây/lần)
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                GenerateAndPublish();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong vòng lặp MockTelemetryBackgroundService");
            }
        }

        _logger.LogInformation("MockTelemetryBackgroundService đã dừng.");
    }

    private void GenerateAndPublish()
    {
        // 1. Tự sinh dữ liệu ngẫu nhiên trong ngưỡng an toàn theo đề bài
        var temp = RandomDouble(27.5, 29.5, 1);
        var humidity = RandomDouble(65.0, 70.0, 1);
        var gas = RandomDouble(110.0, 135.0, 1);
        var smoke = RandomDouble(40.0, 50.0, 1);
        var co = RandomDouble(10.0, 15.0, 1);

        var now = DateTime.UtcNow;

        // 2. Đóng gói vào đối tượng SystemTelemetryReport theo đúng Data Contract
        var report = new SystemTelemetryReport
        {
            Timestamp = now,
            Status = "ONLINE",
            SensitivityMode = "STANDARD",
            AiResult = new AiInferenceResult
            {
                PredictedClass = 0,
                ClassLabel = "SAFE",
                YoloFireConfidence = 0.0,
                YoloSmokeConfidence = 0.0
            },
            Sensors = new SensorMetrics
            {
                Temp = temp,
                Humidity = humidity,
                GasMq2 = gas,
                SmokeMq135 = smoke,
                CoMq7 = co
            }
        };

        // 3. Lưu vào bộ đệm RAM (tối đa 60 bản ghi FIFO)
        _store.Add(report);

        // 4. In trực tiếp dòng dữ liệu vừa sinh ra lên màn hình Terminal
        // Định dạng chính xác: [HH:mm:ss] [MOCK SERVICE] Temp: 28.4C | Gas: 122ppm | AI: Class 0 (SAFE)
        var timeString = DateTime.Now.ToString("HH:mm:ss");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{timeString}] [MOCK SERVICE] Temp: {temp:F1}C | Gas: {gas:F0}ppm | AI: Class 0 (SAFE)");
        Console.ResetColor();

        // Đồng thời ghi qua Serilog
        _logger.LogInformation("[MOCK SERVICE] Temp: {Temp:F1}C | Gas: {Gas:F0}ppm | AI: Class 0 (SAFE)", temp, gas);
    }

    private double RandomDouble(double min, double max, int decimals)
    {
        var val = min + (_random.NextDouble() * (max - min));
        return Math.Round(val, decimals);
    }
}
