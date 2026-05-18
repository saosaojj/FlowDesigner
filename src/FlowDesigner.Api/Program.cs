using FlowDesigner.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Flow Designer API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024 * 1024 * 100;
});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton<FlowService>();
builder.Services.AddSingleton<NodeRegistryService>();
builder.Services.AddSingleton<PerformanceMonitor>();
builder.Services.AddSingleton<BackpressureController>();
builder.Services.AddSingleton<HighPerformanceExecutionEngine>();
builder.Services.AddSingleton<ExecutionEngine>();

builder.Services.AddSingleton<RealYoloDetectionService>();
builder.Services.AddSingleton<RealImageProcessingService>();
builder.Services.AddSingleton<VideoStreamService>();
builder.Services.AddSingleton<AdvancedPerformanceMonitor>();

builder.Services.AddSingleton<YoloDetectionService>();
builder.Services.AddSingleton<ImageProcessingService>();

builder.Services.AddSingleton<DashboardService>();

builder.Services.AddSingleton<WebSocketService>();
builder.Services.AddSingleton<TcpService>();
builder.Services.AddSingleton<RtpService>();

builder.Services.AddSingleton<CommunicationPerformanceMonitor>();
builder.Services.AddSingleton<EnhancedWebSocketService>();
builder.Services.AddSingleton<EnhancedTcpService>();
builder.Services.AddSingleton<EnhancedRtpService>();
builder.Services.AddSingleton<CommunicationNodeExecutor>();

builder.Services.AddHttpClient<FlowApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseAddress"] ?? "http://localhost:5000");
});

var maxConcurrency = builder.Configuration.GetValue("Execution:MaxConcurrency", 100);
var maxQueueSize = builder.Configuration.GetValue("Execution:MaxQueueSize", 10000);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Flow Designer API v1"));
}

app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("=");
logger.LogInformation("   Flow Designer API 已启动   ");
logger.LogInformation("=");
logger.LogInformation("高性能执行引擎: 已初始化");
logger.LogInformation("最大并发数: {MaxConcurrency}", maxConcurrency);
logger.LogInformation("最大队列大小: {MaxQueueSize}", maxQueueSize);
logger.LogInformation("YOLO 检测服务: 已注册");
logger.LogInformation("OpenCV 处理服务: 已注册");
logger.LogInformation("视频流服务: 已注册");
logger.LogInformation("高级性能监控: 已启动");
logger.LogInformation("工业大屏服务: 已注册");
logger.LogInformation("WebSocket 服务: 已注册");
logger.LogInformation("TCP 通讯服务: 已注册");
logger.LogInformation("RTP 通讯服务: 已注册");
logger.LogInformation("增强通讯服务: 已注册");
logger.LogInformation("性能监控: 已启用");
logger.LogInformation("背压控制: 已启用");
logger.LogInformation("连接池管理: 已启用");
logger.LogInformation("Blazor Server: 已启用");
logger.LogInformation("=");

app.Run();
