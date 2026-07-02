using DeskPilot.App.Models;
using DeskPilot.App.Services;
using DeskPilot.App.ViewModels;
using DeskPilot.App.Views;
using DeskPilot.Core.Services;
using DeskPilot.Core.Tools;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace DeskPilot.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static IConfiguration Configuration { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // v0.19.0: 单实例 Mutex - 防止 DeskPilot 多开
        var singleInstance = new SingleInstanceService();
        if (!singleInstance.IsFirstInstance)
        {
            // 第二次启动：激活旧窗口 + 退出
            singleInstance.ActivateExistingInstance();
            singleInstance.Dispose();
            Shutdown(0);
            return;
        }
        // 第一个实例：注册 Exit 事件清理 Mutex
        Exit += (_, _) => singleInstance.Dispose();

        // v0.5.1: CI smoke test — 设置 DESKPILOT_SMOKE_TEST=1 启动验证 XAML+DI 全链路
        if (Environment.GetEnvironmentVariable("DESKPILOT_SMOKE_TEST") == "1")
        {
            try
            {
                Environment.SetEnvironmentVariable("DESKPILOT_PROVIDER", "OpenAI");
                Environment.SetEnvironmentVariable("DESKPILOT_API_KEY", "smoke-test-dummy-key");
                Environment.SetEnvironmentVariable("DESKPILOT_MODEL", "smoke-test-model");

                TryLoadDotEnv();
                var stBuilder = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddEnvironmentVariables(prefix: "DESKPILOT_");
                Configuration = stBuilder.Build();

                var stSettingsService = new SecureSettingsService();
                var stSettings = LoadSettings(stSettingsService);

                var stServices = new ServiceCollection();
                stServices.AddSingleton<IConfiguration>(Configuration);
                stServices.AddSingleton<ISettingsService>(stSettingsService);
                stServices.AddSingleton(stSettings);
                stServices.AddSingleton<IChatService>(new StubChatService());
                stServices.AddHttpClient();
                stServices.AddSingleton<IModelListerFactory, ModelListerFactory>();
                stServices.AddSingleton<IToolRegistry>(sp =>
                {
                    var registry = new ToolRegistry();
                    registry.Register(new ArchiveByDateTool());
                    registry.Register(new MoveFilesTool());
                    registry.Register(new FindDuplicatesTool());
                    registry.Register(new RenameByPatternTool());
                    registry.Register(new BatchResizeImageTool());
                    registry.Register(new ExtractArchiveTool());
                    registry.Register(new HashFilesTool());
                    // v0.13: 文本文件统计（行数/字符/高频词）
                    registry.Register(new TextStatsTool());
                    // v0.13: 文件内容搜索（正则 + 目录递归 + 文件过滤）
                    registry.Register(new SearchContentTool());
                    // v0.14: PDF 合并（多个 PDF → 一个新 PDF，纯托管 PdfSharpCore）
                    registry.Register(new MergePdfTool());
                    // v0.14: 图片格式转换（png/jpg/bmp/webp/gif 互转，System.Drawing）
                    registry.Register(new ConvertImageTool());
                    // v0.14: Excel 批处理（list_sheets / extract_data / write_summary，ClosedXML）
                    registry.Register(new BatchExcelTool());
                    return registry;
                });
                // v0.15: ChatViewModel 注入 SkillCenterWindow 工厂 delegate（Ctrl+Shift+K 打开技能中心）
                stServices.AddSingleton<ChatViewModel>(sp => new ChatViewModel(
                    sp.GetRequiredService<IChatService>(),
                    sp.GetRequiredService<ISkillService>(),
                    sp.GetRequiredService<ISkillExecutor>(),
                    skillCenterFactory: () => sp.GetRequiredService<SkillCenterWindow>()));
                stServices.AddTransient<ChatWindow>();
                stServices.AddTransient<SettingsWindow>();
                // v0.15: 独立技能中心窗口
                stServices.AddSingleton<SkillCenterViewModel>();
                stServices.AddTransient<SkillCenterWindow>();
                stServices.AddSingleton<ISkillService, SkillService>();

                // v0.12: 多步技能执行器（smoke test 路径）
                stServices.AddSingleton<ISkillExecutor>(sp => new SkillExecutor(sp.GetRequiredService<IToolRegistry>()));
                stServices.AddHttpClient<ISkillMarket, SkillMarketService>(http => http.Timeout = TimeSpan.FromSeconds(10));
                stServices.AddSingleton<SettingsViewModel>(sp => new SettingsViewModel(
                    sp.GetRequiredService<ISettingsService>(),
                    sp.GetRequiredService<IModelListerFactory>(),
                    sp.GetRequiredService<ISkillService>(),
                    sp.GetService<ISkillMarket>(),
                    sp.GetService<IMarketplaceSourceService>(),
                    closeWindow: null));
                Services = stServices.BuildServiceProvider();

                var stWindow = Services.GetRequiredService<ChatWindow>();
                stWindow.Show();

                // v0.16 F: smoke test 触发 SkillCenterWindow Show + Close，让 WPF 真实解析 SkillCenterWindow.xaml
                // 防 v0.15.1 同类 XamlParseException bug 静默存活（v0.15 之前 smoke test 只验证 ChatWindow 加载）
                var stSkillCenter = Services.GetRequiredService<SkillCenterWindow>();
                stSkillCenter.Show();
                stSkillCenter.Close();

                // v0.18.0: smoke test 触发 TrayIconService 实例化，让 WinForms NotifyIcon 真实创建 + Dispose
                // 防 TrayIconService 集成 XAML+WinForms 互操作的初始化错误静默存活
                var stTrayIcon = new TrayIconService(stWindow);
                stWindow.SetTrayIcon(stTrayIcon);
                stTrayIcon.Dispose();

                Shutdown(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[smoke-test] FAIL: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Shutdown(2);
            }
        }

        // === 1) .env 文件加载（兼容命令行用户）===
        TryLoadDotEnv();

        // === 2) 构建配置 ===
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "DESKPILOT_");

#if DEBUG
        builder.AddUserSecrets(typeof(App).Assembly, optional: true);
#endif
        Configuration = builder.Build();

        // === 3) 加载设置（优先从加密文件，其次从环境变量/JSON）===
        var settingsService = new SecureSettingsService();
        var settings = LoadSettings(settingsService);

        // === 4) 校验 AI Key ===
        if (!HasApiKey(settings, out var missingHint))
        {
            var result = MessageBox.Show(
                "❌ 未检测到 AI API Key。\n\n" +
                "请在接下来的设置窗口中配置，或参考下方说明：\n\n" +
                missingHint,
                "DeskPilot - 缺少 API Key",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel)
            {
                Shutdown(1);
                return;
            }
            // 用户点"确定" → 进入设置流程
            settings = PromptForSettings(settingsService) ?? settings;
            if (settings == null || !HasApiKey(settings, out _))
            {
                Shutdown(1);
                return;
            }
        }

        // === 5) 注册服务 ===
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration);
        services.AddSingleton<ISettingsService>(settingsService);
        services.AddSingleton(settings);

        // v0.0.3: HTTP 客户端工厂 + 模型列表工厂
        services.AddHttpClient();
        services.AddSingleton<IModelListerFactory, ModelListerFactory>();

        // v0.1.1: 工具注册中心 + 内置工具
        services.AddSingleton<IToolRegistry>(sp =>
        {
            var registry = new ToolRegistry();
            // v0.1: 按日期归档
            registry.Register(new ArchiveByDateTool());
            // v0.2: 批量移动
            registry.Register(new MoveFilesTool());
            // v0.2: 找重复文件
            registry.Register(new FindDuplicatesTool());
            // v0.2: 批量重命名
            registry.Register(new RenameByPatternTool());
            // v0.5: 批量缩放图片
            registry.Register(new BatchResizeImageTool());
            // v0.5: 解压 zip
            registry.Register(new ExtractArchiveTool());
            // v0.5: 计算文件哈希
            registry.Register(new HashFilesTool());
            // v0.13: 文本文件统计（行数/字符/高频词）
            registry.Register(new TextStatsTool());
            // v0.13: 文件内容搜索（正则 + 目录递归 + 文件过滤）
            registry.Register(new SearchContentTool());
            // v0.14: PDF 合并
            registry.Register(new MergePdfTool());
            // v0.14: 图片格式转换
            registry.Register(new ConvertImageTool());
            // v0.14: Excel 批处理
            registry.Register(new BatchExcelTool());
            return registry;
        });

        // v0.15: smoke test 分支 ChatViewModel 同样注入 SkillCenterWindow 工厂 delegate
        services.AddSingleton<ChatViewModel>(sp => new ChatViewModel(
            sp.GetRequiredService<IChatService>(),
            sp.GetService<ISkillService>(),
            sp.GetService<ISkillExecutor>(),
            skillCenterFactory: () => sp.GetRequiredService<SkillCenterWindow>()));
        services.AddTransient<ChatWindow>();
        services.AddTransient<SettingsWindow>();
        // v0.15: 独立技能中心窗口
        services.AddSingleton<SkillCenterViewModel>();
        services.AddTransient<SkillCenterWindow>();

        services.AddSingleton<SettingsViewModel>(sp => new SettingsViewModel(
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<IModelListerFactory>(),
            sp.GetService<ISkillService>(),
            sp.GetService<ISkillMarket>(),
            sp.GetService<IMarketplaceSourceService>(),
            closeWindow: null));

        // v0.6: 权限服务（危险操作需确认）
        var permService = new ToolPermissionService();
        services.AddSingleton<IToolPermissionService>(permService);

        // v0.7: 本地记忆存储
        services.AddSingleton<IMemoryStore>(new LocalJsonMemoryStore());

        // v0.9: 技能服务
        services.AddSingleton<ISkillService, SkillService>();

        // v0.12: 多步技能执行器（依赖 IToolRegistry）
        services.AddSingleton<ISkillExecutor>(sp => new SkillExecutor(sp.GetRequiredService<IToolRegistry>()));

        // v0.10: 技能市场（HttpClient 注入，GitHub raw URL 由 SkillMarketService 内部默认）
        services.AddHttpClient<ISkillMarket, SkillMarketService>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(10);
        });

        // v0.11: 多市场源服务（QwenPaw / ClawHub / ModelScope）
        services.AddHttpClient("skill-market", http => http.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<IMarketplaceSourceService, MarketplaceSourceService>();

        // IChatService 用工厂模式，支持运行时重建
        services.AddSingleton<IChatService>(sp =>
            CreateChatService(sp.GetRequiredService<AppSettings>(), permService, sp.GetRequiredService<IMemoryStore>()));

        Services = services.BuildServiceProvider();

        // === 6) 监听设置变化，动态重建 ChatService ===
        var settingsVm = Services.GetRequiredService<SettingsViewModel>();
        settingsVm.ChatServiceChanged += (_, newSettings) =>
        {
            // 更新全局设置
            var oldSettings = Services.GetRequiredService<AppSettings>();
            CopySettings(newSettings, oldSettings);

            // 重建 IChatService（直接 Dispose 旧的 Kernel）
            var newChatService = CreateChatService(newSettings, Services.GetRequiredService<IToolPermissionService>(), Services.GetRequiredService<IMemoryStore>());
            var oldChatService = Services.GetService<IChatService>();
            (oldChatService as IDisposable)?.Dispose();

            // 重新注册 ChatService 到容器
            // 注：Singleton 容器不能直接替换，改为让 ChatViewModel 持有引用
            // 这里采用最简方案：触发 ChatViewModel 重建
            var chatVm = Services.GetRequiredService<ChatViewModel>();
            chatVm.ResetChatService(newChatService);
        };

        // === 7) 应用主题（按设置） ===
        var initialSettings = settingsService.Load();
        ThemeManager.ApplyTheme(initialSettings.Theme);

        // === 8) 启动窗口 ===
        var chatWindow = Services.GetRequiredService<ChatWindow>();

        // v0.18.0: 系统托盘 - ChatWindow 关闭时最小化到托盘（而不是退出进程）
        var trayIcon = new TrayIconService(chatWindow);
        chatWindow.SetTrayIcon(trayIcon);
        // v0.18.0: 主进程退出时清理托盘
        Exit += (_, _) => trayIcon.Dispose();

        chatWindow.Show();
    }

    /// <summary>
    /// 加载设置：优先级 加密文件 > .env/appsettings > 默认
    /// </summary>
    private static AppSettings LoadSettings(ISettingsService settingsService)
    {
        var settings = settingsService.Load();

        // 如果加密文件为空，回退到配置（兼容命令行用户）
        bool hasAnyKey = !string.IsNullOrWhiteSpace(settings.OpenAiApiKey)
                      || !string.IsNullOrWhiteSpace(settings.DeepSeekApiKey);

        if (!hasAnyKey)
        {
            var providerStr = Configuration["AI:Provider"];
            if (Enum.TryParse<AiProvider>(providerStr, true, out var provider))
                settings.Provider = provider;

            settings.OpenAiApiKey = Configuration["AI:OpenAI:ApiKey"]
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? settings.OpenAiApiKey;
            settings.OpenAiModel = Configuration["AI:OpenAI:Model"] ?? settings.OpenAiModel;

            settings.DeepSeekApiKey = Configuration["AI:DeepSeek:ApiKey"]
                ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
                ?? settings.DeepSeekApiKey;
            settings.DeepSeekModel = Configuration["AI:DeepSeek:Model"] ?? settings.DeepSeekModel;

            settings.OllamaEndpoint = Configuration["AI:Ollama:Endpoint"] ?? settings.OllamaEndpoint;
            settings.OllamaModel = Configuration["AI:Ollama:Model"] ?? settings.OllamaModel;
        }

        return settings;
    }

    /// <summary>
    /// 弹出设置窗口让用户输入。
    /// </summary>
    private static AppSettings? PromptForSettings(ISettingsService settingsService)
    {
        var tempServices = new ServiceCollection();
        tempServices.AddSingleton<ISettingsService>(settingsService);
        tempServices.AddHttpClient();
        tempServices.AddSingleton<IModelListerFactory, ModelListerFactory>();
        tempServices.AddSingleton<SettingsViewModel>(sp => new SettingsViewModel(
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<IModelListerFactory>(),
            skillService: null,
            skillMarket: null,
            marketSources: null,
            closeWindow: null));
        tempServices.AddTransient<SettingsWindow>();
        var sp = tempServices.BuildServiceProvider();

        var vm = sp.GetRequiredService<SettingsViewModel>();
        AppSettings? result = null;
        vm.ChatServiceChanged += (_, s) => result = s;

        var win = sp.GetRequiredService<SettingsWindow>();
        win.ShowDialog();
        return result;
    }

    /// <summary>
    /// 根据当前设置创建 ChatService。
    /// </summary>
    private IChatService CreateChatService(AppSettings settings, IToolPermissionService permission, IMemoryStore memoryStore)
    {
        var kernelBuilder = Kernel.CreateBuilder();

        switch (settings.Provider)
        {
            case AiProvider.OpenAI:
                kernelBuilder.AddOpenAIChatCompletion(
                    string.IsNullOrWhiteSpace(settings.OpenAiModel) ? "gpt-4o-mini" : settings.OpenAiModel,
                    settings.OpenAiApiKey);
                break;

            case AiProvider.DeepSeek:
                kernelBuilder.AddOpenAIChatCompletion(
                    modelId: string.IsNullOrWhiteSpace(settings.DeepSeekModel) ? "deepseek-chat" : settings.DeepSeekModel,
                    apiKey: settings.DeepSeekApiKey,
                    endpoint: new Uri("https://api.deepseek.com/v1"));
                break;

            case AiProvider.Ollama:
                var endpoint = string.IsNullOrWhiteSpace(settings.OllamaEndpoint)
                    ? "http://localhost:11434"
                    : settings.OllamaEndpoint.TrimEnd('/');
                kernelBuilder.AddOpenAIChatCompletion(
                    modelId: string.IsNullOrWhiteSpace(settings.OllamaModel) ? "qwen2.5:7b" : settings.OllamaModel,
                    apiKey: "ollama",
                    endpoint: new Uri(endpoint + "/v1"));
                break;
        }

        var kernel = kernelBuilder.Build();

        // 注册工具到 Kernel（让 AI 能调用）
        var toolRegistry = Services.GetRequiredService<IToolRegistry>();
        foreach (var plugin in toolRegistry.CreateKernelPlugins())
        {
            kernel.Plugins.Add(plugin);
        }

        return new SemanticKernelChatService(kernel, toolRegistry, permission, memoryStore);
    }

    private static void CopySettings(AppSettings src, AppSettings dst)
    {
        dst.Provider = src.Provider;
        dst.OpenAiApiKey = src.OpenAiApiKey;
        dst.OpenAiModel = src.OpenAiModel;
        dst.DeepSeekApiKey = src.DeepSeekApiKey;
        dst.DeepSeekModel = src.DeepSeekModel;
        dst.OllamaEndpoint = src.OllamaEndpoint;
        dst.OllamaModel = src.OllamaModel;
        dst.CachedModels = src.CachedModels ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
    }

    private static void TryLoadDotEnv()
    {
        try
        {
            var searchPaths = new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
                Path.Combine(AppContext.BaseDirectory, "..", "..", ".."),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."),
            };

            foreach (var dir in searchPaths)
            {
                var envPath = Path.Combine(dir, ".env");
                if (File.Exists(envPath))
                {
                    Env.Load(envPath);
                    return;
                }
            }
        }
        catch { }
    }

    private static bool HasApiKey(AppSettings settings, out string hint)
    {
        if (settings.Provider == AiProvider.Ollama)
        {
            hint = "Ollama 模式无需 API Key，请确保本地已运行 Ollama 服务。";
            return true;
        }

        var key = settings.Provider switch
        {
            AiProvider.OpenAI => settings.OpenAiApiKey,
            AiProvider.DeepSeek => settings.DeepSeekApiKey,
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(key))
        {
            hint = string.Empty;
            return true;
        }

        hint = settings.Provider switch
        {
            AiProvider.OpenAI =>
                "方式一（推荐）：在接下来的设置窗口中填入 API Key。\n\n" +
                "方式二（命令行）：项目根目录创建 .env 文件，写入：\n" +
                "    OPENAI_API_KEY=sk-xxxxxxxxxxxx",
            AiProvider.DeepSeek =>
                "方式一（推荐）：在接下来的设置窗口中填入 API Key。\n\n" +
                "方式二（命令行）：项目根目录创建 .env 文件，写入：\n" +
                "    DEEPSEEK_API_KEY=sk-xxxxxxxxxxxx",
            _ => $"未识别的 Provider: {settings.Provider}"
        };
        return false;
    }
}

/// <summary>
/// v0.5.1 CI smoke test 用的 stub — 不调 AI，只返回固定回复
/// </summary>
internal sealed class StubChatService : IChatService
{
    public Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default)
        => Task.FromResult("[smoke-test] Stub reply");

    public async IAsyncEnumerable<string> ChatStreamAsync(
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "[smoke-test] Stub reply";
        await Task.CompletedTask;
    }

    public void Dispose() { }
}