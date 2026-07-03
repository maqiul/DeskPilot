using DeskPilot.Core.Services;
using DeskPilot.Core.Tools;
using DeskPilot.Server;

var builder = WebApplication.CreateBuilder(args);

// v0.0.2 MVP: StubChatService。v0.0.4 接真 AI 时换 SemanticKernelChatService
builder.Services.AddSingleton<IChatService, StubChatService>();

// v0.0.7: 注册 18 个 Core Tools 到 IToolRegistry
// 暂只注册 5 个 Safe Tool 让端到端可见，剩余 13 个 v0.0.8 推进
builder.Services.AddSingleton<IToolRegistry>(sp =>
{
    var registry = new ToolRegistry();
    registry.Register(new HashFilesTool());           // Safe：计算文件 hash
    registry.Register(new TextStatsTool());           // Safe：统计文本
    registry.Register(new SearchContentTool());       // Safe：搜索文本
    registry.Register(new FindDuplicatesTool());      // Safe：找重复文件
    registry.Register(new ExtractArchiveTool());      // Safe：解压
    return registry;
});

// v0.0.3: 支持 CLI --urls 参数（Tauri sidecar 启动时传端口），默认 5180
var urls = args.FirstOrDefault(a => a.StartsWith("--urls="))?.Substring("--urls=".Length)
           ?? "http://localhost:5180";
builder.WebHost.UseUrls(urls);

var app = builder.Build();

// 健康检查端点（方便 Tauri 探活）
app.MapGet("/", () => Results.Ok(new
{
    service = "DeskPilot.Server",
    version = "v0.0.7",
    status = "running"
}));

// v0.0.4: 拼接 ChatStreamAsync 流式输出一次性返回。
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
            version = "v0.0.7"
        });
    }
    catch (System.Exception ex)
    {
        return Results.Ok(new
        {
            reply = $"错误：{ex.Message}",
            success = false,
            version = "v0.0.7"
        });
    }
});

// v0.0.4: SSE 流式输出
app.MapGet("/api/chat/stream", async (string prompt, IChatService chat, HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";

    try
    {
        await foreach (var chunk in chat.ChatStreamAsync(prompt, ct))
        {
            var line = $"data: {System.Text.Json.JsonSerializer.Serialize(new { chunk })}\n\n";
            await ctx.Response.WriteAsync(line, ct);
            await ctx.Response.Body.FlushAsync(ct);
        }
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

// v0.0.7: 列出所有已注册的 Tools
app.MapGet("/api/tools/list", (IToolRegistry registry) =>
{
    var tools = registry.ListTools().Select(t => new
    {
        name = t.Name,
        description = t.Description,
        kernelFunctionCount = t.KernelFunctionCount
    });
    return Results.Ok(new
    {
        count = tools.Count(),
        tools
    });
});

// v0.0.7: 执行一个 Tool
app.MapPost("/api/tools/execute", async (string name, HttpContext ctx, IToolRegistry registry, CancellationToken ct) =>
{
    var tool = registry.Get(name);
    if (tool is null)
    {
        return Results.Ok(new
        {
            success = false,
            error = $"Tool '{name}' 不存在。可用工具：{string.Join(", ", registry.ListNames())}"
        });
    }

    // POST body 是 arguments JSON
    string argsJson = "{}";
    using (var reader = new StreamReader(ctx.Request.Body))
    {
        argsJson = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(argsJson)) argsJson = "{}";
    }

    try
    {
        var result = await tool.ExecuteAsync(argsJson, ct);
        return Results.Ok(new
        {
            success = result.Success,
            summary = result.Summary,
            data = result.Data,
            error = result.ErrorMessage
        });
    }
    catch (System.Exception ex)
    {
        return Results.Ok(new
        {
            success = false,
            error = ex.Message
        });
    }
});

app.Run();