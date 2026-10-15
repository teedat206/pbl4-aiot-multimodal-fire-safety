using System.Collections.Concurrent;
using Pbl4.FireSafety.Application.Contracts.Services;
using Pbl4.FireSafety.Domain.Models;

namespace Pbl4.FireSafety.Infrastructure.Persistence;

/// <summary>
/// Thực thi bộ nhớ đệm In-Memory Ring Buffer thread-safe lưu trữ tối đa 60 bản ghi Telemetry gần nhất.
/// </summary>
public class MockTelemetryStore : IMockTelemetryStore
{
    private const int MaxCapacity = 60;
    private readonly ConcurrentQueue<SystemTelemetryReport> _buffer = new();
    private SystemTelemetryReport? _latest;
    private readonly object _lock = new();

    public int Count => _buffer.Count;

    public void Add(SystemTelemetryReport report)
    {
        lock (_lock)
        {
            _buffer.Enqueue(report);
            _latest = report;

            // Cơ chế FIFO: Nếu vượt quá 60 bản ghi, loại bỏ bản ghi cũ nhất
            while (_buffer.Count > MaxCapacity)
            {
                _buffer.TryDequeue(out _);
            }
        }
    }

    public IReadOnlyList<SystemTelemetryReport> GetRecent(int count = 60)
    {
        lock (_lock)
        {
            var items = _buffer.ToArray();
            if (count >= items.Length)
            {
                return items;
            }

            // Lấy `count` phần tử mới nhất
            return items.Skip(items.Length - count).ToArray();
        }
    }

    public SystemTelemetryReport? GetLatest()
    {
        return _latest;
    }
}
