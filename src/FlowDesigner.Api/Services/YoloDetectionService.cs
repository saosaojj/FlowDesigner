using System.Diagnostics;
using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public class YoloDetectionService
{
    private readonly ILogger<YoloDetectionService> _logger;
    private readonly Dictionary<string, YoloModelConfig> _loadedModels;
    private readonly Random _random;
    
    public YoloDetectionService(ILogger<YoloDetectionService> logger)
    {
        _logger = logger;
        _loadedModels = new Dictionary<string, YoloModelConfig>();
        _random = new Random();
        
        // 预加载默认模型配置
        _loadedModels["default"] = YoloModelConfig.Default;
        _logger.LogInformation("YOLO检测服务初始化完成");
    }
    
    // 模拟YOLO检测（生产环境会调用真实的YOLO模型）
    public async Task<VisionDetectionResult> DetectAsync(
        byte[] imageBytes,
        YoloDetectionParams? detectionParams = null,
        string modelKey = "default")
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new VisionDetectionResult();
        
        try
        {
            if (!_loadedModels.TryGetValue(modelKey, out var config))
            {
                config = YoloModelConfig.Default;
            }
            
            var yoloParams = detectionParams ?? new YoloDetectionParams();
            
            // 模拟检测（实际项目中这里会调用真实的YOLO推理）
            result.Objects = await SimulateDetectionAsync(config, yoloParams);
            result.Success = true;
            result.OriginalWidth = 640;
            result.OriginalHeight = 480;
            
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            
            _logger.LogInformation(
                "YOLO检测完成: 检测到 {Count} 个目标, 耗时: {Time:F1}ms",
                result.ObjectCount, result.ProcessingTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "YOLO检测失败");
        }
        
        return result;
    }
    
    // 绘制检测框（模拟）
    public byte[] DrawDetections(
        byte[] imageBytes,
        List<DetectedObject> detections,
        bool showConfidence = true)
    {
        // 在生产环境中，这里会使用OpenCV或System.Drawing绘制检测框
        // 目前返回原始图像作为占位符
        return imageBytes;
    }
    
    // 获取分类颜色
    public (byte R, byte G, byte B) GetClassColor(string className)
    {
        var hash = className.GetHashCode();
        var hue = (hash % 360 + 360) % 360;
        return HsvToRgb(hue, 0.7f, 0.9f);
    }
    
    // HSV到RGB转换
    private (byte R, byte G, byte B) HsvToRgb(float h, float s, float v)
    {
        var hi = (int)(h / 60f) % 6;
        var f = h / 60f - (int)(h / 60f);
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);
        
        return hi switch
        {
            0 => ((byte)(v * 255), (byte)(t * 255), (byte)(p * 255)),
            1 => ((byte)(q * 255), (byte)(v * 255), (byte)(p * 255)),
            2 => ((byte)(p * 255), (byte)(v * 255), (byte)(t * 255)),
            3 => ((byte)(p * 255), (byte)(q * 255), (byte)(v * 255)),
            4 => ((byte)(t * 255), (byte)(p * 255), (byte)(v * 255)),
            _ => ((byte)(v * 255), (byte)(p * 255), (byte)(q * 255))
        };
    }
    
    // 模拟检测结果（生产环境会替换为真实模型推理）
    private Task<List<DetectedObject>> SimulateDetectionAsync(
        YoloModelConfig config,
        YoloDetectionParams yoloParams)
    {
        var detections = new List<DetectedObject>();
        
        // 随机生成一些检测结果用于演示
        var numDetections = _random.Next(1, 10);
        
        for (int i = 0; i < numDetections; i++)
        {
            var classIndex = _random.Next(config.ClassNames.Count);
            var confidence = (float)(yoloParams.ConfidenceThreshold +
                (1 - yoloParams.ConfidenceThreshold) * _random.NextDouble());
            
            // 过滤类别
            if (yoloParams.ClassFilter != null && yoloParams.ClassFilter.Count > 0)
            {
                if (!yoloParams.ClassFilter.Contains(config.ClassNames[classIndex]))
                    continue;
            }
            
            if (confidence >= yoloParams.ConfidenceThreshold)
            {
                detections.Add(new DetectedObject
                {
                    ClassName = config.ClassNames[classIndex],
                    Confidence = confidence,
                    X = _random.Next(0, 500),
                    Y = _random.Next(0, 400),
                    Width = _random.Next(50, 200),
                    Height = _random.Next(50, 200)
                });
            }
        }
        
        // 限制最大检测数
        if (detections.Count > yoloParams.MaxDetections)
        {
            detections = detections.OrderByDescending(d => d.Confidence)
                .Take(yoloParams.MaxDetections)
                .ToList();
        }
        
        return Task.FromResult(detections);
    }
    
    // 计算IoU（交并比）
    public float CalculateIoU(DetectedObject a, DetectedObject b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
        
        var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var union = a.Area + b.Area - intersection;
        
        return union > 0 ? (float)intersection / union : 0;
    }
    
    // NMS（非极大值抑制）
    public List<DetectedObject> Nms(List<DetectedObject> detections, float iouThreshold)
    {
        var result = new List<DetectedObject>();
        var sortedDetections = detections.OrderByDescending(d => d.Confidence).ToList();
        
        while (sortedDetections.Count > 0)
        {
            var current = sortedDetections[0];
            result.Add(current);
            sortedDetections.RemoveAt(0);
            
            sortedDetections = sortedDetections
                .Where(d => CalculateIoU(current, d) < iouThreshold)
                .ToList();
        }
        
        return result;
    }
    
    // 参数微调
    public async Task<YoloDetectionParams> FineTuneParamsAsync(
        string modelKey,
        List<(byte[] Image, List<DetectedObject> GroundTruth)> validationData,
        YoloDetectionParams? startParams = null)
    {
        _logger.LogInformation("开始参数微调...");
        
        var bestParams = startParams ?? new YoloDetectionParams();
        var bestF1 = 0f;
        
        // 简单网格搜索
        for (float conf = 0.3f; conf <= 0.8f; conf += 0.05f)
        {
            for (float iou = 0.3f; iou <= 0.6f; iou += 0.05f)
            {
                var testParams = new YoloDetectionParams
                {
                    ConfidenceThreshold = conf,
                    IoUThreshold = iou,
                    MaxDetections = bestParams.MaxDetections
                };
                
                var currentF1 = await EvaluateParamsAsync(testParams, validationData, modelKey);
                
                if (currentF1 > bestF1)
                {
                    bestF1 = currentF1;
                    bestParams = testParams;
                    _logger.LogInformation(
                        "找到更好参数: Conf={Conf:F2}, IoU={IoU:F2}, F1={F1:F3}",
                        conf, iou, currentF1);
                }
            }
        }
        
        _logger.LogInformation(
            "参数微调完成, 最佳F1分数: {F1:F3}", bestF1);
        
        return bestParams;
    }
    
    // 评估参数
    private async Task<float> EvaluateParamsAsync(
        YoloDetectionParams testParams,
        List<(byte[] Image, List<DetectedObject> GroundTruth)> validationData,
        string modelKey)
    {
        var totalPrecision = 0f;
        var totalRecall = 0f;
        var count = 0;
        
        foreach (var (image, groundTruth) in validationData)
        {
            var result = await DetectAsync(image, testParams, modelKey);
            
            if (result.Success)
            {
                var (precision, recall) = CalculateMetrics(result.Objects, groundTruth, 0.5f);
                totalPrecision += precision;
                totalRecall += recall;
                count++;
            }
        }
        
        if (count == 0) return 0;
        
        var avgPrecision = totalPrecision / count;
        var avgRecall = totalRecall / count;
        
        return 2 * avgPrecision * avgRecall / (avgPrecision + avgRecall + 1e-6f);
    }
    
    // 计算Precision和Recall
    private (float Precision, float Recall) CalculateMetrics(
        List<DetectedObject> predictions,
        List<DetectedObject> groundTruth,
        float iouThreshold)
    {
        var truePositives = 0;
        var matched = new bool[groundTruth.Count];
        
        foreach (var pred in predictions)
        {
            for (int i = 0; i < groundTruth.Count; i++)
            {
                if (!matched[i] && pred.ClassName == groundTruth[i].ClassName)
                {
                    var iou = CalculateIoU(pred, groundTruth[i]);
                    if (iou >= iouThreshold)
                    {
                        matched[i] = true;
                        truePositives++;
                        break;
                    }
                }
            }
        }
        
        var precision = predictions.Count > 0 ? (float)truePositives / predictions.Count : 0;
        var recall = groundTruth.Count > 0 ? (float)truePositives / groundTruth.Count : 0;
        
        return (precision, recall);
    }
}
