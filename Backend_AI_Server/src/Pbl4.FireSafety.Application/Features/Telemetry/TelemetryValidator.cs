using Pbl4.FireSafety.Application.Contracts.Services;
using Pbl4.FireSafety.Application.DTOs;

namespace Pbl4.FireSafety.Application.Features.Telemetry;

/// <summary>
/// Thực thi kiểm định tính toàn vẹn và phạm vi giá trị cảm biến theo Data Contract của đồ án PBL4.
/// Loại bỏ các giá trị dị thường do đứt dây cảm biến (out-of-range, NaN, Infinity).
/// </summary>
public class TelemetryValidator : ITelemetryValidator
{
    // Các ngưỡng vật lý thực tế của cảm biến (tránh nhiễu & giá trị rác)
    private const float MinTemperature = -20.0f;
    private const float MaxTemperature = 150.0f;

    private const float MinSmoke = 0.0f;
    private const float MaxSmoke = 5000.0f;

    private const float MinGasPpm = 0.0f;
    private const float MaxGasPpm = 100000.0f;

    private const float MinCoPpm = 0.0f;
    private const float MaxCoPpm = 10000.0f;

    private const int MaxDeviceIdLength = 50;

    public bool Validate(TelemetryPayloadDto? payload, out string errorMessage)
    {
        if (payload == null)
        {
            errorMessage = "Gói tin payload rỗng (null).";
            return false;
        }

        // 1. Kiểm tra DeviceId
        if (string.IsNullOrWhiteSpace(payload.DeviceId))
        {
            errorMessage = "Thiếu định danh thiết bị (DeviceId rỗng).";
            return false;
        }

        if (payload.DeviceId.Length > MaxDeviceIdLength)
        {
            errorMessage = $"DeviceId vượt quá độ dài cho phép ({MaxDeviceIdLength} ký tự).";
            return false;
        }

        // 2. Kiểm tra chỉ số Khói (Smoke)
        if (float.IsNaN(payload.Smoke) || float.IsInfinity(payload.Smoke))
        {
            errorMessage = "Chỉ số Smoke không phải là số hợp lệ (NaN hoặc Infinity).";
            return false;
        }

        if (payload.Smoke < MinSmoke || payload.Smoke > MaxSmoke)
        {
            errorMessage = $"Chỉ số Smoke ({payload.Smoke}) nằm ngoài phạm vi vật lý [{MinSmoke}, {MaxSmoke}].";
            return false;
        }

        // 3. Kiểm tra chỉ số Khí Gas (Gas LPG/CH4)
        if (float.IsNaN(payload.Gas) || float.IsInfinity(payload.Gas))
        {
            errorMessage = "Chỉ số Gas không phải là số hợp lệ (NaN hoặc Infinity).";
            return false;
        }

        if (payload.Gas < MinGasPpm || payload.Gas > MaxGasPpm)
        {
            errorMessage = $"Chỉ số Gas ({payload.Gas} ppm) nằm ngoài phạm vi vật lý [{MinGasPpm}, {MaxGasPpm}].";
            return false;
        }

        // 4. Kiểm tra chỉ số Khí độc CO
        if (float.IsNaN(payload.Co) || float.IsInfinity(payload.Co))
        {
            errorMessage = "Chỉ số CO không phải là số hợp lệ (NaN hoặc Infinity).";
            return false;
        }

        if (payload.Co < MinCoPpm || payload.Co > MaxCoPpm)
        {
            errorMessage = $"Chỉ số CO ({payload.Co} ppm) nằm ngoài phạm vi vật lý [{MinCoPpm}, {MaxCoPpm}].";
            return false;
        }

        // 5. Kiểm tra Nhiệt độ (Temperature)
        if (float.IsNaN(payload.Temp) || float.IsInfinity(payload.Temp))
        {
            errorMessage = "Chỉ số Temperature không phải là số hợp lệ (NaN hoặc Infinity).";
            return false;
        }

        if (payload.Temp < MinTemperature || payload.Temp > MaxTemperature)
        {
            errorMessage = $"Nhiệt độ ({payload.Temp}°C) nằm ngoài ngưỡng sinh nhiệt bình thường [{MinTemperature}, {MaxTemperature}]. Có thể do đứt dây cảm biến.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
