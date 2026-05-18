using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using FlowDesigner.Shared.Models;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace FlowDesigner.Api.Services;

public class VideoStreamService : IDisposable
{
    private readonly ILogger<VideoStreamService> _logger;
    private readonly ConcurrentDictionary<string, StreamProcessor> _activeStreams;
    private readonly object _lockObject = new();
    private bool _disposed;

    public VideoStreamService(ILogger<VideoStreamService> logger)
    {
        _logger = logger;
        _activeStreams = new ConcurrentDictionary<string, StreamProcessor>();
    }

    public Task<bool> StartStreamAsync(string streamId, VideoStreamConfig config)
    {
        try
        {
            if (_activeStreams.ContainsKey(streamId))
            {
                _logger.LogWarning("流已存在: {Id}", streamId);
                return Task.FromResult(false);
            }

            var processor = new StreamProcessor(streamId, config, _logger);
            if (_activeStreams.TryAdd(streamId, processor))
            {
                processor.Start();
                _logger.LogInformation("流已启动: {Id}", streamId);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动流失败: {Id}", streamId);
            return Task.FromResult(false);
        }
    }

    public Task<bool> StopStreamAsync(string streamId)
    {
        if (_activeStreams.TryRemove(streamId, out var processor))
        {
            processor.Dispose();
            _logger.LogInformation("流已停止: {Id}", streamId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<VideoFrame?> GetLatestFrameAsync(string streamId)
    {
        if (_activeStreams.TryGetValue(streamId, out var processor))
        {
            return Task.FromResult(processor.LatestFrame);
        }

        return Task.FromResult<VideoFrame?>(null);
    }

    public Task<List<VideoFrame>> GetRecentFramesAsync(string streamId, int count = 10)
    {
        if (_activeStreams.TryGetValue(streamId, out var processor))
        {
            return Task.FromResult(processor.GetRecentFrames(count));
        }

        return Task.FromResult(new List<VideoFrame>());
    }

    public Task<VideoStreamStats> GetStreamStatsAsync(string streamId)
    {
        if (_activeStreams.TryGetValue(streamId, out var processor))
        {
            return Task.FromResult(processor.GetStats());
        }

        return Task.FromResult(new VideoStreamStats
        {
            IsActive = false,
            Error = "Stream not found"
        });
    }

    public Task<IEnumerable<string>> GetActiveStreamsAsync()
    {
        return Task.FromResult(_activeStreams.Keys.AsEnumerable());
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            foreach (var processor in _activeStreams.Values)
            {
                processor.Dispose();
            }
            _activeStreams.Clear();
        }

        _disposed = true;
    }
}

public class StreamProcessor : IDisposable
{
    private readonly string _streamId;
    private readonly VideoStreamConfig _config;
    private readonly ILogger _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ConcurrentQueue<VideoFrame> _frameBuffer;
    private readonly object _lockObject = new();
    private volatile bool _disposed;
    private VideoFrame? _latestFrame;
    private long _totalFrames;
    private long _droppedFrames;
    private long _processedFrames;
    private DateTime _startTime;
    private readonly List<double> _processingTimes;

    public VideoFrame? LatestFrame => Volatile.Read(ref _latestFrame);

    public StreamProcessor(string streamId, VideoStreamConfig config, ILogger logger)
    {
        _streamId = streamId;
        _config = config;
        _logger = logger;
        _frameBuffer = new ConcurrentQueue<VideoFrame>();
        _processingTimes = new List<double>();
    }

    public void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _startTime = DateTime.UtcNow;

        Task.Run(() => ProcessStreamAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
    }

    private async Task ProcessStreamAsync(CancellationToken cancellationToken)
    {
        try
        {
            var capture = new VideoCapture(_config.SourcePathOrUrl);
            
            if (!capture.IsOpened)
            {
                _logger.LogError("无法打开视频源: {Source}", _config.SourcePathOrUrl);
                capture.Dispose();
                return;
            }

            // 设置帧率（如果是 RTSP）
            if (_config.SourceType == VideoSourceType.RTSP)
            {
                capture.Set(Emgu.CV.CvEnum.CapProp.Fps, _config.Fps);
            }

            _logger.LogInformation(
                "视频流已打开: {Source}, 分辨率: {Width}x{Height}, FPS: {Fps}",
                _config.SourcePathOrUrl,
                capture.Get(Emgu.CV.CvEnum.CapProp.FrameWidth),
                capture.Get(Emgu.CV.CvEnum.CapProp.FrameHeight),
                capture.Get(Emgu.CV.CvEnum.CapProp.Fps));

            var frameInterval = TimeSpan.FromSeconds(1.0 / _config.Fps);
            var skipCount = Math.Max(0, _config.FrameSkip);
            var skipCounter = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var frameMat = new Mat();
                var readSuccess = capture.Read(frameMat);

                if (!readSuccess || frameMat.IsEmpty)
                {
                    frameMat.Dispose();
                    Interlocked.Increment(ref _droppedFrames);
                    
                    // 如果是 RTSP，尝试重新连接
                    if (_config.SourceType == VideoSourceType.RTSP && _config.AutoReconnect)
                    {
                        await Task.Delay(1000, cancellationToken);
                        capture.Dispose();
                        capture = new VideoCapture(_config.SourcePathOrUrl);
                    }
                    
                    continue;
                }

                skipCounter++;
                if (skipCounter <= skipCount)
                {
                    frameMat.Dispose();
                    continue;
                }
                skipCounter = 0;

                Interlocked.Increment(ref _totalFrames);

                // 编码帧
                var frame = new VideoFrame
                {
                    Timestamp = DateTime.UtcNow,
                    FrameNumber = _totalFrames
                };

                // 转换为 JPEG
                if (_config.EnableEncoding)
                {
                    frame.ImageData = EncodeFrame(frameMat, _config.Quality);
                    frame.Width = frameMat.Width;
                    frame.Height = frameMat.Height;
                }

                frameMat.Dispose();
                stopwatch.Stop();
                frame.ProcessingTime = stopwatch.Elapsed;

                // 更新最新帧
                Volatile.Write(ref _latestFrame, frame);

                // 添加到缓冲区
                lock (_lockObject)
                {
                    _frameBuffer.Enqueue(frame);
                    while (_frameBuffer.Count > _config.MaxBufferSize)
                    {
                        _frameBuffer.TryDequeue(out _);
                    }
                }

                // 更新统计
                lock (_processingTimes)
                {
                    _processingTimes.Add(frame.ProcessingTime.TotalMilliseconds);
                    if (_processingTimes.Count > 100)
                    {
                        _processingTimes.RemoveAt(0);
                    }
                }

                Interlocked.Increment(ref _processedFrames);

                // 控制帧率
                var elapsed = stopwatch.Elapsed;
                if (elapsed < frameInterval)
                {
                    await Task.Delay(frameInterval - elapsed, cancellationToken);
                }
            }
            
            capture?.Dispose();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("流处理已取消: {Id}", _streamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流处理错误: {Id}", _streamId);
        }
    }

    private byte[] EncodeFrame(Mat frame, int quality)
    {
        var buffer = new VectorOfByte();
        var encodeParams = new KeyValuePair<Emgu.CV.CvEnum.ImwriteFlags, int>[]
        {
            new(Emgu.CV.CvEnum.ImwriteFlags.JpegQuality, quality)
        };
        CvInvoke.Imencode(".jpg", frame, buffer, encodeParams);
        
        return buffer.ToArray();
    }

    public List<VideoFrame> GetRecentFrames(int count)
    {
        lock (_lockObject)
        {
            return _frameBuffer.TakeLast(count).ToList();
        }
    }

    public VideoStreamStats GetStats()
    {
        double avgTime, maxTime, minTime;
        
        lock (_processingTimes)
        {
            avgTime = _processingTimes.Count > 0 ? _processingTimes.Average() : 0;
            maxTime = _processingTimes.Count > 0 ? _processingTimes.Max() : 0;
            minTime = _processingTimes.Count > 0 ? _processingTimes.Min() : 0;
        }

        var elapsed = DateTime.UtcNow - _startTime;
        var fps = elapsed.TotalSeconds > 0 ? _processedFrames / elapsed.TotalSeconds : 0;

        return new VideoStreamStats
        {
            IsActive = true,
            TotalFrames = _totalFrames,
            ProcessedFrames = _processedFrames,
            DroppedFrames = _droppedFrames,
            BufferSize = _frameBuffer.Count,
            AvgProcessingTime = avgTime,
            MaxProcessingTime = maxTime,
            MinProcessingTime = minTime,
            CurrentFps = fps,
            ElapsedTime = elapsed
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _disposed = true;
    }
}

public class VideoStreamConfig
{
    public VideoSourceType SourceType { get; set; }
    public string SourcePathOrUrl { get; set; } = string.Empty;
    public int Fps { get; set; } = 30;
    public int FrameSkip { get; set; } = 0;
    public int Quality { get; set; } = 85;
    public int BufferSize { get; set; } = 10;
    public int MaxBufferSize { get; set; } = 100;
    public bool EnableEncoding { get; set; } = true;
    public bool AutoReconnect { get; set; } = true;
}

public enum VideoSourceType
{
    File,
    RTSP,
    Camera
}

public class VideoFrame
{
    public DateTime Timestamp { get; set; }
    public long FrameNumber { get; set; }
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class VideoStreamStats
{
    public bool IsActive { get; set; }
    public string? Error { get; set; }
    public long TotalFrames { get; set; }
    public long ProcessedFrames { get; set; }
    public long DroppedFrames { get; set; }
    public int BufferSize { get; set; }
    public double AvgProcessingTime { get; set; }
    public double MaxProcessingTime { get; set; }
    public double MinProcessingTime { get; set; }
    public double CurrentFps { get; set; }
    public TimeSpan ElapsedTime { get; set; }
}
