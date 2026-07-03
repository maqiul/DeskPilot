using DeskPilot.Core.Services;
using DeskPilot.Server;

var builder = WebApplication.CreateBuilder(args);

// v0.0.2 MVP: StubChatService。v0.0.4 接真 AI 时换 SemanticKernelChatService
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
    version = "v0.0.4",
    status = "running"
}));

// v0.0.4 MVP 端点：拼接 ChatStreamAsync 流式输出一次性返回。
// v0.0.5 升级为真实 SSE 流（前端逐 token 显示）。
app.MapGet("/api/chat", async (string prompt, IChatService chat, CancellationToken ct) =>
{
    try
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in chat.ChatStreamAsync(prompt, ct))
        {
            sb.Append(chunk);
        }
        return Results.Ok(new
        {
            reply = sb.ToString(),
            success = true,
            version = "v0.0.4"
        });
    }
    catch (System.Exception ex)
    {
        return Results.Ok(new
        {
            reply = $"错误：{ex.Message}",
            success = false,
            version = "v0.0.4"
        });
    }
});

// v0.0.4 新端点：真 SSE 流式输出（Server-Sent Events）。
// 浏览器/Vue 端 fetch + ReadableStream 读逐 token chunk。
app.MapGet("/api/chat/stream", async (string prompt, IChatService chat, HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";

    try
    {
        await foreach (var chunk in chat.ChatStreamAsync(prompt, ct))
        {
            // SSE 协议：data: <json>\n\n
            var line = $"data: {System.Text.Json.JsonSerializer.Serialize(new { chunk })}\n\n";
            await ctx.Response.WriteAsync(line, ct);
            await ctx.Response.Body.FlushAsync(ct);
        }

        // 流结束标记
        await ctx.Response.WriteAsync("data: [DONE]\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }
    catch (System.Exception ex)
    {
        var err = $"data: {System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message })}\n\n";
        await ctx.Response.WriteAsync(err, ct);
        await ctx.Response.Body.FlushAsync(ct);
    }
});

app.Run();