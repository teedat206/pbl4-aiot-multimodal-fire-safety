using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Pbl4.FireSafety.Application.Contracts.Services;
using Pbl4.FireSafety.Application.DTOs;
using Pbl4.FireSafety.Application.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Pbl4.FireSafety.Infrastructure.AI;

/// <summary>
/// Thực thi suy luận phát hiện ngọn lửa và khói từ frame ảnh camera sử dụng YOLOv8 qua ONNX Runtime.
/// Hỗ trợ thread-safe, non-maximum suppression (NMS), hot-reload watcher và fallback heuristic khi chưa có model.
/// </summary>
public class YoloOnnxService : IHostedService, IYoloService, IDisposable
{
    private const int TargetWidth = 640;
    private const int TargetHeight = 640;

    private readonly ILogger<YoloOnnxService> _logger;
    private readonly AiModelOptions _options;
    private readonly string _modelAbsolutePath;

    private InferenceSession? _session;
    private readonly ReaderWriterLockSlim _sessionLock = new();

    private FileSystemWatcher? _fileWatcher;
    private CancellationTokenSource? _debounceCts;
    private readonly object _debounceLock = new();
    private bool _disposed;

    private static readonly string[] DefaultClasses = { "Fire", "Smoke" };

    public bool IsModelLoaded
    {
        get
        {
            _sessionLock.EnterReadLock();
            try
            {
                return _session != null;
            }
            finally
            {
                _sessionLock.ExitReadLock();
            }
        }
    }

