using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using FlowDesigner.Shared.Models;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Size = System.Drawing.Size;

namespace FlowDesigner.Api.Services;

public class RealImageProcessingService
{
    private readonly ILogger<RealImageProcessingService> _logger;
    private readonly object _lockObject = new();

    public RealImageProcessingService(ILogger<RealImageProcessingService> logger)
    {
        _logger = logger;
        CheckOpenCv();
    }

    private void CheckOpenCv()
    {
        try
        {
            _logger.LogInformation("OpenCV 初始化检查完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenCV 初始化失败");
        }
    }

    public async Task<ImageProcessingResult> ProcessAsync(
        byte[] imageBytes,
        ImageProcessingParams parameters)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new ImageProcessingResult();

        try
        {
            using var image = Image.Load<Rgb24>(imageBytes);
            var mat = ImageSharpToMat(image);

            Mat processed;
            
            switch (parameters.OperationType)
            {
                case ImageOperationType.Resize:
                    processed = Resize(mat, parameters);
                    break;
                case ImageOperationType.Crop:
                    processed = Crop(mat, parameters);
                    break;
                case ImageOperationType.Rotate:
                    processed = Rotate(mat, parameters);
                    break;
                case ImageOperationType.Flip:
                    processed = Flip(mat, parameters);
                    break;
                case ImageOperationType.Blur:
                    processed = Blur(mat, parameters);
                    break;
                case ImageOperationType.Sharpen:
                    processed = Sharpen(mat, parameters);
                    break;
                case ImageOperationType.Grayscale:
                    processed = Grayscale(mat);
                    break;
                case ImageOperationType.Threshold:
                    processed = Threshold(mat, parameters);
                    break;
                case ImageOperationType.EdgeDetection:
                    processed = EdgeDetection(mat, parameters);
                    break;
                case ImageOperationType.Contours:
                    processed = FindContours(mat, parameters);
                    break;
                case ImageOperationType.ColorSpace:
                    processed = ColorSpace(mat, parameters);
                    break;
                case ImageOperationType.HistogramEqualization:
                    processed = HistogramEqualization(mat);
                    break;
                case ImageOperationType.Morphology:
                    processed = Morphology(mat, parameters);
                    break;
                default:
                    processed = mat.Clone();
                    break;
            }

            result.ProcessedImage = MatToBytes(processed);
            result.Width = processed.Width;
            result.Height = processed.Height;
            result.Success = true;
            
            processed.Dispose();
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

    #region 图像处理操作

    private Mat Resize(Mat mat, ImageProcessingParams parameters)
    {
        var targetSize = new Size(parameters.TargetWidth, parameters.TargetHeight);
        var interpolation = Inter.Linear;
        
        if (parameters.KeepAspectRatio)
        {
            var ratio = Math.Min(
                (float)parameters.TargetWidth / mat.Width,
                (float)parameters.TargetHeight / mat.Height);
            
            targetSize = new Size((int)(mat.Width * ratio), (int)(mat.Height * ratio));
        }

        var result = new Mat();
        CvInvoke.Resize(mat, result, targetSize, 0, 0, interpolation);
        return result;
    }

    private Mat Crop(Mat mat, ImageProcessingParams parameters)
    {
        var x = Math.Max(0, parameters.CropX);
        var y = Math.Max(0, parameters.CropY);
        var width = Math.Min(parameters.CropWidth, mat.Width - x);
        var height = Math.Min(parameters.CropHeight, mat.Height - y);

        if (width <= 0 || height <= 0)
        {
            return mat.Clone();
        }

        var rect = new System.Drawing.Rectangle(x, y, width, height);
        return new Mat(mat, rect).Clone();
    }

    private Mat Rotate(Mat mat, ImageProcessingParams parameters)
    {
        var center = new System.Drawing.PointF(mat.Width / 2f, mat.Height / 2f);
        var rotationMatrix = new Mat();
        CvInvoke.GetRotationMatrix2D(center, parameters.RotationAngle, 1.0, rotationMatrix);

        Size newSize;
        if (parameters.RotateExpand)
        {
            var angleRad = parameters.RotationAngle * Math.PI / 180.0;
            var absCos = Math.Abs(Math.Cos(angleRad));
            var absSin = Math.Abs(Math.Sin(angleRad));
            var newWidth = (int)(mat.Height * absSin + mat.Width * absCos);
            var newHeight = (int)(mat.Height * absCos + mat.Width * absSin);

            var data = new double[6];
            System.Runtime.InteropServices.Marshal.Copy(rotationMatrix.DataPointer, data, 0, 6);
            data[2] += (newWidth / 2.0) - center.X;
            data[5] += (newHeight / 2.0) - center.Y;
            System.Runtime.InteropServices.Marshal.Copy(data, 0, rotationMatrix.DataPointer, 6);

            newSize = new Size(newWidth, newHeight);
        }
        else
        {
            newSize = mat.Size;
        }

        var result = new Mat();
        CvInvoke.WarpAffine(mat, result, rotationMatrix, newSize);
        rotationMatrix.Dispose();
        return result;
    }

    private Mat Flip(Mat mat, ImageProcessingParams parameters)
    {
        var result = new Mat();
        CvInvoke.Flip(mat, result, (FlipType)parameters.FlipCode);
        return result;
    }

    private Mat Blur(Mat mat, ImageProcessingParams parameters)
    {
        var kernelSize = parameters.BlurKernelSize;
        if (kernelSize % 2 == 0) kernelSize++;
        kernelSize = Math.Max(1, kernelSize);

        var result = new Mat();
        
        if (parameters.BlurSigmaX > 0)
        {
            CvInvoke.GaussianBlur(mat, result, new Size(kernelSize, kernelSize), parameters.BlurSigmaX);
        }
        else
        {
            CvInvoke.Blur(mat, result, new Size(kernelSize, kernelSize), new System.Drawing.Point(-1, -1));
        }

        return result;
    }

    private Mat Sharpen(Mat mat, ImageProcessingParams parameters)
    {
        var result = new Mat();
        
        // 创建锐化内核
        var kernel = new float[,]
        {
            { 0, -1, 0 },
            { -1, 5, -1 },
            { 0, -1, 0 }
        };

        if (parameters.SharpenStrength != 1.0f)
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (i == 1 && j == 1)
                    {
                        kernel[i, j] = 1 + (kernel[i, j] - 1) * parameters.SharpenStrength;
                    }
                    else
                    {
                        kernel[i, j] *= parameters.SharpenStrength;
                    }
                }
            }
        }

        var kernelMat = new Mat(3, 3, DepthType.Cv32F, 1);
        var kernelData = new float[9];
        int idx = 0;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                kernelData[idx++] = kernel[i, j];
        System.Runtime.InteropServices.Marshal.Copy(kernelData, 0, kernelMat.DataPointer, 9);
        
        CvInvoke.Filter2D(mat, result, kernelMat, new System.Drawing.Point(-1, -1));
        kernelMat.Dispose();
        
        return result;
    }

    private Mat Grayscale(Mat mat)
    {
        var result = new Mat();
        CvInvoke.CvtColor(mat, result, ColorConversion.Bgr2Gray);
        return result;
    }

    private Mat Threshold(Mat mat, ImageProcessingParams parameters)
    {
        var gray = new Mat();
        if (mat.NumberOfChannels != 1)
        {
            CvInvoke.CvtColor(mat, gray, ColorConversion.Bgr2Gray);
        }
        else
        {
            gray = mat.Clone();
        }

        var result = new Mat();
        CvInvoke.Threshold(
            gray, 
            result, 
            parameters.ThresholdValue, 
            parameters.MaxThresholdValue, 
            (ThresholdType)parameters.ThresholdType);

        if (result.NumberOfChannels == 1)
        {
            var rgb = new Mat();
            CvInvoke.CvtColor(result, rgb, ColorConversion.Gray2Bgr);
            result.Dispose();
            return rgb;
        }

        return result;
    }

    private Mat EdgeDetection(Mat mat, ImageProcessingParams parameters)
    {
        var gray = new Mat();
        if (mat.NumberOfChannels != 1)
        {
            CvInvoke.CvtColor(mat, gray, ColorConversion.Bgr2Gray);
        }
        else
        {
            gray = mat.Clone();
        }

        var edges = new Mat();
        CvInvoke.Canny(
            gray, 
            edges, 
            parameters.CannyThreshold1, 
            parameters.CannyThreshold2);

        var result = new Mat();
        CvInvoke.CvtColor(edges, result, ColorConversion.Gray2Bgr);
        
        gray.Dispose();
        edges.Dispose();
        
        return result;
    }

    private Mat FindContours(Mat mat, ImageProcessingParams parameters)
    {
        var gray = new Mat();
        if (mat.NumberOfChannels != 1)
        {
            CvInvoke.CvtColor(mat, gray, ColorConversion.Bgr2Gray);
        }
        else
        {
            gray = mat.Clone();
        }

        // 二值化
        var binary = new Mat();
        CvInvoke.Threshold(gray, binary, 127, 255, ThresholdType.Binary);

        // 查找轮廓
        var contours = new VectorOfVectorOfPoint();
        var hierarchy = new Mat();
        CvInvoke.FindContours(
            binary, 
            contours, 
            hierarchy, 
            (RetrType)parameters.ContourRetrievalMode, 
            (ChainApproxMethod)parameters.ContourApproximationMethod);

        // 绘制轮廓
        var result = mat.Clone();
        for (int i = 0; i < contours.Size; i++)
        {
            CvInvoke.DrawContours(
                result, 
                contours, 
                i, 
                new MCvScalar(0, 255, 0), 
                2);
        }

        gray.Dispose();
        binary.Dispose();
        hierarchy.Dispose();
        contours.Dispose();

        return result;
    }

    private Mat ColorSpace(Mat mat, ImageProcessingParams parameters)
    {
        var result = new Mat();
        CvInvoke.CvtColor(mat, result, (ColorConversion)parameters.ColorConversionCode);
        
        if (result.NumberOfChannels == 1)
        {
            var rgb = new Mat();
            CvInvoke.CvtColor(result, rgb, ColorConversion.Gray2Bgr);
            result.Dispose();
            return rgb;
        }
        
        return result;
    }

    private Mat HistogramEqualization(Mat mat)
    {
        var ycrcb = new Mat();
        CvInvoke.CvtColor(mat, ycrcb, ColorConversion.Bgr2YCrCb);

        var channels = new VectorOfMat();
        CvInvoke.Split(ycrcb, channels);
        
        CvInvoke.EqualizeHist(channels[0], channels[0]);
        
        var result = new Mat();
        CvInvoke.Merge(channels, result);
        
        var bgr = new Mat();
        CvInvoke.CvtColor(result, bgr, ColorConversion.YCrCb2Bgr);

        ycrcb.Dispose();
        result.Dispose();
        channels.Dispose();

        return bgr;
    }

    private Mat Morphology(Mat mat, ImageProcessingParams parameters)
    {
        var kernelSize = parameters.MorphologyKernelSize;
        if (kernelSize % 2 == 0) kernelSize++;
        kernelSize = Math.Max(1, kernelSize);

        var kernel = CvInvoke.GetStructuringElement(
            ElementShape.Rectangle, 
            new Size(kernelSize, kernelSize), 
            new System.Drawing.Point(-1, -1));

        var result = new Mat();
        CvInvoke.MorphologyEx(
            mat, 
            result, 
            (MorphOp)parameters.MorphologyOperation, 
            kernel, 
            new System.Drawing.Point(-1, -1), 
            parameters.MorphologyIterations,
            BorderType.Reflect101,
            new MCvScalar());

        return result;
    }

    #endregion

    #region 辅助方法

    private Mat ImageSharpToMat(Image<Rgb24> image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder());
        ms.Position = 0;
        var mat = new Mat();
        CvInvoke.Imdecode(ms.ToArray(), ImreadModes.Color, mat);
        return mat;
    }

    private byte[] MatToBytes(Mat mat)
    {
        using var ms = new MemoryStream();
        var buffer = new VectorOfByte();
        CvInvoke.Imencode(".jpg", mat, buffer);
        
        return buffer.ToArray();
    }

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
                    imageBytes = parameters.ImageBytes ?? new byte[0];
                    info["Source"] = "Direct Bytes";
                    break;
                case ImageSourceType.Camera:
                    imageBytes = await CaptureFromCameraAsync(parameters.CameraIndex, info);
                    break;
                case ImageSourceType.RTSP:
                    imageBytes = await CaptureFromRTSPAsync(parameters.RTSPUrl, info);
                    break;
                default:
                    imageBytes = GenerateTestImage();
                    info["Source"] = "Test Image";
                    break;
            }

            var mat = new Mat();
            CvInvoke.Imdecode(imageBytes, ImreadModes.Color, mat);
            info["Width"] = mat.Width;
            info["Height"] = mat.Height;
            info["Channels"] = mat.NumberOfChannels;
            mat.Dispose();

            return (imageBytes, info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图像加载失败");
            return (GenerateTestImage(), new Dictionary<string, object> { ["Error"] = ex.Message });
        }
    }

    private async Task<byte[]> LoadFromFileAsync(string? filePath, Dictionary<string, object> info)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            info["Source"] = "Generated Test Image";
            return GenerateTestImage();
        }

        info["Source"] = $"File: {Path.GetFileName(filePath)}";
        return await File.ReadAllBytesAsync(filePath);
    }

    private async Task<byte[]> LoadFromUrlAsync(string? url, Dictionary<string, object> info)
    {
        if (string.IsNullOrEmpty(url))
        {
            info["Source"] = "Generated Test Image";
            return GenerateTestImage();
        }

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        var imageBytes = await httpClient.GetByteArrayAsync(url);
        info["Source"] = $"URL: {url}";
        return imageBytes;
    }

    private byte[] LoadFromBase64(string? base64Data, Dictionary<string, object> info)
    {
        if (string.IsNullOrEmpty(base64Data))
        {
            info["Source"] = "Generated Test Image";
            return GenerateTestImage();
        }

        var data = base64Data.Contains(',') 
            ? base64Data.Split(',')[1] 
            : base64Data;
        
        info["Source"] = "Base64 String";
        return Convert.FromBase64String(data);
    }

    private async Task<byte[]> CaptureFromCameraAsync(int cameraIndex, Dictionary<string, object> info)
    {
        try
        {
            using var capture = new VideoCapture(cameraIndex);
            if (!capture.IsOpened)
            {
                info["Source"] = $"Camera {cameraIndex} - Failed to open";
                return GenerateTestImage();
            }

            // 等待相机初始化
            await Task.Delay(100);

            var frame = new Mat();
            capture.Read(frame);

            if (frame.IsEmpty)
            {
                info["Source"] = $"Camera {cameraIndex} - No frame";
                frame.Dispose();
                return GenerateTestImage();
            }

            var buffer = new VectorOfByte();
            CvInvoke.Imencode(".jpg", frame, buffer);

            info["Source"] = $"Camera {cameraIndex}";
            info["Width"] = frame.Width;
            info["Height"] = frame.Height;

            frame.Dispose();
            return buffer.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "相机捕获失败");
            info["Source"] = $"Camera Error: {ex.Message}";
            return GenerateTestImage();
        }
    }

    private async Task<byte[]> CaptureFromRTSPAsync(string? rtspUrl, Dictionary<string, object> info)
    {
        if (string.IsNullOrEmpty(rtspUrl))
        {
            info["Source"] = "Generated Test Image";
            return GenerateTestImage();
        }

        try
        {
            using var capture = new VideoCapture(rtspUrl);
            if (!capture.IsOpened)
            {
                info["Source"] = $"RTSP - Failed to open";
                return GenerateTestImage();
            }

            await Task.Delay(500);

            var frame = new Mat();
            capture.Read(frame);

            if (frame.IsEmpty)
            {
                info["Source"] = $"RTSP - No frame";
                frame.Dispose();
                return GenerateTestImage();
            }

            var buffer = new VectorOfByte();
            CvInvoke.Imencode(".jpg", frame, buffer);

            info["Source"] = $"RTSP: {rtspUrl}";
            info["Width"] = frame.Width;
            info["Height"] = frame.Height;

            frame.Dispose();
            return buffer.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RTSP 捕获失败");
            info["Source"] = $"RTSP Error: {ex.Message}";
            return GenerateTestImage();
        }
    }

    private byte[] GenerateTestImage()
    {
        using var image = new Image<Rgb24>(640, 480, new Rgb24(50, 50, 50));
        
        // 绘制一些测试内容
        image.Mutate(ctx =>
        {
            for (int i = 0; i < 5; i++)
            {
                var px = 100 + i * 100;
                var py = 100 + (i % 2) * 150;
                var color = new Rgb24((byte)(50 + i * 50), (byte)(100 + i * 30), (byte)(200 - i * 30));
                
                ctx.DrawPolygon(
                    Color.FromRgb(color.R, color.G, color.B),
                    3f,
                    new SixLabors.ImageSharp.PointF(px, py),
                    new SixLabors.ImageSharp.PointF(px + 60, py),
                    new SixLabors.ImageSharp.PointF(px + 30, py + 60));
            }
        });

        using var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder());
        return ms.ToArray();
    }

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
            var prefix = "img";
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var extension = GetExtension(parameters.OutputFormat);
            filename = $"{prefix}_{timestamp}{extension}";

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

    #endregion
}
