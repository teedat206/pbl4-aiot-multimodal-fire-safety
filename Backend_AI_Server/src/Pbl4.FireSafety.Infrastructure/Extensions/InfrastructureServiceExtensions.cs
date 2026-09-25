using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pbl4.FireSafety.Application.Options;

namespace Pbl4.FireSafety.Infrastructure.Extensions;

/// <summary>
/// Extension method đăng ký tất cả Infrastructure services vào DI container.
/// Được gọi một lần duy nhất trong Program.cs (Composition Root).
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Options Pattern: bind strongly-typed config từ appsettings.json ---
        services.Configure<MqttOptions>(
            configuration.GetSection(MqttOptions.SectionName));

        services.Configure<AiModelOptions>(
            configuration.GetSection(AiModelOptions.SectionName));

        services.Configure<FirebaseOptions>(
            configuration.GetSection(FirebaseOptions.SectionName));

        services.Configure<SignalROptions>(
            configuration.GetSection(SignalROptions.SectionName));

        // --- Persistence (EF Core DbContext) — đăng ký ở BE-02 ---
        // services.AddDbContext<AppDbContext>(...);

        // --- MQTT Ingestion BackgroundService — đăng ký ở BE-03 ---
        // services.AddHostedService<MqttIngestionService>();

        // --- AI Services (ONNX) — đăng ký ở BE-04, BE-05 ---
        // services.AddSingleton<IRandomForestService, RandomForestOnnxService>();
        // services.AddSingleton<IYoloService, YoloOnnxService>();

        // --- Fusion Decision Service — đăng ký ở BE-06 ---
        // services.AddScoped<IFusionDecisionService, FusionDecisionService>();

        // --- Notification Services — đăng ký ở BE-07, BE-08 ---
        // services.AddScoped<IFireSafetyHubService, FireSafetyHubService>();
        // services.AddScoped<IFcmNotificationService, FcmNotificationService>();

        return services;
    }
}
