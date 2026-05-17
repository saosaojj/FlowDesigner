using System.Diagnostics;
using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public class ImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;
    
    public ImageProcessingService(ILogger<ImageProcessingService> logger)
    {
        _logger = logger;
    }
    
    // 处理图像
    public async Task<ImageProcessingResult> ProcessAsync(
        byte[] imageBytes,
        ImageProcessingParams parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImageProcessingResult();
        
        try
        {
            result.ProcessedImage = await ProcessImageInternalAsync(imageBytes, parameters);
            result.Success = true;
            result.Width = 640;
            result.Height = 480;
            result.Metadata = new Dictionary<string, object>
            {
                ["Operation"] = parameters.OperationType.ToString(),
                ["OriginalSize"] = imageBytes.Length
            };
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "图像处理失败");
        }
        
        stopwatch.Stop();
        result.ProcessingTime = stopwatch.Elapsed;
        return result;
    }
    
    // 内部处理方法
    private Task<byte[]> ProcessImageInternalAsync(
        byte[] imageBytes,
        ImageProcessingParams parameters)
    {
        // 在生产环境中，这里会使用OpenCV进行真实的图像处理
        // 目前返回原始图像作为占位符
        return Task.FromResult(imageBytes);
    }
    
    // 图像输入处理
    public async Task<(byte[] Image, Dictionary<string, object> Info)> LoadImageAsync(
        ImageInputParams parameters)
    {
        var info = new Dictionary<string, object>();
        
        try
        {
            byte[] imageBytes;
            
            switch (parameters.SourceType)
            {
                case ImageSourceType.File:
                    imageBytes = await LoadFromFileAsync(parameters.FilePath, info);
                    break;
                    
                case ImageSourceType.URL:
                    imageBytes = await LoadFromUrlAsync(parameters.Url, info);
                    break;
                    
                case ImageSourceType.Base64:
                    imageBytes = LoadFromBase64(parameters.Base64Data, info);
                    break;
                    
                case ImageSourceType.Bytes:
                    imageBytes = parameters.ImageBytes ?? Array.Empty<byte>();
                    info["Source"] = "Direct Bytes";
                    break;
                    
                case ImageSourceType.Camera:
                    imageBytes = await CaptureFromCameraAsync(parameters.CameraIndex, info);
                    break;
                    
                case ImageSourceType.RTSP:
                    imageBytes = await CaptureFromRTSPAsync(parameters.RTSPUrl, info);
                    break;
                    
                default:
                    throw new NotSupportedException(
                        $"不支持的图像源类型: {parameters.SourceType}");
            }
            
            info["Size"] = imageBytes.Length;
            return (imageBytes, info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图像加载失败");
            throw;
        }
    }
    
    // 从文件加载
    private Task<byte[]> LoadFromFileAsync(
        string? filePath,
        Dictionary<string, object> info)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            // 生成一个简单的测试图像
            info["Source"] = "Generated Test Image";
            return Task.FromResult(GenerateTestImage());
        }
        
        info["Source"] = $"File: {Path.GetFileName(filePath)}";
        return Task.FromResult(File.ReadAllBytes(filePath));
    }
    
    // 从URL加载
    private async Task<byte[]> LoadFromUrlAsync(
        string? url,
        Dictionary<string, object> info)
    {
        if (string.IsNullOrEmpty(url))
        {
            info["Source"] = "Generated Test Image";
            return GenerateTestImage();
        }
        
        using var httpClient = new HttpClient();
        var imageBytes = await httpClient.GetByteArrayAsync(url);
        info["Source"] = $"URL: {url}";
        return imageBytes;
    }
    
    // 从Base64加载
    private byte[] LoadFromBase64(
        string? base64Data,
        Dictionary<string, object> info)
    {
        if (string.IsNullOrEmpty(base64Data))
        {
            info["Source"] = "Generated Test Image";
            return GenerateTestImage();
        }
        
        var data = base64Data;
        if (base64Data.Contains(','))
        {
            data = base64Data.Split(',')[1];
        }
        
        var imageBytes = Convert.FromBase64String(data);
        info["Source"] = "Base64 String";
        return imageBytes;
    }
    
    // 从摄像头捕获
    private Task<byte[]> CaptureFromCameraAsync(
        int cameraIndex,
        Dictionary<string, object> info)
    {
        // 在生产环境中会使用OpenCV或其他库访问摄像头
        // 目前返回测试图像
        info["Source"] = $"Camera Index: {cameraIndex}";
        info["Status"] = "Camera Capture Simulated";
        return Task.FromResult(GenerateTestImage());
    }
    
    // 从RTSP流捕获
    private Task<byte[]> CaptureFromRTSPAsync(
        string? rtspUrl,
        Dictionary<string, object> info)
    {
        // 在生产环境中会使用OpenCV处理RTSP流
        info["Source"] = $"RTSP: {rtspUrl ?? "Not Set"}";
        info["Status"] = "RTSP Capture Simulated";
        return Task.FromResult(GenerateTestImage());
    }
    
    // 保存图像
    public async Task<bool> SaveImageAsync(
        byte[] imageBytes,
        ImageOutputParams parameters,
        string? customFilename = null)
    {
        try
        {
            var outputDir = parameters.OutputPath ?? "./output";
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            
            string filename;
            if (parameters.AutoFilename)
            {
                var prefix = parameters.FilenamePrefix ?? "img";
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var extension = GetExtension(parameters.OutputFormat);
                filename = $"{prefix}_{timestamp}{extension}";
            }
            else
            {
                filename = customFilename ?? $"image{GetExtension(parameters.OutputFormat)}";
            }
            
            var fullPath = Path.Combine(outputDir, filename);
            await File.WriteAllBytesAsync(fullPath, imageBytes);
            
            _logger.LogInformation("图像已保存: {Path}", fullPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图像保存失败");
            return false;
        }
    }
    
    // 获取文件扩展名
    private string GetExtension(ImageFormat format)
    {
        return format switch
        {
            ImageFormat.Jpeg => ".jpg",
            ImageFormat.Png => ".png",
            ImageFormat.Bmp => ".bmp",
            ImageFormat.Tiff => ".tiff",
            ImageFormat.WebP => ".webp",
            _ => ".jpg"
        };
    }
    
    // 图像过滤
    public (bool Passed, string Reason, Dictionary<string, object> FilterStats) FilterImage(
        VisionDetectionResult detections,
        Dictionary<string, object> filterProperties)
    {
        var stats = new Dictionary<string, object>();
        var targetClass = filterProperties.GetValueOrDefault("targetClass")?.ToString() ?? "";
        var minConfidence = Convert.ToSingle(
            filterProperties.GetValueOrDefault("minConfidence") ?? 0.8f);
        var minArea = Convert.ToInt64(
            filterProperties.GetValueOrDefault("minArea") ?? 0);
        var maxArea = Convert.ToInt64(
            filterProperties.GetValueOrDefault("maxArea") ?? long.MaxValue);
        var minCount = Convert.ToInt32(
            filterProperties.GetValueOrDefault("minCount") ?? 1);
        var maxCount = Convert.ToInt32(
            filterProperties.GetValueOrDefault("maxCount") ?? int.MaxValue);
        var logicOperator = filterProperties.GetValueOrDefault("logicOperator")?.ToString() ?? "AND";
        
        var matchingDetections = detections.Objects.Where(d =>
        {
            var classMatch = string.IsNullOrEmpty(targetClass) || 
                d.ClassName.Equals(targetClass, StringComparison.OrdinalIgnoreCase);
            var confidenceMatch = d.Confidence >= minConfidence;
            var areaMatch = d.Area >= minArea && d.Area <= maxArea;
            
            return logicOperator switch
            {
                "AND" => classMatch && confidenceMatch && areaMatch,
                "OR" => classMatch || confidenceMatch || areaMatch,
                _ => classMatch && confidenceMatch && areaMatch
            };
        }).ToList();
        
        stats["TotalDetections"] = detections.ObjectCount;
        stats["MatchingDetections"] = matchingDetections.Count;
        
        var countCheck = matchingDetections.Count >= minCount && matchingDetections.Count <= maxCount;
        
        var reason = countCheck switch
        {
            false => $"检测数量不匹配: {matchingDetections.Count} 不在 [{minCount}, {maxCount}] 范围内",
            _ => "通过过滤条件"
        };
        
        return (countCheck, reason, stats);
    }
    
    // 图像预处理
    public async Task<(float[] Tensor, byte[] PreviewImage)> PreprocessImageAsync(
        byte[] imageBytes,
        Dictionary<string, object> preprocessParams)
    {
        var width = Convert.ToInt32(
            preprocessParams.GetValueOrDefault("resizeWidth") ?? 640);
        var height = Convert.ToInt32(
            preprocessParams.GetValueOrDefault("resizeHeight") ?? 640);
        var normalize = Convert.ToBoolean(
            preprocessParams.GetValueOrDefault("normalize") ?? true);
        var channelOrder = preprocessParams.GetValueOrDefault("channelOrder")?.ToString() ?? "RGB";
        var dataFormat = preprocessParams.GetValueOrDefault("dataFormat")?.ToString() ?? "NCHW";
        
        // 生成模拟张量数据
        var tensorSize = 3 * width * height; // 3通道
        var tensor = new float[tensorSize];
        
        // 模拟预处理
        var random = new Random();
        for (int i = 0; i < tensorSize; i++)
        {
            tensor[i] = normalize 
                ? (float)random.NextDouble() * 2 - 1 // [-1, 1]
                : (float)random.Next(0, 256);
        }
        
        return (tensor, imageBytes);
    }
    
    // 生成测试图像
    private byte[] GenerateTestImage()
    {
        // 生成一个简单的图像数据
        // 在生产环境中，这里会创建真实的图像
        return new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
    }
    
    // 获取图像信息
    public Dictionary<string, object> GetImageInfo(byte[] imageBytes)
    {
        var info = new Dictionary<string, object>
        {
            ["Size"] = imageBytes.Length,
            ["Timestamp"] = DateTime.UtcNow
        };
        
        // 在生产环境中，这里会解析真实的图像信息
        info["Width"] = 640;
        info["Height"] = 480;
        info["Channels"] = 3;
        
        return info;
    }
}
