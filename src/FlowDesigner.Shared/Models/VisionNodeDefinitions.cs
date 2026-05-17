namespace FlowDesigner.Shared.Models;

public static class VisionNodeDefinitions
{
    public static List<NodeDefinition> GetAllVisionNodes()
    {
        return new List<NodeDefinition>
        {
            // 图像输入节点
            new NodeDefinition
            {
                Type = "image-input",
                Category = "vision",
                Name = "图像输入",
                Description = "从文件、URL、摄像头或RTSP流获取图像",
                Color = "#0ea5e9",
                Icon = "fa-image",
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "image", Type = "image", Label = "图像输出" },
                    new PortDefinition { Name = "info", Type = "object", Label = "信息输出" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["sourceType"] = new PropertyDefinition { Type = "select", Label = "输入源类型", DefaultValue = "File" },
                    ["filePath"] = new PropertyDefinition { Type = "string", Label = "文件路径", DefaultValue = "" },
                    ["url"] = new PropertyDefinition { Type = "string", Label = "URL地址", DefaultValue = "" },
                    ["base64Data"] = new PropertyDefinition { Type = "textarea", Label = "Base64数据", DefaultValue = "" },
                    ["cameraIndex"] = new PropertyDefinition { Type = "number", Label = "摄像头索引", DefaultValue = 0 },
                    ["rtspUrl"] = new PropertyDefinition { Type = "string", Label = "RTSP流地址", DefaultValue = "" },
                    ["captureInterval"] = new PropertyDefinition { Type = "number", Label = "捕获间隔(ms)", DefaultValue = 1000 },
                    ["autoCapture"] = new PropertyDefinition { Type = "boolean", Label = "自动捕获", DefaultValue = true },
                    ["loopCount"] = new PropertyDefinition { Type = "number", Label = "循环次数", DefaultValue = 0 }
                }
            },

            // YOLO 检测节点
            new NodeDefinition
            {
                Type = "yolo-detect",
                Category = "vision",
                Name = "YOLO检测",
                Description = "使用YOLO模型进行目标检测",
                Color = "#8b5cf6",
                Icon = "fa-search",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "image", Type = "image", Label = "图像输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "detections", Type = "object", Label = "检测结果" },
                    new PortDefinition { Name = "image", Type = "image", Label = "可视化图像" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["modelPath"] = new PropertyDefinition { Type = "string", Label = "模型路径", DefaultValue = "yolov8n.onnx" },
                    ["confidence"] = new PropertyDefinition { Type = "number", Label = "置信度阈值", DefaultValue = 0.5f },
                    ["iouThreshold"] = new PropertyDefinition { Type = "number", Label = "IoU阈值", DefaultValue = 0.45f },
                    ["maxDetections"] = new PropertyDefinition { Type = "number", Label = "最大检测数", DefaultValue = 100 },
                    ["classFilter"] = new PropertyDefinition { Type = "string", Label = "类别过滤(逗号分隔)", DefaultValue = "" },
                    ["useGPU"] = new PropertyDefinition { Type = "boolean", Label = "使用GPU加速", DefaultValue = false },
                    ["drawDetections"] = new PropertyDefinition { Type = "boolean", Label = "绘制检测框", DefaultValue = true },
                    ["showConfidence"] = new PropertyDefinition { Type = "boolean", Label = "显示置信度", DefaultValue = true },
                    ["lineThickness"] = new PropertyDefinition { Type = "number", Label = "线条粗细", DefaultValue = 2 },
                    ["fontScale"] = new PropertyDefinition { Type = "number", Label = "字体大小", DefaultValue = 0.5f }
                }
            },

            // 图像处理节点
            new NodeDefinition
            {
                Type = "image-process",
                Category = "vision",
                Name = "图像处理",
                Description = "使用OpenCV进行图像处理操作",
                Color = "#f59e0b",
                Icon = "fa-magic",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "image", Type = "image", Label = "图像输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "image", Type = "image", Label = "处理后图像" },
                    new PortDefinition { Name = "metadata", Type = "object", Label = "元数据" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["operation"] = new PropertyDefinition { Type = "select", Label = "处理操作", DefaultValue = "Resize" },
                    ["targetWidth"] = new PropertyDefinition { Type = "number", Label = "目标宽度", DefaultValue = 640 },
                    ["targetHeight"] = new PropertyDefinition { Type = "number", Label = "目标高度", DefaultValue = 480 },
                    ["keepAspectRatio"] = new PropertyDefinition { Type = "boolean", Label = "保持宽高比", DefaultValue = true },
                    ["cropX"] = new PropertyDefinition { Type = "number", Label = "裁剪X坐标", DefaultValue = 0 },
                    ["cropY"] = new PropertyDefinition { Type = "number", Label = "裁剪Y坐标", DefaultValue = 0 },
                    ["cropWidth"] = new PropertyDefinition { Type = "number", Label = "裁剪宽度", DefaultValue = 320 },
                    ["cropHeight"] = new PropertyDefinition { Type = "number", Label = "裁剪高度", DefaultValue = 240 },
                    ["rotationAngle"] = new PropertyDefinition { Type = "number", Label = "旋转角度", DefaultValue = 90 },
                    ["blurKernelSize"] = new PropertyDefinition { Type = "number", Label = "模糊核大小", DefaultValue = 5 },
                    ["thresholdValue"] = new PropertyDefinition { Type = "number", Label = "阈值", DefaultValue = 127 },
                    ["cannyThreshold1"] = new PropertyDefinition { Type = "number", Label = "Canny阈值1", DefaultValue = 100 },
                    ["cannyThreshold2"] = new PropertyDefinition { Type = "number", Label = "Canny阈值2", DefaultValue = 200 },
                    ["colorConversion"] = new PropertyDefinition { Type = "select", Label = "颜色空间转换", DefaultValue = "BGR2GRAY" },
                    ["morphologyOperation"] = new PropertyDefinition { Type = "select", Label = "形态学操作", DefaultValue = "Open" },
                    ["morphologyKernel"] = new PropertyDefinition { Type = "number", Label = "形态学核大小", DefaultValue = 5 }
                }
            },

            // 图像输出节点
            new NodeDefinition
            {
                Type = "image-output",
                Category = "vision",
                Name = "图像输出",
                Description = "保存或显示处理后的图像",
                Color = "#10b981",
                Icon = "fa-save",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "image", Type = "image", Label = "图像输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "success", Type = "boolean", Label = "成功状态" },
                    new PortDefinition { Name = "path", Type = "string", Label = "输出路径" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["outputPath"] = new PropertyDefinition { Type = "string", Label = "输出路径", DefaultValue = "./output" },
                    ["outputFormat"] = new PropertyDefinition { Type = "select", Label = "输出格式", DefaultValue = "Jpeg" },
                    ["quality"] = new PropertyDefinition { Type = "number", Label = "输出质量(0-100)", DefaultValue = 85 },
                    ["saveToFile"] = new PropertyDefinition { Type = "boolean", Label = "保存到文件", DefaultValue = true },
                    ["returnBase64"] = new PropertyDefinition { Type = "boolean", Label = "返回Base64", DefaultValue = true },
                    ["autoFilename"] = new PropertyDefinition { Type = "boolean", Label = "自动命名", DefaultValue = true },
                    ["filenamePrefix"] = new PropertyDefinition { Type = "string", Label = "文件名前缀", DefaultValue = "img" }
                }
            },

            // 图像过滤节点
            new NodeDefinition
            {
                Type = "image-filter",
                Category = "vision",
                Name = "图像过滤",
                Description = "根据检测结果过滤和筛选图像",
                Color = "#ef4444",
                Icon = "fa-filter",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "image", Type = "image", Label = "图像输入" },
                    new PortDefinition { Name = "detections", Type = "object", Label = "检测结果" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "pass", Type = "image", Label = "通过图像" },
                    new PortDefinition { Name = "reject", Type = "image", Label = "拒绝图像" },
                    new PortDefinition { Name = "info", Type = "object", Label = "过滤信息" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["filterType"] = new PropertyDefinition { Type = "select", Label = "过滤类型", DefaultValue = "Class" },
                    ["targetClass"] = new PropertyDefinition { Type = "string", Label = "目标类别", DefaultValue = "person" },
                    ["minConfidence"] = new PropertyDefinition { Type = "number", Label = "最小置信度", DefaultValue = 0.8f },
                    ["minArea"] = new PropertyDefinition { Type = "number", Label = "最小面积", DefaultValue = 0 },
                    ["maxArea"] = new PropertyDefinition { Type = "number", Label = "最大面积", DefaultValue = 999999999 },
                    ["minCount"] = new PropertyDefinition { Type = "number", Label = "最小数量", DefaultValue = 1 },
                    ["maxCount"] = new PropertyDefinition { Type = "number", Label = "最大数量", DefaultValue = 9999 },
                    ["logicOperator"] = new PropertyDefinition { Type = "select", Label = "逻辑操作", DefaultValue = "AND" }
                }
            },

