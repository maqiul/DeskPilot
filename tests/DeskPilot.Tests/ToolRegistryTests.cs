using DeskPilot.Core.Tools;

namespace DeskPilot.Tests;

/// <summary>
/// 简单的 stub 工具，用于测试 ToolRegistry。
/// 包含一个 [KernelFunction] 方法让 Registry 验证通过。
/// </summary>
internal sealed class StubKernelTool : ITool
{
    public string Name => "stub_tool";
    public string Description => "用于测试";
    public string InputSchemaJson => "{}";

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
        => Task.FromResult(ToolResult.Ok("stub executed"));

    [Microsoft.SemanticKernel.KernelFunction("stub_function")]
    public Task<string> StubKernelMethodAsync(string input)
        => Task.FromResult($"stub:{input}");
}

internal sealed class NoKernelFunctionTool : ITool
{
    public string Name => "no_kf_tool";
    public string Description => "测试无效工具";
    public string InputSchemaJson => "{}";

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
        => Task.FromResult(ToolResult.Ok("x"));

    // 故意不加 [KernelFunction]
    public Task<string> SomeMethodAsync(string input) => Task.FromResult(input);
}

internal sealed class EmptyNameTool : ITool
{
    public string Name => ""; // 故意为空
    public string Description => "";
    public string InputSchemaJson => "{}";

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
        => Task.FromResult(ToolResult.Ok("x"));

    [Microsoft.SemanticKernel.KernelFunction("f")]
    public Task<string> FAsync() => Task.FromResult("f");
}

public sealed class ToolRegistryTests
{
    [Fact]
    public void Register_ValidTool_Stores()
    {
        var registry = new ToolRegistry();
        registry.Register(new StubKernelTool());

        Assert.Single(registry.ListTools());
        Assert.Equal("stub_tool", registry.Get("stub_tool")!.Name);
    }

    [Fact]
    public void Register_ToolWithoutKernelFunction_Throws()
    {
        var registry = new ToolRegistry();
        var ex = Assert.Throws<InvalidOperationException>(() => registry.Register(new NoKernelFunctionTool()));
        Assert.Contains("KernelFunction", ex.Message);
    }

    [Fact]
    public void Register_EmptyName_Throws()
    {
        var registry = new ToolRegistry();
        Assert.Throws<ArgumentException>(() => registry.Register(new EmptyNameTool()));
    }

    [Fact]
    public void Get_UnknownName_ReturnsNull()
    {
        var registry = new ToolRegistry();
        Assert.Null(registry.Get("missing"));
    }

    [Fact]
    public void ListNames_SortedAlphabetically()
    {
        var registry = new ToolRegistry();
        registry.Register(new ArchiveByDateTool());
        registry.Register(new StubKernelTool());

        var names = registry.ListNames();
        Assert.Equal(2, names.Count);
        Assert.Equal("archive_files_by_date", names[0]);
        Assert.Equal("stub_tool", names[1]);
    }

    [Fact]
    public void ListTools_ContainsDescriptorMetadata()
    {
        var registry = new ToolRegistry();
        registry.Register(new ArchiveByDateTool());

        var tool = registry.ListTools().Single();
        Assert.Equal("archive_files_by_date", tool.Name);
        Assert.Contains("按文件日期", tool.Description);
        Assert.Contains("sourceDirectory", tool.InputSchemaJson);
        Assert.True(tool.KernelFunctionCount > 0);
    }

    [Fact]
    public void CreateKernelPlugins_ReturnsPluginPerTool()
    {
        var registry = new ToolRegistry();
        registry.Register(new ArchiveByDateTool());
        registry.Register(new StubKernelTool());

        var plugins = registry.CreateKernelPlugins();
        Assert.Equal(2, plugins.Count);
        // 每个 plugin 内至少有一个 function（KernelFunction 方法）
        Assert.All(plugins, p => Assert.NotEmpty(p.GetFunctionsMetadata()));
    }
}