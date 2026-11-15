using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using Pbl4.FireSafety.Application.Contracts.Services;
using Pbl4.FireSafety.Application.DTOs;
using Pbl4.FireSafety.Application.Options;
using Pbl4.FireSafety.Domain.Entities;
using Pbl4.FireSafety.Domain.Enums;
using Pbl4.FireSafety.Domain.Models;
using Pbl4.FireSafety.Infrastructure.Persistence;

namespace Pbl4.FireSafety.Infrastructure.Mqtt;

/// <summary>
/// BackgroundService chạy nền 24/7, duy trì kết nối liên tục với MQTT Broker,
/// lắng nghe luồng dữ liệu cảm biến và frame ảnh camera từ các mạch ESP32, kiểm định tính hợp lệ,
/// thực thi suy luận AI đa phương thức và kết hợp qua Fusion Decision Matrix.
/// </summary>
public class MqttIngestionService : BackgroundService
{
    private readonly ILogger<MqttIngestionService> _logger;
    private readonly MqttOptions _mqttOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelemetryValidator _validator;
    private readonly IRandomForestService _rfService;
    private readonly IMockTelemetryStore _telemetryStore;
    private readonly IYoloService _yoloService;
    private readonly IFusionDecisionService _fusionService;
    private IMqttClient? _mqttClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public MqttIngestionService(
        ILogger<MqttIngestionService> logger,
        IOptions<MqttOptions> mqttOptions,
        IServiceScopeFactory scopeFactory,
        ITelemetryValidator validator,
        IRandomForestService rfService,
        IMockTelemetryStore telemetryStore,
        IYoloService yoloService,
        IFusionDecisionService fusionService)
    {
        _logger = logger;
        _mqttOptions = mqttOptions.Value;
        _scopeFactory = scopeFactory;
        _validator = validator;
        _rfService = rfService;
        _telemetryStore = telemetryStore;
        _yoloService = yoloService;
        _fusionService = fusionService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MqttIngestionService đang khởi động...");

        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        // 1. Cấu hình các sự kiện vòng đời kết nối
        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        // 2. Vòng lặp duy trì kết nối (Auto-Reconnect với Backoff)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    await ConnectWithRetryAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong vòng lặp giám sát MQTT Client");
            }

            // Chờ 5 giây trước khi kiểm tra lại trạng thái kết nối
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        // 3. Dọn dẹp ngắt kết nối an toàn khi dừng server
        if (_mqttClient.IsConnected)
        {
            var disconnectOptions = factory.CreateClientDisconnectOptionsBuilder()
                .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                .Build();

            await _mqttClient.DisconnectAsync(disconnectOptions, CancellationToken.None);
            _logger.LogInformation("Đã ngắt kết nối an toàn khỏi MQTT Broker");
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        var clientOptions = BuildMqttClientOptions();
        var retryDelay = TimeSpan.FromSeconds(2);
        const int maxRetrySeconds = 30;

        while (!_mqttClient!.IsConnected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation(
                    "Đang thử kết nối tới MQTT Broker tại {Host}:{Port}...",
                    _mqttOptions.Host,
                    _mqttOptions.Port);

                var result = await _mqttClient.ConnectAsync(clientOptions, cancellationToken);

                if (result.ResultCode == MqttClientConnectResultCode.Success)
                {
                    _logger.LogInformation("Kết nối MQTT Broker thành công!");
                    return;
                }

                _logger.LogWarning("Kết nối MQTT Broker không thành công: {Reason}", result.ResultCode);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    "Không thể kết nối tới MQTT Broker: {Message}. Thử lại sau {Delay} giây...",
                    ex.Message,
                    retryDelay.TotalSeconds);
            }

