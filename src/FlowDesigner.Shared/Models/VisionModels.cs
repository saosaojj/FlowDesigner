using System;

namespace FlowDesigner.Shared.Models;

// 视觉检测结果
public class VisionDetectionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<DetectedObject> Objects { get; set; } = new();
    public int ObjectCount => Objects.Count;
    public TimeSpan ProcessingTime { get; set; }
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
}

// 检测到的目标对象
public class DetectedObject
{
    public string ClassName { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    
    public float CenterX => X + Width / 2;
    public float CenterY => Y + Height / 2;
    public float Area => Width * Height;
    
    public string ToString() => $"{ClassName} ({Confidence:P1}) at ({X:F1}, {Y:F1})";
}

// YOLO 检测参数
public class YoloDetectionParams
{
    public float ConfidenceThreshold { get; set; } = 0.5f;
    public float IoUThreshold { get; set; } = 0.45f;
    public List<string>? ClassFilter { get; set; }
    public int MaxDetections { get; set; } = 100;
    public bool UseGPU { get; set; } = false;
}

// 图像处理操作类型
public enum ImageOperationType
{
    Resize,
    Crop,
    Rotate,
    Flip,
    Blur,
    Sharpen,
    Grayscale,
    Threshold,
    EdgeDetection,
    Contours,
    ColorSpace,
    HistogramEqualization,
    Morphology
}

// 图像处理参数
public class ImageProcessingParams
{
    public ImageOperationType OperationType { get; set; }
    
    // Resize 参数
    public int TargetWidth { get; set; } = 640;
    public int TargetHeight { get; set; } = 480;
    public bool KeepAspectRatio { get; set; } = true;
    
    // Crop 参数
    public int CropX { get; set; }
    public int CropY { get; set; }
    public int CropWidth { get; set; }
    public int CropHeight { get; set; }
    
    // Rotate 参数
    public float RotationAngle { get; set; }
    public bool RotateExpand { get; set; } = true;
    
    // Flip 参数
    public int FlipCode { get; set; } // 0: 垂直, 1: 水平, -1: 两者
    
    // Blur 参数
    public int BlurKernelSize { get; set; } = 5;
    public float BlurSigmaX { get; set; } = 0;
    
    // Sharpen 参数
    public float SharpenStrength { get; set; } = 1.0f;
    
    // Threshold 参数
    public float ThresholdValue { get; set; } = 127;
    public float MaxThresholdValue { get; set; } = 255;
    public int ThresholdType { get; set; }
    
    // Edge Detection 参数
    public float CannyThreshold1 { get; set; } = 100;
    public float CannyThreshold2 { get; set; } = 200;
    
    // Contours 参数
    public int ContourRetrievalMode { get; set; } = 2;
    public int ContourApproximationMethod { get; set; } = 1;
    
    // Color Space 参数
    public int ColorConversionCode { get; set; } // BGR2GRAY=6, BGR2RGB=4, BGR2HSV=40
    
    // Morphology 参数
    public int MorphologyOperation { get; set; } = 2; // Erode=0, Dilate=1, Open=2, Close=3
    public int MorphologyKernelSize { get; set; } = 5;
    public int MorphologyIterations { get; set; } = 1;
}

// 图像处理结果
public class ImageProcessingResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public byte[]? ProcessedImage { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    
    // 附加数据
    public Dictionary<string, object> Metadata { get; set; } = new();
}

// 图像输入源类型
public enum ImageSourceType
{
    File,
    URL,
    Base64,
    Bytes,
    Camera,
    RTSP
}

// 图像输入参数
public class ImageInputParams
{
    public ImageSourceType SourceType { get; set; }
    public string? FilePath { get; set; }
    public string? Url { get; set; }
    public string? Base64Data { get; set; }
    public byte[]? ImageBytes { get; set; }
    
    // Camera/RTSP 专用
    public int CameraIndex { get; set; } = 0;
    public string? RTSPUrl { get; set; }
    public int CaptureIntervalMs { get; set; } = 1000;
    public bool AutoCapture { get; set; } = true;
}

// 图像输出参数
public class ImageOutputParams
{
    public string? OutputPath { get; set; }
    public ImageFormat OutputFormat { get; set; } = ImageFormat.Jpeg;
    public int Quality { get; set; } = 85;
    public bool DrawDetections { get; set; } = true;
    public bool SaveToFile { get; set; } = false;
    public bool ReturnBase64 { get; set; } = true;
}

// 图像格式
public enum ImageFormat
{
    Jpeg,
    Png,
    Bmp,
    Tiff,
    WebP
}

// YOLO 模型配置
public class YoloModelConfig
{
    public string ModelPath { get; set; } = "yolov8n.onnx";
    public string ModelName { get; set; } = "YOLOv8n";
    public int InputWidth { get; set; } = 640;
    public int InputHeight { get; set; } = 640;
    public int NumClasses { get; set; } = 80;
    public List<string> ClassNames { get; set; } = new();
    
    public static YoloModelConfig Default => new()
    {
        ClassNames = new List<string>
        {
            "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
            "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat",
            "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella",
            "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite",
            "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
            "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich",
            "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch",
            "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse", "remote",
            "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator",
            "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
        }
    };
}
