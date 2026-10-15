using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Pbl4.FireSafety.Application.Contracts.Services;
using Pbl4.FireSafety.Application.Options;
using Pbl4.FireSafety.Domain.Enums;

namespace Pbl4.FireSafety.Infrastructure.AI;

/// <summary>
/// Thực thi suy luận mô hình Random Forest qua ONNX Runtime với cơ chế Hot-Reloading thread-safe.
/// Cho phép nhóm AI thay thế nóng file rf_model.onnx mà không cần khởi động lại Server.
/// </summary>
public class RandomForestOnnxService : IRandomForestService, IHostedService, IDisposable
{
    private readonly ILogger<RandomForestOnnxService> _logger;
    private readonly AiModelOptions _options;
    private readonly string _modelAbsolutePath;

    private InferenceSession? _session;
    private readonly ReaderWriterLockSlim _sessionLock = new();

    private FileSystemWatcher? _fileWatcher;
    private CancellationTokenSource? _debounceCts;
    private readonly object _debounceLock = new();

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

    public RandomForestOnnxService(
        ILogger<RandomForestOnnxService> logger,
        IOptions<AiModelOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Xác định đường dẫn tuyệt đối của file model
        var configuredPath = _options.RandomForestOnnxPath;
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
        _logger.LogInformation("Khởi tạo RandomForestOnnxService. Đường dẫn model: {Path}", _modelAbsolutePath);

        // 1. Thử nạp model lần đầu
        LoadModelInternal();

        // 2. Kích hoạt Hot-Reloading nếu được bật trong cấu hình
        if (_options.EnableHotReload)
        {
            SetupFileSystemWatcher();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Đang dừng RandomForestOnnxService...");
        _fileWatcher?.Dispose();
        _fileWatcher = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Thực hiện suy luận dự đoán nguy cơ cháy nổ từ vector [smoke, gas, co, temp].
    /// Thread-safe hoàn toàn, không xung đột khi đang reload model.
    /// </summary>
    public SensorInferenceResult Predict(float smoke, float gas, float co, float temp)
    {
        _sessionLock.EnterReadLock();
        try
        {
            if (_session != null)
            {
                return RunOnnxInference(_session, smoke, gas, co, temp);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong quá trình thực thi ONNX Inference. Chuyển sang Fallback");
        }
        finally
        {
            _sessionLock.ExitReadLock();
        }

        // Nếu session chưa nạp hoặc có lỗi, sử dụng Fallback dự phòng
        return RunFallbackInference(smoke, gas, co, temp);
    }

    /// <summary>
    /// Nạp lại model theo yêu cầu từ bên ngoài (Manual Hot-Reload).
    /// </summary>
    public Task<bool> ReloadModelAsync()
    {
        return Task.FromResult(LoadModelInternal());
    }

    private bool LoadModelInternal()
    {
        if (!File.Exists(_modelAbsolutePath))
        {
            _logger.LogWarning(
                "Chưa tìm thấy file mô hình Random Forest tại '{Path}'. Đang kích hoạt chế độ Fallback Rule-Based dự phòng cho đến khi nhóm AI nạp file vào",
                _modelAbsolutePath);
            return false;
        }

        try
        {
            _logger.LogInformation("Đang nạp file mô hình ONNX từ: {Path}...", _modelAbsolutePath);
            var sw = Stopwatch.StartNew();

            var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
            };

            // Tạo session mới độc lập
            var newSession = new InferenceSession(_modelAbsolutePath, sessionOptions);
            sw.Stop();

            // Hoán đổi nóng thread-safe (Atomic Swap)
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

            // Giải phóng session cũ
            oldSession?.Dispose();

            _logger.LogInformation(
                "Nạp thành công mô hình Random Forest ONNX trong {Elapsed}ms. Model Inputs: {InputsCount}, Outputs: {OutputsCount}",
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

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                return;
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _fileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnModelFileChanged;
            _fileWatcher.Created += OnModelFileChanged;
            _fileWatcher.Renamed += OnModelFileChanged;

            _logger.LogInformation(
                "Cơ chế Hot-Reloading ONNX đã kích hoạt. Đang giám sát file: '{Directory}/{FileName}'",
                directory,
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể khởi tạo FileSystemWatcher cho Hot-Reloading");
        }
    }

    private void OnModelFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Phát hiện thay đổi trên file mô hình: {Path}. Chuẩn bị Hot-Reload...", e.FullPath);

        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            // Debounce 500ms để hệ điều hành hoàn tất việc ghi file
            Task.Delay(500, token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;

                // Thử mở file xem file đã hoàn tất ghi chưa
                if (WaitForFileAvailable(e.FullPath, TimeSpan.FromSeconds(3)))
                {
                    LoadModelInternal();
                }
                else
                {
                    _logger.LogWarning("File model vẫn đang bị khóa bởi tiến trình khác, bỏ qua lần reload này");
                }
            }, TaskScheduler.Default);
        }
    }

    private static bool WaitForFileAvailable(string filePath, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length > 0)
                {
                    return true;
                }
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
        return false;
    }

