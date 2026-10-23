namespace Pbl4.FireSafety.Domain.Enums;

/// <summary>
/// Mức độ nguy hiểm cháy nổ được Random Forest phân loại.
/// Class 0 = An toàn tuyệt đối → Class 4 = Hỏa hoạn xác nhận.
/// </summary>
public enum FireRiskClass
{
    Safe = 0,
    LowRisk = 1,
    MediumRisk = 2,
    HighRisk = 3,
    FireConfirmed = 4
}
