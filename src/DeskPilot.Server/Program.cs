using DeskPilot.Core.Services;
using DeskPilot.Core.Tools;
using DeskPilot.Server;

var builder = WebApplication.CreateBuilder(args);

// v0.0.2 MVP: StubChatService。v0.0.4 接真 AI 时换 SemanticKernelChatService
builder.Services.AddSingleton<IChatService, StubChatService>();

// v0.1.1: Tool 调用历史持久化（最近 100 条）
builder.Services.AddSingleton<ToolHistoryStore>();

// v0.0.8: 全量注册 17 个 Core Tools
builder.Services.AddSingleton<IToolRegistry>(sp =>
{
    var registry = new ToolRegistry();
    // 5 个 Safe Tool（v0.0.7 已注册）
    registry.Register(new HashFilesTool());
    registry.Register(new TextStatsTool());
    registry.Register(new SearchContentTool());
    registry.Register(new FindDuplicatesTool());
    registry.Register(new ExtractArchiveTool());
    // 12 个 v0.0.8 新增（含 1 Safe + 11 Destructive）
    registry.Register(new ArchiveByDateTool());
    registry.Register(new BatchExcelTool());
    registry.Register(new BatchResizeImageTool());
    registry.Register(new ConvertImageTool());
    registry.Register(new CropImageTool());
    registry.Register(new MergePdfTool());
    registry.Register(new MoveFilesTool());
    registry.Register(new RenameByExifTool());
    registry.Register(new RenameByPatternTool());
    registry.Register(new RotateImageTool());
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
    version = "v0.1.7",
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
            version = "v0.1.7"
        });
    }
    catch (System.Exception ex)
    {
        return Results.Ok(new
        {
            reply = $"错误：{ex.Message}",
            success = false,
            version = "v0.1.7"
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

// v0.1.0: 列出所有已注册的 Tools（新增 risk 字段）
app.MapGet("/api/tools/list", (IToolRegistry registry) =>
{
    var tools = registry.ListTools().Select(t => new
    {
        name = t.Name,
        description = t.Description,
        kernelFunctionCount = t.KernelFunctionCount,
        risk = t.Risk
    });
    return Results.Ok(new
    {
        count = tools.Count(),
        tools
    });
});

// v0.1.1: 执行一个 Tool（接入 ToolHistoryStore）
app.MapPost("/api/tools/execute", async (string name, HttpContext ctx, IToolRegistry registry, ToolHistoryStore history, CancellationToken ct) =>
{
    // POST body 是 arguments JSON（先读记录到历史，再 dispatch）
    string argsJson = "{}";
    using (var reader = new StreamReader(ctx.Request.Body))
    {
        argsJson = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(argsJson)) argsJson = "{}";
    }

    var tool = registry.Get(name);
    if (tool is null)
    {
        history.Add(new ToolHistoryEntry
        {
            ToolName = name,
            ArgsJson = argsJson,
            Success = false,
            Summary = "",
            ErrorMessage = $"Tool '{name}' 不存在"
        });
        return Results.Ok(new
        {
            success = false,
            error = $"Tool '{name}' 不存在。可用工具：{string.Join(", ", registry.ListNames())}"
        });
    }

    // POST body 是 arguments JSON（前面已读，重复 skip）
    try
    {
        var result = await tool.ExecuteAsync(argsJson, ct);
        // 记录历史
        history.Add(new ToolHistoryEntry
        {
            ToolName = name,
            ArgsJson = argsJson,
            Success = result.Success,
            Summary = result.Summary ?? "",
            ErrorMessage = result.ErrorMessage
        });
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
        // 失败也记录
        history.Add(new ToolHistoryEntry
        {
            ToolName = name,
            ArgsJson = argsJson,
            Success = false,
            Summary = "",
            ErrorMessage = ex.Message
        });
        return Results.Ok(new
        {
            success = false,
            error = ex.Message
        });
    }
});

// v0.1.1: 列出最近 N 条 Tool 调用历史（默认 50，最大 100）
// v0.1.4: 支持 before 参数分页（ISO 8601 时间戳，返回早于该时间的记录）
app.MapGet("/api/tools/history", (ToolHistoryStore history, int? limit, string? before) =>
{
    var n = Math.Clamp(limit ?? 50, 1, 100);
    IReadOnlyList<DeskPilot.Server.ToolHistoryEntry> entriesRaw;
    if (!string.IsNullOrWhiteSpace(before) && DateTime.TryParse(before, out var beforeDt))
    {
        entriesRaw = history.ListBefore(beforeDt.ToUniversalTime(), n);
    }
    else
    {
        entriesRaw = history.List(n);
    }
    var entries = entriesRaw.Select(e => new
    {
        timestamp = e.Timestamp,
        toolName = e.ToolName,
        argsJson = e.ArgsJson,
        success = e.Success,
        summary = e.Summary,
        errorMessage = e.ErrorMessage
    });
    return Results.Ok(new
    {
        count = entries.Count(),
        entries
    });
});

// v0.1.7: 深度健康探活（验证 sidecar 不止进程在，且 ToolRegistry/历史存储都正常）
app.MapGet("/api/health", (IToolRegistry registry, ToolHistoryStore history) =>
{
    var toolCount = registry.ListTools().Count();
    var toolNames = registry.ListNames().ToList();
    var ok = toolCount > 0;

    // 探测历史存储：尝试读取（不写）
    var historyOk = true;
    var historyMsg = "ok";
    try
    {
        var _ = history.List(1); // 不抛即视为 ok
    }
    catch (System.Exception ex)
    {
        historyOk = false;
        historyMsg = ex.Message;
    }

    return Results.Ok(new
    {
        service = "DeskPilot.Server",
        version = "v0.1.7",
        status = ok ? "ready" : "degraded",
        checks = new
        {
            toolRegistry = new
            {
                ok = toolCount > 0,
                count = toolCount,
                sample = toolNames.Take(3).ToList(),
                message = toolCount > 0 ? "工具已注册" : "ToolRegistry 为空！请检查 Program.cs DI 注册"
            },
            historyStore = new
            {
                ok = historyOk,
                message = historyMsg
            }
        }
    });
});

app.Run();