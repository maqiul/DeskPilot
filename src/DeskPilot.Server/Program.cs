using DeskPilot.Core.Services;
using DeskPilot.Server;

var builder = WebApplication.CreateBuilder(args);

// v0.0.2 MVP: StubChatService。v0.0.3 替换为 SemanticKernelChatService
builder.Services.AddSingleton<IChatService, StubChatService>();

// v0.0.3: 支持 CLI --urls 参数（Tauri sidecar 启动时传端口），默认 5180
var urls = args.FirstOrDefault(a => a.StartsWith("--urls="))?.Substring("--urls=".Length)
           ?? "http://localhost:5180";
builder.WebHost.UseUrls(urls);

var app = builder.Build();

// 健康检查端点（方便 Tauri 探活）
app.MapGet("/", () => Results.Ok(new
{
    service = "DeskPilot.Server",
    version = "v0.0.2",
    status = "running"
}));

// v0.0.2 MVP 端点：一次性返回 reply（query string 简单传参）
// v0.0.3 升级为 POST + JSON body + SSE 流式输出
app.MapGet("/api/chat", async (string prompt, IChatService chat, CancellationToken ct) =>
{
    try
    {
        var reply = await chat.ChatAsync(prompt, ct);
        return Results.Ok(new
        {
            reply,
            success = true,
            version = "v0.0.2"
        });
    }
    catch (System.Exception ex)
    {
        return Results.Ok(new
        {
            reply = $"错误：{ex.Message}",
            success = false,
            version = "v0.0.2"
        });
    }
});

app.Run();