    private SensorInferenceResult RunOnnxInference(
        InferenceSession session,
        float smoke,
        float gas,
        float co,
        float temp)
    {
        // Vector đầu vào chuẩn: [smoke, gas, co, temp]
        var inputValues = new[] { smoke, gas, co, temp };
        var dimensions = new[] { 1, 4 };
        var tensor = new DenseTensor<float>(inputValues, dimensions);

        // Lấy tên input node đầu tiên từ metadata của model (thường là "float_input" hoặc "X")
        var inputName = session.InputMetadata.Keys.First();
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, tensor)
        };

        using var results = session.Run(inputs);

        var predictedClass = 0;
        var confidence = 0.0f;
        var probabilities = new float[5];

        // Duyệt các output của ONNX (thường có label và probabilities)
        foreach (var result in results)
        {
            // Trường hợp 1: Output dạng nhãn dự đoán (int64)
            if (result.Value is Tensor<long> labelTensor)
            {
                predictedClass = (int)labelTensor.GetValue(0);
            }
            // Trường hợp 2: Output dạng nhãn int32
            else if (result.Value is Tensor<int> labelInt32Tensor)
            {
                predictedClass = labelInt32Tensor.GetValue(0);
            }
            // Trường hợp 3: Output dạng mảng xác suất DenseTensor<float>
            else if (result.Value is Tensor<float> probTensor)
            {
                var length = Math.Min(5, (int)probTensor.Length);
                for (var i = 0; i < length; i++)
                {
                    probabilities[i] = probTensor.GetValue(i);
                }
            }
            // Trường hợp 4: Output của Scikit-Learn dạng Sequence of Maps (IDictionary)
            else if (result.Value is IEnumerable<IDictionary<long, float>> mapSeq)
            {
                var firstMap = mapSeq.FirstOrDefault();
                if (firstMap != null)
                {
                    foreach (var (k, v) in firstMap)
                    {
                        if (k >= 0 && k < 5)
                        {
                            probabilities[k] = v;
                        }
                    }
                }
            }
        }

        // Giới hạn class trong khoảng 0..4
        predictedClass = Math.Clamp(predictedClass, 0, 4);

        // Xác định confidence: nếu có mảng xác suất thì lấy xác suất của class được chọn
        if (probabilities.Any(p => p > 0))
        {
            confidence = probabilities[predictedClass];
        }
        else
        {
            confidence = 0.85f; // Mặc định nếu model chỉ trả label
            probabilities[predictedClass] = confidence;
        }

        return new SensorInferenceResult
        {
            RiskClass = (FireRiskClass)predictedClass,
            Confidence = confidence,
            Probabilities = probabilities,
            ModelSource = "ONNX-RandomForest"
        };
    }

    /// <summary>
    /// Chế độ suy luận dự phòng nếu chưa có file ONNX trên đĩa.
    /// Dựa trên các ngưỡng an toàn để tính toán giả lập chuẩn xác.
    /// </summary>
    private static SensorInferenceResult RunFallbackInference(float smoke, float gas, float co, float temp)
    {
        var riskScore = 0;

        if (smoke > 0.8f || gas > 500f || co > 200f || temp > 55f)
        {
            riskScore = 4; // Cháy xác nhận
        }
        else if (smoke > 0.6f || gas > 350f || co > 120f || temp > 45f)
        {
            riskScore = 3; // Nguy hiểm cao
        }
        else if (smoke > 0.4f || gas > 200f || co > 70f || temp > 38f)
        {
            riskScore = 2; // Nguy hiểm trung bình
        }
        else if (smoke > 0.2f || gas > 100f || co > 40f || temp > 32f)
        {
            riskScore = 1; // Nguy cơ thấp
        }
        else
        {
            riskScore = 0; // An toàn
        }

        var probs = new float[5];
        probs[riskScore] = 0.90f;

        return new SensorInferenceResult
        {
            RiskClass = (FireRiskClass)riskScore,
            Confidence = 0.90f,
            Probabilities = probs,
            ModelSource = "Fallback-RuleBased"
        };
    }

    public void Dispose()
    {
        _debounceCts?.Dispose();
        _fileWatcher?.Dispose();

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

        _sessionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