    public YoloOnnxService(
        ILogger<YoloOnnxService> logger,
        IOptions<AiModelOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        var configuredPath = _options.YoloOnnxPath;
        if (Path.IsPathRooted(configuredPath))
        {
            _modelAbsolutePath = configuredPath;
        }
        else
        {
            _modelAbsolutePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Khởi tạo YoloOnnxService. Đường dẫn model: {Path}", _modelAbsolutePath);

        LoadModelInternal();

        if (_options.EnableHotReload)
        {
            SetupFileSystemWatcher();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Đang dừng YoloOnnxService...");
        _fileWatcher?.Dispose();
        _fileWatcher = null;
        return Task.CompletedTask;
    }

    public Task<bool> ReloadModelAsync()
    {
        return Task.FromResult(LoadModelInternal());
    }

    public YoloDetectionResult Detect(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return new YoloDetectionResult
            {
                HasFire = false,
                HasSmoke = false,
                ModelSource = "Invalid-EmptyPayload"
            };
        }

        var sw = Stopwatch.StartNew();

        _sessionLock.EnterReadLock();
        try
        {
            if (_session != null)
            {
                return RunOnnxInference(_session, imageBytes, sw);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi xảy ra trong quá trình chạy YOLO ONNX. Chuyển sang Fallback Heuristic");
        }
        finally
        {
            _sessionLock.ExitReadLock();
        }

        return RunFallbackInference(imageBytes, sw);
    }

    public Task<YoloDetectionResult> DetectAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        return Task.Run(() => Detect(imageBytes), ct);
    }

    private YoloDetectionResult RunOnnxInference(InferenceSession session, byte[] imageBytes, Stopwatch sw)
    {
        using var image = Image.Load<Rgb24>(imageBytes);
        int originalWidth = image.Width;
        int originalHeight = image.Height;

        // Tiền xử lý: Resize 640x640 và chuẩn hóa CHW [1, 3, 640, 640]
        image.Mutate(x => x.Resize(TargetWidth, TargetHeight));

        var inputTensor = new DenseTensor<float>(new[] { 1, 3, TargetHeight, TargetWidth });
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++)
                {
                    var pixel = row[x];
                    inputTensor[0, 0, y, x] = pixel.R / 255.0f;
                    inputTensor[0, 1, y, x] = pixel.G / 255.0f;
                    inputTensor[0, 2, y, x] = pixel.B / 255.0f;
                }
            }
        });

        var inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
        };

        using var results = session.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();

        // Phân tích tensor kết quả YOLOv8 và lọc qua NMS
        var boxes = ParseYoloOutput(outputTensor, originalWidth, originalHeight);
        var filteredBoxes = ApplyNonMaximumSuppression(boxes, _options.YoloIouThreshold);

        sw.Stop();

        float maxFire = 0f;
        float maxSmoke = 0f;
        foreach (var b in filteredBoxes)
        {
            if (b.Label.Equals("Fire", StringComparison.OrdinalIgnoreCase) && b.Confidence > maxFire)
            {
                maxFire = b.Confidence;
            }
            else if (b.Label.Equals("Smoke", StringComparison.OrdinalIgnoreCase) && b.Confidence > maxSmoke)
            {
                maxSmoke = b.Confidence;
            }
        }

        return new YoloDetectionResult
        {
            HasFire = maxFire >= _options.YoloConfidenceThreshold,
            HasSmoke = maxSmoke >= _options.YoloConfidenceThreshold,
            MaxFireConfidence = maxFire,
            MaxSmokeConfidence = maxSmoke,
            Detections = filteredBoxes,
            ModelSource = "Onnx-YOLOv8",
            InferenceTimeMs = sw.ElapsedMilliseconds
        };
    }

    private List<YoloBoundingBox> ParseYoloOutput(Tensor<float> output, int origWidth, int origHeight)
    {
        var candidates = new List<YoloBoundingBox>();
        var dims = output.Dimensions.ToArray();

        if (dims.Length < 3) return candidates;

        // YOLOv8 xuất ra định dạng [1, 4 + C, 8400] hoặc [1, 8400, 4 + C]
        bool isChannelsFirst = dims[1] < dims[2];
        int numClasses = isChannelsFirst ? dims[1] - 4 : dims[2] - 4;
        int numAnchors = isChannelsFirst ? dims[2] : dims[1];

        if (numClasses <= 0 || numAnchors <= 0) return candidates;

        for (int i = 0; i < numAnchors; i++)
        {
            float cx, cy, w, h;

            if (isChannelsFirst)
            {
                cx = output[0, 0, i];
                cy = output[0, 1, i];
                w = output[0, 2, i];
                h = output[0, 3, i];
            }
            else
            {
                cx = output[0, i, 0];
                cy = output[0, i, 1];
                w = output[0, i, 2];
                h = output[0, i, 3];
            }

            int bestClass = -1;
            float bestScore = 0f;

            for (int c = 0; c < numClasses; c++)
            {
                float score = isChannelsFirst ? output[0, 4 + c, i] : output[0, i, 4 + c];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = c;
                }
            }

            if (bestScore >= _options.YoloConfidenceThreshold)
            {
                // Quy đổi tọa độ về kích thước ảnh gốc
                float scaleX = (float)origWidth / TargetWidth;
                float scaleY = (float)origHeight / TargetHeight;

                float x = (cx - (w / 2f)) * scaleX;
                float y = (cy - (h / 2f)) * scaleY;
                float boxWidth = w * scaleX;
                float boxHeight = h * scaleY;

                string label = bestClass < DefaultClasses.Length ? DefaultClasses[bestClass] : $"Class_{bestClass}";

                candidates.Add(new YoloBoundingBox(
                    bestClass,
                    label,
                    bestScore,
                    Math.Max(0, x),
                    Math.Max(0, y),
                    boxWidth,
                    boxHeight
                ));
            }
        }

        return candidates;
    }

    private static List<YoloBoundingBox> ApplyNonMaximumSuppression(List<YoloBoundingBox> boxes, float iouThreshold)
    {
        var result = new List<YoloBoundingBox>();
        var sorted = boxes.OrderByDescending(b => b.Confidence).ToList();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);

            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                if (sorted[i].ClassId == best.ClassId)
                {
                    float iou = ComputeIoU(best, sorted[i]);
                    if (iou > iouThreshold)
                    {
                        sorted.RemoveAt(i);
                    }
                }
            }
        }

        return result;
    }

    private static float ComputeIoU(YoloBoundingBox a, YoloBoundingBox b)
    {
        float x1 = Math.Max(a.X, b.X);
        float y1 = Math.Max(a.Y, b.Y);
        float x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        float y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        float intersectionWidth = Math.Max(0, x2 - x1);
        float intersectionHeight = Math.Max(0, y2 - y1);
        float intersectionArea = intersectionWidth * intersectionHeight;

        if (intersectionArea <= 0) return 0f;

        float areaA = a.Width * a.Height;
        float areaB = b.Width * b.Height;
        float unionArea = areaA + areaB - intersectionArea;

        return unionArea <= 0 ? 0f : (intersectionArea / unionArea);
    }

    private YoloDetectionResult RunFallbackInference(byte[] imageBytes, Stopwatch sw)
    {
        try
        {
            using var image = Image.Load<Rgb24>(imageBytes);
            int sampleStep = Math.Max(1, Math.Min(image.Width, image.Height) / 50);
            int firePixelCount = 0;
            int totalSampled = 0;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y += sampleStep)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x += sampleStep)
                    {
                        var p = row[x];
                        totalSampled++;
                        // Heuristic sắc tố ngọn lửa: Màu đỏ cam sáng nổi bật (R cao, R > G > B)
                        if (p.R > 210 && p.G > 100 && p.B < 90 && p.R > p.G)
                        {
                            firePixelCount++;
                        }
                    }
                }
            });

            sw.Stop();

            float ratio = totalSampled > 0 ? (float)firePixelCount / totalSampled : 0f;
            bool hasFire = ratio > 0.05f;
            float confidence = hasFire ? Math.Min(0.95f, 0.50f + (ratio * 2f)) : 0.05f;

            var detections = new List<YoloBoundingBox>();
            if (hasFire)
            {
                detections.Add(new YoloBoundingBox(
                    0,
                    "Fire",
                    confidence,
                    image.Width * 0.25f,
                    image.Height * 0.25f,
                    image.Width * 0.5f,
                    image.Height * 0.5f));
            }

            return new YoloDetectionResult
            {
                HasFire = hasFire,
                HasSmoke = false,
                MaxFireConfidence = hasFire ? confidence : 0f,
                MaxSmokeConfidence = 0f,
                Detections = detections,
                ModelSource = "Fallback-Heuristic",
                InferenceTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể phân tích frame ảnh qua Fallback Heuristic");
            sw.Stop();
            return new YoloDetectionResult
            {
                HasFire = false,
                HasSmoke = false,
                ModelSource = "Fallback-Failed",
                InferenceTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    private bool LoadModelInternal()
    {
        if (!File.Exists(_modelAbsolutePath))
        {
            _logger.LogWarning(
                "Chưa tìm thấy file mô hình YOLOv8 tại '{Path}'. Kích hoạt chế độ Fallback Heuristic dự phòng cho đến khi nhóm AI nạp file vào.",
                _modelAbsolutePath);
            return false;
        }

        try
        {
            _logger.LogInformation("Đang nạp file mô hình YOLOv8 ONNX từ: {Path}...", _modelAbsolutePath);
            var sw = Stopwatch.StartNew();

            var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
            };

            var newSession = new InferenceSession(_modelAbsolutePath, sessionOptions);
            sw.Stop();

            InferenceSession? oldSession;
            _sessionLock.EnterWriteLock();
            try
            {
                oldSession = _session;
                _session = newSession;
            }
            finally
            {
                _sessionLock.ExitWriteLock();
            }

            oldSession?.Dispose();

            _logger.LogInformation(
                "Nạp thành công mô hình YOLOv8 ONNX trong {Elapsed}ms. Inputs: {InputsCount}, Outputs: {OutputsCount}",
                sw.ElapsedMilliseconds,
                newSession.InputMetadata.Count,
                newSession.OutputMetadata.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể nạp file ONNX tại '{Path}'. Giữ nguyên trạng thái hiện tại", _modelAbsolutePath);
            return false;
        }
    }

    private void SetupFileSystemWatcher()
    {
        try
        {
            var directory = Path.GetDirectoryName(_modelAbsolutePath);
            var fileName = Path.GetFileName(_modelAbsolutePath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            _fileWatcher = new FileSystemWatcher(directory ?? ".", fileName ?? "*.onnx")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnModelFileChanged;
            _fileWatcher.Created += OnModelFileChanged;
            _fileWatcher.Renamed += OnModelFileChanged;

            _logger.LogInformation("Đã kích hoạt FileSystemWatcher giám sát Hot-Reload YOLOv8 tại: {Path}", _modelAbsolutePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể khởi tạo FileSystemWatcher cho YOLOv8.");
        }
    }

    private void OnModelFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            Task.Delay(500, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    _logger.LogInformation("Phát hiện thay đổi file mô hình YOLOv8 '{Path}'. Tiến hành Hot-Reload...", e.FullPath);
                    LoadModelInternal();
                }
            }, TaskScheduler.Default);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _debounceCts?.Dispose(); } catch { }
        try { _fileWatcher?.Dispose(); } catch { }

        try
        {
            _sessionLock.EnterWriteLock();
            try
            {
                _session?.Dispose();
                _session = null;
            }
            finally
            {
                _sessionLock.ExitWriteLock();
            }
        }
        catch { }

        try { _sessionLock.Dispose(); } catch { }
        GC.SuppressFinalize(this);
    }
}
