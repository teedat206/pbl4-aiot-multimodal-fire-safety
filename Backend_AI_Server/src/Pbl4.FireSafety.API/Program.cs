using Pbl4.FireSafety.Infrastructure.Extensions;
using Serilog;

// ─── Bootstrap Logger ───────────────────────────────────────────────────────
// Tạo logger tạm thời để bắt lỗi trong giai đoạn khởi tạo Host.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("=== Pbl4 Fire Safety Backend đang khởi động... ===");

    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog: thay thế hoàn toàn logging mặc định của .NET ──────────────
    // Đọc cấu hình từ section "Serilog" trong appsettings.json.
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Application", "Pbl4.FireSafety.API")
    );

    // ─── Controllers & API ───────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opt =>
    {
        opt.SwaggerDoc("v1", new()
        {
            Title = "PBL4 Fire Safety API",
            Version = "v1",
            Description = "Backend API cho Hệ thống AIoT Giám sát Hỏa hoạn PBL4"
        });
    });

    // ─── SignalR Hub ─────────────────────────────────────────────────────────
    builder.Services.AddSignalR(opt =>
    {
        opt.EnableDetailedErrors = builder.Environment.IsDevelopment();
        opt.KeepAliveInterval = TimeSpan.FromSeconds(15);
        opt.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        opt.HandshakeTimeout = TimeSpan.FromSeconds(10);
    });

    // ─── CORS: cho phép Mobile App (Flutter) kết nối SignalR ────────────────
    builder.Services.AddCors(opt =>
    {
        opt.AddPolicy("Pbl4CorsPolicy", policy =>
        {
            policy
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                // Cho phép máy ảo Android (10.0.2.2) và thiết bị thật LAN
                .WithOrigins(
                    "http://10.0.2.2:5000",
                    "http://localhost:5000",
                    "http://localhost:3000");
        });
    });

    // ─── Infrastructure Services (Options, EF, MQTT, AI, FCM) ───────────────
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // ─── Health Checks ───────────────────────────────────────────────────────
    builder.Services.AddHealthChecks();

    // ─── Build Application ───────────────────────────────────────────────────
    var app = builder.Build();

    // ─── Middleware Pipeline ─────────────────────────────────────────────────
    // Ghi log mỗi HTTP request vào Serilog (thay thế UseHttpLogging).
    app.UseSerilogRequestLogging(opt =>
    {
        opt.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0.0000}ms)";
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PBL4 Fire Safety API v1"));
        app.UseDeveloperExceptionPage();
    }

    app.UseRouting();
    app.UseCors("Pbl4CorsPolicy");
    app.UseAuthorization();

    // ─── Endpoints ───────────────────────────────────────────────────────────
    app.MapControllers();

    // SignalR Hub endpoint (sẽ triển khai đầy đủ ở BE-07)
    // app.MapHub<FireSafetyHub>("/hubs/firesafety");

    app.MapHealthChecks("/health");

    Log.Information("=== Backend khởi động thành công. Đang lắng nghe... ===");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Backend dừng đột ngột do lỗi không xử lý được!");
}
finally
{
    Log.CloseAndFlush();
}
