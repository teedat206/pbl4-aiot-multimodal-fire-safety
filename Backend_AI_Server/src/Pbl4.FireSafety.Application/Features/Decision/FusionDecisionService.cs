using Pbl4.FireSafety.Application.Contracts.Services;
using Pbl4.FireSafety.Domain.Enums;

namespace Pbl4.FireSafety.Application.Features.Decision;

/// <summary>
/// Thực thi Ma trận quyết định đa phương thức (Multi-Modal Fusion Decision Matrix).
/// Kết hợp kết quả từ 2 mô hình AI:
/// 1. Random Forest (Dữ liệu dạng bảng từ cảm biến MQ-2, MQ-135, MQ-7, DHT20).
/// 2. YOLOv8 (Thị giác máy tính từ frame ảnh camera).
/// Giúp loại bỏ báo động giả (False Alarms) và đảm bảo không bỏ sót nguy cơ hỏa hoạn thực sự.
/// </summary>
public class FusionDecisionService : IFusionDecisionService
{
    public (int FinalClass, float FinalConfidence) Decide(
        int sensorClass,
        float sensorConfidence,
        float? visionConfidence)
    {
        var result = Evaluate(
            (FireRiskClass)sensorClass,
            sensorConfidence,
            visionConfidence,
            visionSmokeConfidence: null);

        return ((int)result.FinalClass, result.FinalConfidence);
    }

