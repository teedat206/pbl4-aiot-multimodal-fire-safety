using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pbl4.FireSafety.Infrastructure.Persistence;

/// <summary>
/// Factory dùng cho các công cụ thiết kế (Design-time tools như dotnet-ef migrations).
/// Giúp công cụ EF CLI khởi tạo DbContext mà không cần chạy toàn bộ ứng dụng ASP.NET Web API.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Pbl4Database");

        if (string.IsNullOrEmpty(connectionString))
        {
            var apiPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Pbl4.FireSafety.API", "appsettings.json");
            if (!File.Exists(apiPath))
            {
                apiPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            }

            if (File.Exists(apiPath))
            {
                try
                {
                    var json = File.ReadAllText(apiPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var csSection) &&
                        csSection.TryGetProperty("Pbl4Database", out var csProp))
                    {
                        connectionString = csProp.GetString();
                    }
                }
                catch
                {
                    // Fallback bên dưới
                }
            }
        }

        connectionString ??= "Server=localhost;Database=pbl4_firesafety;User Id=root;Password=root;";

        var builder = new DbContextOptionsBuilder<AppDbContext>();

        builder.UseMySQL(connectionString, mySqlOptions =>
        {
            mySqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
        });

        return new AppDbContext(builder.Options);
    }
}
