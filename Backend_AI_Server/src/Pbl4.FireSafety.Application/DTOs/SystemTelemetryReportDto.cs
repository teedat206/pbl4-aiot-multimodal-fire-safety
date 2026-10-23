using Pbl4.FireSafety.Domain.Models;

namespace Pbl4.FireSafety.Application.DTOs;

/// <summary>
/// DTO đóng gói toàn bộ gói tin Telemetry theo Data Contract chuẩn khi giao tiếp với Mobile App và REST API.
/// </summary>
public record SystemTelemetryReportDto : SystemTelemetryReport;