    public FusionResult Evaluate(
        FireRiskClass sensorClass,
        float sensorConfidence,
        float? visionFireConfidence,
        float? visionSmokeConfidence)
    {
        float fireConf = visionFireConfidence ?? 0f;
        float smokeConf = visionSmokeConfidence ?? 0f;
        bool hasVision = visionFireConfidence.HasValue || visionSmokeConfidence.HasValue;

        // ─── TRƯỜNG HỢP 1: Camera Offline (Không có frame ảnh) ─────────────────
        if (!hasVision)
        {
            return new FusionResult
            {
                FinalClass = sensorClass,
                FinalConfidence = sensorConfidence,
                ClassLabel = FormatClassLabel(sensorClass),
                Explanation = "Đánh giá đơn phương thức từ cụm cảm biến môi trường (Camera chưa có dữ liệu)."
            };
        }

        // ─── TRƯỜNG HỢP 2: Lưỡng chứng trùng hợp (Cả cảm biến lẫn camera đều thấy nguy cơ cao) ──
        if (sensorClass >= FireRiskClass.HighRisk && fireConf >= 0.45f)
        {
            float combinedConf = Math.Min(0.99f, Math.Max(sensorConfidence, fireConf) * 1.1f);
            return new FusionResult
            {
                FinalClass = FireRiskClass.FireConfirmed,
                FinalConfidence = combinedConf,
                ClassLabel = "FIRE CONFIRMED",
                Explanation = $"Xác nhận hỏa hoạn nguy cấp: Cả cụm cảm biến ({sensorClass}) và camera AI (Lửa: {fireConf:P0}) đều xác nhận độc lập."
            };
        }

        // ─── TRƯỜNG HỢP 3: Cảm biến báo động trung bình + Camera phát hiện ngọn lửa ──
        if (sensorClass == FireRiskClass.MediumRisk && fireConf >= 0.45f)
        {
            float combinedConf = Math.Min(0.95f, (sensorConfidence * 0.4f) + (fireConf * 0.6f) + 0.1f);
            return new FusionResult
            {
                FinalClass = FireRiskClass.HighRisk,
                FinalConfidence = combinedConf,
                ClassLabel = "HIGH RISK",
                Explanation = $"Nguy cơ cháy cao: Camera phát hiện ngọn lửa ({fireConf:P0}) và cảm biến môi trường đang tăng mức cảnh báo."
            };
        }

        // ─── TRƯỜNG HỢP 4: Cảm biến báo động + Camera phát hiện khói đậm đặc ───
        if (sensorClass >= FireRiskClass.MediumRisk && smokeConf >= 0.50f)
        {
            var elevatedClass = sensorClass == FireRiskClass.MediumRisk ? FireRiskClass.HighRisk : sensorClass;
            float combinedConf = Math.Min(0.95f, Math.Max(sensorConfidence, smokeConf));
            return new FusionResult
            {
                FinalClass = elevatedClass,
                FinalConfidence = combinedConf,
                ClassLabel = FormatClassLabel(elevatedClass),
                Explanation = $"Nguy cơ khói độc: Camera phát hiện khói ({smokeConf:P0}) đồng bộ với nồng độ khí đo được từ cảm biến ({sensorClass})."
            };
        }

        // ─── TRƯỜNG HỢP 5: Chỉ có Camera thấy lửa (Cảm biến đang ở mức An toàn / Thấp) ───
        if (sensorClass <= FireRiskClass.LowRisk && fireConf >= 0.45f)
        {
            if (fireConf >= 0.80f)
            {
                // Ngọn lửa bùng phát rất rõ rệt nhưng khói chưa kịp khuếch tán đến cảm biến
                return new FusionResult
                {
                    FinalClass = FireRiskClass.HighRisk,
                    FinalConfidence = fireConf * 0.9f,
                    ClassLabel = "HIGH RISK",
                    Explanation = $"Cảnh báo sớm: Camera phát hiện ngọn lửa rõ nét ({fireConf:P0}), khói/nhiệt chưa kịp lan tới vị trí đặt cảm biến."
                };
            }

            if (fireConf >= 0.60f)
            {
                return new FusionResult
                {
                    FinalClass = FireRiskClass.MediumRisk,
                    FinalConfidence = fireConf,
                    ClassLabel = "MEDIUM RISK",
                    Explanation = $"Nghi ngờ có ngọn lửa từ hình ảnh ({fireConf:P0}). Cảm biến môi trường hiện chưa ghi nhận bất thường."
                };
            }

            // Dưới 0.60f: Khả năng cao là báo giả do phản quang/ánh sáng đèn
            return new FusionResult
            {
                FinalClass = FireRiskClass.LowRisk,
                FinalConfidence = fireConf,
                ClassLabel = "LOW RISK",
                Explanation = $"Ghi nhận vệt sáng khả nghi ({fireConf:P0}), cảm biến an toàn. Phân loại mức rủi ro thấp để tránh báo động giả."
            };
        }

        // ─── TRƯỜNG HỢP 6: Cảm biến báo nguy cơ cao nhưng Camera không thấy lửa ───
        // (VD: Rò rỉ khí gas sinh hoạt MQ-2, khí độc CO MQ-7, hoặc cháy âm ỉ sinh nhiều khói)
        if (sensorClass >= FireRiskClass.HighRisk && fireConf < 0.45f)
        {
            return new FusionResult
            {
                FinalClass = sensorClass,
                FinalConfidence = sensorConfidence,
                ClassLabel = FormatClassLabel(sensorClass),
                Explanation = $"Cảnh báo từ cảm biến ({sensorClass}): Nồng độ khí gas/khói/nhiệt độ vượt ngưỡng an toàn nghiêm trọng. Không quan sát thấy ngọn lửa trực diện."
            };
        }

        // ─── TRƯỜNG HỢP 7: Cả 2 đều ở ngưỡng bình thường hoặc rủi ro nhẹ ───────
        return new FusionResult
        {
            FinalClass = sensorClass,
            FinalConfidence = sensorConfidence,
            ClassLabel = FormatClassLabel(sensorClass),
            Explanation = "Môi trường ổn định và an toàn theo cả dữ liệu cảm biến lẫn phân tích thị giác."
        };
    }

    private static string FormatClassLabel(FireRiskClass riskClass) => riskClass switch
    {
        FireRiskClass.Safe => "SAFE",
        FireRiskClass.LowRisk => "LOW RISK",
        FireRiskClass.MediumRisk => "MEDIUM RISK",
        FireRiskClass.HighRisk => "HIGH RISK",
        FireRiskClass.FireConfirmed => "FIRE CONFIRMED",
        _ => riskClass.ToString().ToUpperInvariant()
    };
}
