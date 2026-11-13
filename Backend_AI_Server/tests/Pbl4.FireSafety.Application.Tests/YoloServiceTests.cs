using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pbl4.FireSafety.Application.Options;
using Pbl4.FireSafety.Infrastructure.AI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Pbl4.FireSafety.Application.Tests;

public class YoloServiceTests
{
    private static YoloOnnxService CreateService(string modelPath = "models/non_existent.onnx")
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AiModelOptions
        {
            YoloOnnxPath = modelPath,
            YoloConfidenceThreshold = 0.45f,
            YoloIouThreshold = 0.45f,
            EnableHotReload = false
        });

        return new YoloOnnxService(NullLogger<YoloOnnxService>.Instance, options);
    }

    private static byte[] CreateTestImageBytes(Rgb24 color, int width = 100, int height = 100)
    {
        using var image = new Image<Rgb24>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = color;
            }
        }

        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Detect_NullOrEmptyBytes_ReturnsInvalidResult()
    {
        // Arrange
        using var service = CreateService();

        // Act
        var resultNull = service.Detect(null!);
        var resultEmpty = service.Detect(Array.Empty<byte>());

        // Assert
        Assert.False(resultNull.HasFire);
        Assert.False(resultNull.HasSmoke);
        Assert.Equal("Invalid-EmptyPayload", resultNull.ModelSource);

        Assert.False(resultEmpty.HasFire);
        Assert.False(resultEmpty.HasSmoke);
        Assert.Equal("Invalid-EmptyPayload", resultEmpty.ModelSource);
    }

    [Fact]
    public void Detect_WhenNoModelFile_UsesFallbackHeuristic()
    {
        // Arrange
        using var service = CreateService();
        var safeBytes = CreateTestImageBytes(new Rgb24(50, 150, 50)); // Màu xanh lá cây an toàn

        // Act
        var result = service.Detect(safeBytes);

        // Assert
        Assert.False(result.HasFire);
        Assert.False(result.HasSmoke);
        Assert.Equal("Fallback-Heuristic", result.ModelSource);
    }

    [Fact]
    public void Detect_FireColoredImage_InFallback_DetectsFire()
    {
        // Arrange
        using var service = CreateService();
        // Màu đỏ cam rực lửa (R > 210, G > 100, B < 90)
        var fireBytes = CreateTestImageBytes(new Rgb24(250, 120, 20));

        // Act
        var result = service.Detect(fireBytes);

        // Assert
        Assert.True(result.HasFire);
        Assert.False(result.HasSmoke);
        Assert.True(result.MaxFireConfidence >= 0.45f);
        Assert.Single(result.Detections);
        Assert.Equal("Fire", result.Detections[0].Label);
    }

    [Fact]
    public void Detect_SafeBlueImage_InFallback_DetectsSafe()
    {
        // Arrange
        using var service = CreateService();
        // Màu xanh dương mát mẻ
        var coolBytes = CreateTestImageBytes(new Rgb24(30, 80, 220));

        // Act
        var result = service.Detect(coolBytes);

        // Assert
        Assert.False(result.HasFire);
        Assert.False(result.HasSmoke);
        Assert.Equal(0f, result.MaxFireConfidence);
        Assert.Empty(result.Detections);
    }

    [Fact]
    public async Task ReloadModelAsync_WhenModelFileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        using var service = CreateService("models/missing.onnx");

        // Act
        var success = await service.ReloadModelAsync();

        // Assert
        Assert.False(success);
        Assert.False(service.IsModelLoaded);
    }
}