            // 参数微调节点
            new NodeDefinition
            {
                Type = "yolo-finetune",
                Category = "vision",
                Name = "参数微调",
                Description = "动态调整YOLO检测参数",
                Color = "#ec4899",
                Icon = "fa-sliders-h",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "trigger", Type = "any", Label = "触发信号" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "params", Type = "object", Label = "参数输出" },
                    new PortDefinition { Name = "success", Type = "boolean", Label = "成功状态" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["confidenceStart"] = new PropertyDefinition { Type = "number", Label = "起始置信度", DefaultValue = 0.3f },
                    ["confidenceEnd"] = new PropertyDefinition { Type = "number", Label = "结束置信度", DefaultValue = 0.8f },
                    ["confidenceStep"] = new PropertyDefinition { Type = "number", Label = "置信度步长", DefaultValue = 0.05f },
                    ["iouStart"] = new PropertyDefinition { Type = "number", Label = "起始IoU", DefaultValue = 0.3f },
                    ["iouEnd"] = new PropertyDefinition { Type = "number", Label = "结束IoU", DefaultValue = 0.6f },
                    ["iouStep"] = new PropertyDefinition { Type = "number", Label = "IoU步长", DefaultValue = 0.05f },
                    ["optimizeMetric"] = new PropertyDefinition { Type = "select", Label = "优化指标", DefaultValue = "Precision" },
                    ["maxIterations"] = new PropertyDefinition { Type = "number", Label = "最大迭代次数", DefaultValue = 100 },
                    ["autoSave"] = new PropertyDefinition { Type = "boolean", Label = "自动保存最优参数", DefaultValue = true }
                }
            },

            // 视频分析节点
            new NodeDefinition
            {
                Type = "video-analyze",
                Category = "vision",
                Name = "视频分析",
                Description = "对视频流或文件进行持续分析",
                Color = "#06b6d4",
                Icon = "fa-video",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "control", Type = "any", Label = "控制信号" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "frame", Type = "image", Label = "当前帧" },
                    new PortDefinition { Name = "detections", Type = "object", Label = "检测结果" },
                    new PortDefinition { Name = "stats", Type = "object", Label = "统计信息" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["sourceType"] = new PropertyDefinition { Type = "select", Label = "视频源类型", DefaultValue = "File" },
                    ["videoPath"] = new PropertyDefinition { Type = "string", Label = "视频文件路径", DefaultValue = "" },
                    ["rtspUrl"] = new PropertyDefinition { Type = "string", Label = "RTSP流地址", DefaultValue = "" },
                    ["cameraIndex"] = new PropertyDefinition { Type = "number", Label = "摄像头索引", DefaultValue = 0 },
                    ["frameInterval"] = new PropertyDefinition { Type = "number", Label = "帧间隔", DefaultValue = 5 },
                    ["maxFrames"] = new PropertyDefinition { Type = "number", Label = "最大帧数", DefaultValue = 0 },
                    ["enableTracking"] = new PropertyDefinition { Type = "boolean", Label = "启用目标跟踪", DefaultValue = false },
                    ["saveFrameStats"] = new PropertyDefinition { Type = "boolean", Label = "保存帧统计", DefaultValue = true },
                    ["outputVideo"] = new PropertyDefinition { Type = "boolean", Label = "输出处理视频", DefaultValue = false }
                }
            },

            // 图像预处理节点
            new NodeDefinition
            {
                Type = "image-preprocess",
                Category = "vision",
                Name = "图像预处理",
                Description = "AI模型的图像预处理管道",
                Color = "#14b8a6",
                Icon = "fa-wrench",
                Inputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "image", Type = "image", Label = "图像输入" }
                },
                Outputs = new List<PortDefinition>
                {
                    new PortDefinition { Name = "tensor", Type = "object", Label = "张量输出" },
                    new PortDefinition { Name = "image", Type = "image", Label = "预览图像" }
                },
                Properties = new Dictionary<string, PropertyDefinition>
                {
                    ["resizeWidth"] = new PropertyDefinition { Type = "number", Label = "调整宽度", DefaultValue = 640 },
                    ["resizeHeight"] = new PropertyDefinition { Type = "number", Label = "调整高度", DefaultValue = 640 },
                    ["normalize"] = new PropertyDefinition { Type = "boolean", Label = "归一化", DefaultValue = true },
                    ["meanR"] = new PropertyDefinition { Type = "number", Label = "均值R", DefaultValue = 0.0f },
                    ["meanG"] = new PropertyDefinition { Type = "number", Label = "均值G", DefaultValue = 0.0f },
                    ["meanB"] = new PropertyDefinition { Type = "number", Label = "均值B", DefaultValue = 0.0f },
                    ["stdR"] = new PropertyDefinition { Type = "number", Label = "标准差R", DefaultValue = 1.0f },
                    ["stdG"] = new PropertyDefinition { Type = "number", Label = "标准差G", DefaultValue = 1.0f },
                    ["stdB"] = new PropertyDefinition { Type = "number", Label = "标准差B", DefaultValue = 1.0f },
                    ["channelOrder"] = new PropertyDefinition { Type = "select", Label = "通道顺序", DefaultValue = "RGB" },
                    ["dataFormat"] = new PropertyDefinition { Type = "select", Label = "数据格式", DefaultValue = "NCHW" }
                }
            }
        };
    }
}