            await Task.Delay(retryDelay, cancellationToken);
            // Tăng dần thời gian chờ thử lại (Exponential backoff) tối đa 30s
            retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, maxRetrySeconds));
        }
    }

    private MqttClientOptions BuildMqttClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqttOptions.Host, _mqttOptions.Port)
            .WithClientId(string.IsNullOrWhiteSpace(_mqttOptions.ClientId)
                ? $"pbl4-backend-{Guid.NewGuid():N}"
                : _mqttOptions.ClientId)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(_mqttOptions.KeepAliveSeconds))
            .WithCleanSession();

        if (!string.IsNullOrEmpty(_mqttOptions.Username))
        {
            builder.WithCredentials(_mqttOptions.Username, _mqttOptions.Password);
        }

        if (_mqttOptions.UseTls)
        {
            builder.WithTlsOptions(o => o.UseTls());
        }

        return builder.Build();
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        _logger.LogInformation("Đã thiết lập kết nối tới MQTT Broker. Bắt đầu đăng ký các Topic...");

        var factory = new MqttFactory();

        // 1. Topic telemetry (cảm biến)
        var telemetryTopic = string.IsNullOrWhiteSpace(_mqttOptions.TelemetryTopic)
            ? "pbl4/telemetry"
            : _mqttOptions.TelemetryTopic;

        // 2. Topic camera frame (ảnh)
        var cameraTopic = string.IsNullOrWhiteSpace(_mqttOptions.CameraFrameTopic)
            ? "pbl4/camera/frame"
            : _mqttOptions.CameraFrameTopic;

        var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(telemetryTopic).WithAtLeastOnceQoS())
            .WithTopicFilter(f => f.WithTopic("pbl4/+/telemetry").WithAtLeastOnceQoS())
            .WithTopicFilter(f => f.WithTopic(cameraTopic).WithAtLeastOnceQoS())
            .WithTopicFilter(f => f.WithTopic("pbl4/+/camera/frame").WithAtLeastOnceQoS())
            .Build();

        await _mqttClient!.SubscribeAsync(subscribeOptions);

        _logger.LogInformation(
            "Đã đăng ký thành công các Topic: [{TelemetryTopic}], [pbl4/+/telemetry], [{CameraTopic}], [pbl4/+/camera/frame]",
            telemetryTopic,
            cameraTopic);
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning(
            "Mất kết nối với MQTT Broker. Lý do: {Reason}. Client sẽ tự động kết nối lại...",
            args.Reason);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;
        var payloadBytes = args.ApplicationMessage.PayloadSegment.Array != null
            ? args.ApplicationMessage.PayloadSegment.ToArray()
            : Array.Empty<byte>();

        // 1. Kiểm tra nếu gói tin thuộc kênh Camera (Binary JPEG)
        if (topic.EndsWith("camera/frame", StringComparison.OrdinalIgnoreCase))
        {
            ProcessCameraFrame(topic, payloadBytes);
            return;
        }

        // 2. Xử lý gói tin Telemetry (JSON cảm biến)
        var jsonString = Encoding.UTF8.GetString(payloadBytes);
        _logger.LogDebug("Nhận gói tin từ Topic [{Topic}]: {Payload}", topic, jsonString);

        try
        {
            var payloadDto = JsonSerializer.Deserialize<TelemetryPayloadDto>(jsonString, JsonOptions);

            // Nếu thiết bị gửi vào topic pbl4/{deviceId}/telemetry mà trong JSON chưa có DeviceId
            if (payloadDto != null && string.IsNullOrWhiteSpace(payloadDto.DeviceId))
            {
                var topicParts = topic.Split('/');
                if (topicParts.Length == 3 && topicParts[0] == "pbl4" && topicParts[2] == "telemetry")
                {
                    payloadDto = payloadDto with { DeviceId = topicParts[1] };
                }
                else
                {
                    payloadDto = payloadDto with { DeviceId = "esp32-node-01" };
                }
            }

            // Kiểm tra tính hợp lệ dữ liệu (Contract Validation)
            if (!_validator.Validate(payloadDto, out var validationError))
            {
                _logger.LogWarning(
                    "Gói tin từ [{Topic}] không hợp lệ và bị từ chối: {Error}. Nội dung: {Json}",
                    topic,
                    validationError,
                    jsonString);
                return;
            }

            await ProcessTelemetryAsync(payloadDto!);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(
                jsonEx,
                "Không thể parse JSON từ Topic [{Topic}]. Nội dung gói tin không đúng định dạng: {RawPayload}",
                topic,
                jsonString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi bất ngờ xảy ra khi xử lý gói tin từ Topic [{Topic}]", topic);
        }
    }

    private void ProcessCameraFrame(string topic, byte[] imageBytes)
    {
        var deviceId = "esp32-cam-01";
        var topicParts = topic.Split('/');
        if (topicParts.Length == 4 && topicParts[0] == "pbl4" && topicParts[2] == "camera")
        {
            deviceId = topicParts[1];
        }

        var detection = _yoloService.Detect(imageBytes);

        _logger.LogInformation(
            "Frame camera từ [{DeviceId}] ({Bytes}B) => YOLOv8: Lửa={HasFire} ({FireConf:P0}), Khói={HasSmoke} ({SmokeConf:P0}), Hộp={BoxCount} ({Time}ms, {Source})",
            deviceId,
            imageBytes.Length,
            detection.HasFire,
            detection.MaxFireConfidence,
            detection.HasSmoke,
            detection.MaxSmokeConfidence,
            detection.Detections.Count,
            detection.InferenceTimeMs,
            detection.ModelSource);

        // Lấy dữ liệu cảm biến gần nhất để chạy Fusion đa phương thức
        var latest = _telemetryStore.GetLatest();
        var sensorClass = latest != null ? (FireRiskClass)latest.AiResult.PredictedClass : FireRiskClass.Safe;
        float sensorConf = 0.90f;

        var fusionResult = _fusionService.Evaluate(
            sensorClass,
            sensorConf,
            detection.MaxFireConfidence,
            detection.MaxSmokeConfidence);

        // Cập nhật kết quả AI vision và Fusion vào bản ghi Telemetry mới nhất trên RAM
        if (latest != null)
        {
            var updated = new SystemTelemetryReport
            {
                Timestamp = DateTime.UtcNow,
                Status = latest.Status,
                SensitivityMode = latest.SensitivityMode,
                Sensors = latest.Sensors,
                AiResult = new AiInferenceResult
                {
                    PredictedClass = (int)fusionResult.FinalClass,
                    ClassLabel = fusionResult.ClassLabel,
                    YoloFireConfidence = detection.MaxFireConfidence,
                    YoloSmokeConfidence = detection.MaxSmokeConfidence
                }
            };
            _telemetryStore.Add(updated);
        }

        // Kích hoạt ghi nhận AlertEvent nếu kết quả Fusion từ Camera nâng mức nguy hiểm
        if (fusionResult.IsAlertTriggered)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var alertEvent = new AlertEvent
                {
                    DeviceId = deviceId,
                    TriggerClass = fusionResult.FinalClass,
                    SensorConfidence = sensorConf,
                    VisionConfidence = detection.MaxFireConfidence,
                    FcmSent = false,
                    Message = fusionResult.Explanation,
                    TimestampUtc = DateTime.UtcNow
                };
                dbContext.AlertEvents.Add(alertEvent);
                dbContext.SaveChanges();

                _logger.LogWarning(
                    "🚨 [CẢNH BÁO TỪ THỊ GIÁC CAMERA] [{DeviceId}] => {Class} ({Conf:P0}). Lý do: {Reason}",
                    deviceId,
                    fusionResult.FinalClass,
                    fusionResult.FinalConfidence,
                    fusionResult.Explanation);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi lưu AlertEvent từ frame camera cho [{DeviceId}].", deviceId);
            }
        }
    }

    private async Task ProcessTelemetryAsync(TelemetryPayloadDto payload)
    {
        var utcNow = DateTime.UtcNow;

        // 1. Thực thi suy luận AI Random Forest (ONNX) từ vector cảm biến [smoke, gas, co, temp]
        var sensorAiResult = _rfService.Predict(payload.Smoke, payload.Gas, payload.Co, payload.Temp);

        // Lấy lại confidences thị giác từ frame gần nhất (nếu có)
        var latestReport = _telemetryStore.GetLatest();
        double yoloFireConf = latestReport?.AiResult.YoloFireConfidence ?? 0.0;
        double yoloSmokeConf = latestReport?.AiResult.YoloSmokeConfidence ?? 0.0;

        // 2. Chạy Ma trận quyết định đa phương thức (Fusion Decision Matrix)
        var fusionResult = _fusionService.Evaluate(
            sensorAiResult.RiskClass,
            sensorAiResult.Confidence,
            (float)yoloFireConf,
            (float)yoloSmokeConf);

        var finalRiskClass = fusionResult.FinalClass;

        // 3. Lưu vào In-Memory Store để Mobile App đọc realtime qua GET /api/telemetry/latest và recent
        _telemetryStore.Add(new SystemTelemetryReport
        {
            Timestamp = utcNow,
            Status = "ONLINE",
            SensitivityMode = "STANDARD",
            AiResult = new AiInferenceResult
            {
                PredictedClass = (int)finalRiskClass,
                ClassLabel = fusionResult.ClassLabel,
                YoloFireConfidence = yoloFireConf,
                YoloSmokeConfidence = yoloSmokeConf
            },
            Sensors = new SensorMetrics
            {
                Temp = payload.Temp,
                Humidity = 0.0,
                GasMq2 = payload.Gas,
                SmokeMq135 = payload.Smoke,
                CoMq7 = payload.Co
            }
        });

        // 4. Lưu bền vững vào Database SQLite
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var telemetryEntity = new TelemetryLog
            {
                DeviceId = payload.DeviceId,
                Smoke = payload.Smoke,
                Gas = payload.Gas,
                Co = payload.Co,
                Temperature = payload.Temp,
                RiskClass = finalRiskClass,
                TimestampUtc = utcNow
            };

            dbContext.TelemetryLogs.Add(telemetryEntity);

            var deviceStatus = await dbContext.DeviceStatuses
                .FirstOrDefaultAsync(d => d.DeviceId == payload.DeviceId);

            if (deviceStatus == null)
            {
                deviceStatus = new DeviceStatus
                {
                    DeviceId = payload.DeviceId,
                    IsOnline = true,
                    LastRiskClass = finalRiskClass,
                    LastSeenUtc = utcNow,
                    FirmwareVersion = "1.0.0"
                };
                dbContext.DeviceStatuses.Add(deviceStatus);
            }
            else
            {
                deviceStatus.IsOnline = true;
                deviceStatus.LastSeenUtc = utcNow;
                deviceStatus.LastRiskClass = finalRiskClass;
            }

            // Nếu mức độ nguy hiểm vượt ngưỡng (Class >= HighRisk), tự động tạo bản ghi AlertEvent
            if (fusionResult.IsAlertTriggered)
            {
                var alertEvent = new AlertEvent
                {
                    DeviceId = payload.DeviceId,
                    TriggerClass = finalRiskClass,
                    SensorConfidence = sensorAiResult.Confidence,
                    VisionConfidence = (float)yoloFireConf,
                    FcmSent = false,
                    Message = fusionResult.Explanation,
                    TimestampUtc = utcNow
                };
                dbContext.AlertEvents.Add(alertEvent);

                _logger.LogWarning(
                    "🚨 [CẢNH BÁO HỎA HOẠN KÍCH HOẠT] [{DeviceId}] => Mức độ: {Class} ({Conf:P0}). Lý do: {Reason}",
                    payload.DeviceId,
                    finalRiskClass,
                    fusionResult.FinalConfidence,
                    fusionResult.Explanation);
            }

            await dbContext.SaveChangesAsync();
        }
        catch (Exception dbEx)
        {
            _logger.LogWarning(dbEx, "Lỗi khi lưu Telemetry/Alert xuống Database cho [{DeviceId}]. Dữ liệu vẫn được giữ trên RAM.", payload.DeviceId);
        }

        _logger.LogInformation(
            "Đã ghi nhận Telemetry từ [{DeviceId}] | Cảm biến: {SensorClass} ({SensorConf:P0}) | Thị giác: Lửa {FireConf:P0} | => FUSION: {FinalClass} ({FinalConf:P0})",
            payload.DeviceId,
            sensorAiResult.RiskClass,
            sensorAiResult.Confidence,
            yoloFireConf,
            finalRiskClass,
            fusionResult.FinalConfidence);
    }

    public override void Dispose()
    {
        _mqttClient?.Dispose();
        base.Dispose();
    }
}
