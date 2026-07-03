using System.Collections.Concurrent;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 工具注册中心。
///
/// 设计：
/// - 工具类实现 ITool + 在方法上标注 [KernelFunction]
/// - Register() 注册实例并验证至少有一个 KernelFunction 方法
/// - CreateKernelPlugins() 把所有工具打包成 SK 的 KernelPlugin 列表
/// </summary>
public interface IToolRegistry
{
    void Register(ITool tool);
    ITool? Get(string name);
    IReadOnlyList<string> ListNames();
    IReadOnlyList<ToolDescriptor> ListTools();
    IReadOnlyList<Microsoft.SemanticKernel.KernelPlugin> CreateKernelPlugins();
}

public sealed class ToolDescriptor
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string InputSchemaJson { get; init; } = string.Empty;
    public int KernelFunctionCount { get; init; }
    public string Risk { get; init; } = string.Empty;
}

public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, (ITool Tool, ToolDescriptor Descriptor)> _tools = new();

    public void Register(ITool tool)
    {
        if (string.IsNullOrWhiteSpace(tool.Name))
            throw new ArgumentException("Tool name 不能为空", nameof(tool));

        // 扫描工具类上的 [KernelFunction] 方法
        var kernelFunctionMethods = tool.GetType()
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.GetCustomAttributes(typeof(Microsoft.SemanticKernel.KernelFunctionAttribute), false).Any())
            .ToArray();

        if (kernelFunctionMethods.Length == 0)
            throw new InvalidOperationException(
                $"工具 {tool.Name} 上没有 [KernelFunction] 方法。" +
                $"请在至少一个公开实例方法上加 [KernelFunction(\"function_name\")] 标注。");

        var descriptor = new ToolDescriptor
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchemaJson = tool.InputSchemaJson,
            KernelFunctionCount = kernelFunctionMethods.Length,
            Risk = tool.Risk.ToString()
        };

        _tools[tool.Name] = (tool, descriptor);
    }

    public ITool? Get(string name) =>
        _tools.TryGetValue(name, out var entry) ? entry.Tool : null;

    public IReadOnlyList<string> ListNames() =>
        _tools.Keys.OrderBy(k => k).ToList();

    public IReadOnlyList<ToolDescriptor> ListTools() =>
        _tools.Values.Select(e => e.Descriptor).OrderBy(d => d.Name).ToList();

    public IReadOnlyList<Microsoft.SemanticKernel.KernelPlugin> CreateKernelPlugins()
    {
        var plugins = new List<Microsoft.SemanticKernel.KernelPlugin>();
        foreach (var (tool, _) in _tools.Values.OrderBy(e => e.Tool.Name))
        {
            // SK 直接扫描实例对象的所有 [KernelFunction] 方法
            var plugin = Microsoft.SemanticKernel.KernelPluginFactory.CreateFromObject(
                tool,
                pluginName: tool.Name);
            plugins.Add(plugin);
        }
        return plugins;
    }
}