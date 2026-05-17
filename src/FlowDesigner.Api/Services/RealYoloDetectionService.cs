using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using FlowDesigner.Shared.Models;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Size = System.Drawing.Size;

namespace FlowDesigner.Api.Services;

public class RealYoloDetectionService : IDisposable
{
    private readonly ILogger<RealYoloDetectionService> _logger;
    private readonly Dictionary<string, InferenceSession> _modelSessions;
    private readonly object _lockObject = new();
    private bool _disposed;

    public RealYoloDetectionService(ILogger<RealYoloDetectionService> logger)
    {
        _logger = logger;
        _modelSessions = new Dictionary<string, InferenceSession>();
        InitializeModels();
    }

    private void InitializeModels()
    {
        try
        {
            var modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "yolov8n.onnx");
            if (File.Exists(modelPath))
            {
                LoadModel(modelPath, "default");
            }
            else
            {
                _logger.LogWarning("YOLO 模型文件未找到: {Path}, 将使用模拟模式", modelPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化 YOLO 模型时出错");
        }
    }

    public async Task<bool> LoadModel(string modelPath, string modelId = "default")
    {
        try
        {
            if (!File.Exists(modelPath))
            {
                _logger.LogError("模型文件不存在: {Path}", modelPath);
                return false;
            }

            var sessionOptions = new SessionOptions();
            
            // 尝试使用 GPU
            try
            {
                sessionOptions.AppendExecutionProvider_CUDA(0);
                _logger.LogInformation("使用 CUDA GPU 进行推理");
            }
            catch
            {
                // 如果 GPU 不可用，使用 CPU
                sessionOptions.AppendExecutionProvider_CPU();
                _logger.LogInformation("使用 CPU 进行推理");
            }

            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            sessionOptions.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;

            var session = new InferenceSession(modelPath, sessionOptions);

            lock (_lockObject)
            {
                if (_modelSessions.ContainsKey(modelId))
                {
                    _modelSessions[modelId].Dispose();
                }
                _modelSessions[modelId] = session;
            }

            _logger.LogInformation("模型加载成功: {Id}, {Path}", modelId, modelPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载模型失败: {Path}", modelPath);
            return false;
        }
    }

    public async Task<VisionDetectionResult> DetectAsync(
        byte[] imageBytes,
        YoloDetectionParams? parameters = null,
        string modelId = "default")
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new VisionDetectionResult();

        try
        {
            parameters ??= new YoloDetectionParams();

            // 检查模型是否加载
            if (!_modelSessions.ContainsKey(modelId))
            {
                // 回退到模拟模式
                _logger.LogWarning("模型未加载，使用模拟模式: {Id}", modelId);
                return await SimulateDetectionAsync(parameters);
            }

            // 加载图像
            using var image = Image.Load<Rgb24>(imageBytes);
            result.OriginalWidth = image.Width;
            result.OriginalHeight = image.Height;

            // 预处理图像
            var inputTensor = PreprocessImage(image, parameters);
            
            // 进行推理
            var detections = RunInference(inputTensor, parameters, modelId);
            
            // 后处理
            result.Objects = PostProcessDetections(
                detections,
                image.Width,
                image.Height,
                parameters);

            result.Success = true;
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;

            _logger.LogDebug(
                "检测完成: {Count} 个目标, 耗时: {Time:F2}ms",
                result.ObjectCount,
                result.ProcessingTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "YOLO 检测失败");
        }

        return result;
    }

    private DenseTensor<float> PreprocessImage(Image<Rgb24> image, YoloDetectionParams parameters)
    {
        const int modelInputSize = 640;

        // 调整图像大小
        var resized = image.Clone(x => x.Resize(modelInputSize, modelInputSize));

        // 创建张量
        var tensor = new DenseTensor<float>(new[] { 1, 3, modelInputSize, modelInputSize });

        // 归一化并填充张量
        resized.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < modelInputSize; y++)
            {
                var rowSpan = accessor.GetRowSpan(y);
                for (int x = 0; x < modelInputSize; x++)
                {
                    var pixel = rowSpan[x];
                    tensor[0, 0, y, x] = pixel.R / 255.0f;
                    tensor[0, 1, y, x] = pixel.G / 255.0f;
                    tensor[0, 2, y, x] = pixel.B / 255.0f;
                }
            }
        });

        return tensor;
    }

    private List<DetectedObject> RunInference(
        DenseTensor<float> inputTensor,
        YoloDetectionParams parameters,
        string modelId)
    {
        var detections = new List<DetectedObject>();

        if (!_modelSessions.TryGetValue(modelId, out var session))
        {
            return detections;
        }

        // 创建输入数据
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", inputTensor)
        };

        // 运行推理
        using var results = session.Run(inputs);
        
        // 处理输出
        var output = results.FirstOrDefault();
        if (output != null)
        {
            var outputTensor = output.Value as DenseTensor<float>;
            if (outputTensor != null)
            {
                detections = ParseOutput(outputTensor, parameters);
            }
        }

        return detections;
    }

    private List<DetectedObject> ParseOutput(DenseTensor<float> outputTensor, YoloDetectionParams parameters)
    {
        var detections = new List<DetectedObject>();
        
        // YOLOv8 输出格式: [1, 84, 8400] - [batch, (4 + 80 classes), anchors]
        // 前4个值是边界框，接下来是80个类别置信度
        
        var classNames = GetDefaultClassNames();
        var batchSize = outputTensor.Dimensions[0];
        var numClasses = outputTensor.Dimensions[1] - 4;
        var numAnchors = outputTensor.Dimensions[2];

        for (int i = 0; i < numAnchors; i++)
        {
            float maxConfidence = 0;
            int bestClassId = -1;

            // 找到置信度最高的类别
            for (int cls = 0; cls < numClasses; cls++)
            {
                var confidence = outputTensor[0, cls + 4, i];
                if (confidence > maxConfidence && confidence >= parameters.ConfidenceThreshold)
                {
                    maxConfidence = confidence;
                    bestClassId = cls;
                }
            }

            if (bestClassId == -1) continue;

            // 获取边界框
            var x = outputTensor[0, 0, i];
            var y = outputTensor[0, 1, i];
            var w = outputTensor[0, 2, i];
            var h = outputTensor[0, 3, i];

            // 转换坐标（中心点到左上角）
            var x1 = x - w / 2;
            var y1 = y - h / 2;

            // 检查类别过滤
            if (parameters.ClassFilter != null && parameters.ClassFilter.Count > 0)
            {
                if (bestClassId >= classNames.Count || 
                    !parameters.ClassFilter.Contains(classNames[bestClassId]))
                {
                    continue;
                }
            }

            detections.Add(new DetectedObject
            {
                ClassName = bestClassId < classNames.Count ? classNames[bestClassId] : $"class_{bestClassId}",
                Confidence = maxConfidence,
                X = x1,
                Y = y1,
                Width = w,
                Height = h
            });
        }

        // 应用 NMS
        if (detections.Count > 0)
        {
            detections = ApplyNms(detections, parameters.IoUThreshold);
        }

        // 限制最大检测数
        return detections
            .OrderByDescending(d => d.Confidence)
            .Take(parameters.MaxDetections)
            .ToList();
    }

    private List<DetectedObject> PostProcessDetections(
        List<DetectedObject> detections,
        int originalWidth,
        int originalHeight,
        YoloDetectionParams parameters)
    {
        const int modelInputSize = 640;
        
        // 计算缩放比例
        var scaleX = (float)originalWidth / modelInputSize;
        var scaleY = (float)originalHeight / modelInputSize;

        // 转换坐标回原始尺寸
        return detections.Select(d => new DetectedObject
        {
            ClassName = d.ClassName,
            Confidence = d.Confidence,
            X = d.X * scaleX,
            Y = d.Y * scaleY,
            Width = d.Width * scaleX,
            Height = d.Height * scaleY
        }).ToList();
    }

    private List<DetectedObject> ApplyNms(List<DetectedObject> detections, float iouThreshold)
    {
        var result = new List<DetectedObject>();
        var sorted = detections.OrderByDescending(d => d.Confidence).ToList();

        while (sorted.Count > 0)
        {
            var current = sorted[0];
            result.Add(current);
            sorted.RemoveAt(0);

            // 移除重叠度高的检测
            sorted = sorted.Where(d =>
            {
                if (d.ClassName != current.ClassName) return true;
                var iou = CalculateIoU(current, d);
                return iou < iouThreshold;
            }).ToList();
        }

        return result;
    }

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

    public byte[] DrawDetections(byte[] imageBytes, List<DetectedObject> detections, bool showConfidence = true)
    {
        try
        {
            using var image = Image.Load<Rgb24>(imageBytes);
            var colorDict = GetClassColors();

            foreach (var detection in detections)
            {
                var color = colorDict.TryGetValue(detection.ClassName, out var c) ? c : new Rgb24(255, 0, 0);
                
                // 绘制边框
                image.Mutate(x => x.DrawPolygon(
                    Color.FromRgb(color.R, color.G, color.B),
                    2f,
                    new SixLabors.ImageSharp.Drawing.PointF(
                        detection.X,
                        detection.Y),
                    new SixLabors.ImageSharp.Drawing.PointF(
                        detection.X + detection.Width,
                        detection.Y),
                    new SixLabors.ImageSharp.Drawing.PointF(
                        detection.X + detection.Width,
                        detection.Y + detection.Height),
                    new SixLabors.ImageSharp.Drawing.PointF(
                        detection.X,
                        detection.Y + detection.Height)));

                if (showConfidence)
                {
                    // 绘制标签
                    var label = $"{detection.ClassName}: {detection.Confidence:P1}";
                    // 这里可以添加文本绘制
                }
            }

            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "绘制检测框失败");
            return imageBytes;
        }
    }

    private Dictionary<string, Rgb24> GetClassColors()
    {
        var colors = new Dictionary<string, Rgb24>();
        var colorList = new[]
        {
            new Rgb24(255, 0, 0), new Rgb24(0, 255, 0), new Rgb24(0, 0, 255),
            new Rgb24(255, 255, 0), new Rgb24(255, 0, 255), new Rgb24(0, 255, 255),
            new Rgb24(128, 0, 0), new Rgb24(0, 128, 0), new Rgb24(0, 0, 128),
            new Rgb24(128, 128, 0), new Rgb24(128, 0, 128), new Rgb24(0, 128, 128)
        };

        var classNames = GetDefaultClassNames();
        for (int i = 0; i < classNames.Count; i++)
        {
            colors[classNames[i]] = colorList[i % colorList.Length];
        }

        return colors;
    }

    private List<string> GetDefaultClassNames()
    {
        return YoloModelConfig.Default.ClassNames;
    }

    private async Task<VisionDetectionResult> SimulateDetectionAsync(YoloDetectionParams parameters)
    {
        await Task.Delay(50);
        var random = new Random();
        var detections = new List<DetectedObject>();
        var classNames = GetDefaultClassNames();

        var count = random.Next(1, 5);
        for (int i = 0; i < count; i++)
        {
            var classId = random.Next(classNames.Count);
            detections.Add(new DetectedObject
            {
                ClassName = classNames[classId],
                Confidence = parameters.ConfidenceThreshold + (float)random.NextDouble() * (1 - parameters.ConfidenceThreshold),
                X = random.Next(100, 400),
                Y = random.Next(100, 300),
                Width = random.Next(50, 150),
                Height = random.Next(50, 150)
            });
        }

        return new VisionDetectionResult
        {
            Success = true,
            Objects = detections,
            ProcessingTime = TimeSpan.FromMilliseconds(50),
            OriginalWidth = 640,
            OriginalHeight = 480
        };
    }

    public async Task<YoloDetectionParams> FineTuneParamsAsync(
        string modelId,
        List<(byte[] Image, List<DetectedObject> GroundTruth)> validationData,
        YoloDetectionParams? startParams = null)
    {
        var bestParams = startParams ?? new YoloDetectionParams();
        var bestF1 = 0.0f;

        _logger.LogInformation("开始参数微调，验证集大小: {Count}", validationData.Count);

        // 网格搜索
        for (float conf = 0.3f; conf <= 0.8f; conf += 0.1f)
        {
            for (float iou = 0.3f; iou <= 0.7f; iou += 0.1f)
            {
                var testParams = new YoloDetectionParams
                {
                    ConfidenceThreshold = conf,
                    IoUThreshold = iou,
                    MaxDetections = bestParams.MaxDetections,
                    UseGPU = bestParams.UseGPU,
                    ClassFilter = bestParams.ClassFilter
                };

                var f1 = await EvaluateParamsAsync(testParams, validationData, modelId);
                
                if (f1 > bestF1)
                {
                    bestF1 = f1;
                    bestParams = testParams;
                    _logger.LogInformation(
                        "找到更好参数: Conf={Conf:F2}, IoU={IoU:F2}, F1={F1:F4}",
                        conf, iou, f1);
                }
            }
        }

        _logger.LogInformation("参数微调完成，最佳 F1: {F1:F4}", bestF1);
        return bestParams;
    }

    private async Task<float> EvaluateParamsAsync(
        YoloDetectionParams testParams,
        List<(byte[] Image, List<DetectedObject> GroundTruth)> validationData,
        string modelId)
    {
        float totalPrecision = 0, totalRecall = 0;
        int count = 0;

        foreach (var (image, groundTruth) in validationData)
        {
            var result = await DetectAsync(image, testParams, modelId);
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
        return 2 * avgPrecision * avgRecall / (avgPrecision + avgRecall + 0.0001f);
    }

    private (float Precision, float Recall) CalculateMetrics(
        List<DetectedObject> predictions,
        List<DetectedObject> groundTruth,
        float iouThreshold)
    {
        var matched = new bool[groundTruth.Count];
        var truePositives = 0;

        foreach (var pred in predictions.OrderByDescending(p => p.Confidence))
        {
            for (int i = 0; i < groundTruth.Count; i++)
            {
                if (matched[i]) continue;
                if (pred.ClassName != groundTruth[i].ClassName) continue;

                var iou = CalculateIoU(pred, groundTruth[i]);
                if (iou >= iouThreshold)
                {
                    matched[i] = true;
                    truePositives++;
                    break;
                }
            }
        }

        var precision = predictions.Count > 0 ? (float)truePositives / predictions.Count : 0;
        var recall = groundTruth.Count > 0 ? (float)truePositives / groundTruth.Count : 0;

        return (precision, recall);
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
            lock (_lockObject)
            {
                foreach (var session in _modelSessions.Values)
                {
                    session.Dispose();
                }
                _modelSessions.Clear();
            }
        }

        _disposed = true;
    }
}
