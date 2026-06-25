using System.Net;
using DeskPilot.App.Models;
using DeskPilot.App.ViewModels;
using DeskPilot.Core.Models;
using DeskPilot.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// SettingsViewModel 的 RefreshModelsCommand 测试。
/// </summary>
public class SettingsViewModelRefreshTests
{
    [Fact]
    public async Task RefreshModels_PopulatesList_AndCachesToSettings()
    {
        // Arrange
        var service = new InMemorySettingsService();
        var lister = new StubModelLister(new[]
        {
            new ModelInfo("gpt-4o", "GPT-4o", "openai"),
            new ModelInfo("gpt-4o-mini", "GPT-4o Mini", "openai"),
            new ModelInfo("gpt-3.5-turbo", "GPT-3.5 Turbo", "openai")
        });
        var factory = new StubModelListerFactory(lister);
        var vm = new SettingsViewModel(service, factory);

        // Act
        await vm.RefreshModelsAsync();

        // Assert: 3 个 live + fallback 去重后 = 6（fallback 全包含 live 的 3 个）
        Assert.Equal(6, vm.CurrentModelList.Count);
        Assert.Contains(vm.CurrentModelList, m => m.Id == "gpt-4o");
        Assert.Contains(vm.CurrentModelList, m => m.Id == "o1-mini"); // 来自 fallback
        Assert.NotNull(service.Stored.CachedModels);
        Assert.True(service.Stored.CachedModels.ContainsKey("OpenAI"));
        Assert.Equal(3, service.Stored.CachedModels["OpenAI"].Count); // 缓存只存 live
    }

    [Fact]
    public async Task RefreshModels_EmptyResult_KeepsExistingList_AndShowsError()
    {
        var service = new InMemorySettingsService();
        var lister = new StubModelLister(Array.Empty<ModelInfo>()); // 网络失败返回空
        var factory = new StubModelListerFactory(lister);
        var vm = new SettingsViewModel(service, factory);

        var initialCount = vm.CurrentModelList.Count; // fallback 6 个
        await vm.RefreshModelsAsync();

        // 失败时保留 fallback 列表，不清空
        Assert.Equal(initialCount, vm.CurrentModelList.Count);
        Assert.True(vm.IsError);
    }

    [Fact]
    public async Task RefreshModels_MergesWithFallback()
    {
        var service = new InMemorySettingsService();
        var lister = new StubModelLister(new[]
        {
            new ModelInfo("gpt-4o", OwnedBy: "openai"),
            new ModelInfo("claude-3.5-sonnet", OwnedBy: "anthropic") // 不在 fallback
        });
        var factory = new StubModelListerFactory(lister);
        var vm = new SettingsViewModel(service, factory);

        await vm.RefreshModelsAsync();

        // 2 个动态 (gpt-4o, claude) + 6 个 fallback 去重 → 7 个
        // (gpt-4o 在 live 和 fallback 各 1 次, 实际加 1 个新 = claude)
        Assert.Equal(7, vm.CurrentModelList.Count);
        Assert.Equal("gpt-4o", vm.CurrentModelList[0].Id);
        Assert.Equal("claude-3.5-sonnet", vm.CurrentModelList[1].Id);
    }

    [Fact]
    public void Constructor_ForOpenAi_LoadsFallbackList()
    {
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service, new StubModelListerFactory(new StubModelLister(Array.Empty<ModelInfo>())));

        Assert.Equal(AiProvider.OpenAI, vm.Provider);
        Assert.Equal(6, vm.CurrentModelList.Count); // OpenAI fallback 6 个
    }

    [Fact]
    public void Constructor_ForOllama_LoadsEmptyList()
    {
        var service = new InMemorySettingsService { Stored = new AppSettings { Provider = AiProvider.Ollama } };
        var vm = new SettingsViewModel(service, new StubModelListerFactory(new StubModelLister(Array.Empty<ModelInfo>())));

        Assert.Equal(AiProvider.Ollama, vm.Provider);
        Assert.Empty(vm.CurrentModelList); // Ollama 无 fallback
    }

    [Fact]
    public void ChangeProvider_UpdatesModelList()
    {
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service, new StubModelListerFactory(new StubModelLister(Array.Empty<ModelInfo>())));

        // 默认 OpenAI → 6 个
        Assert.Equal(6, vm.CurrentModelList.Count);

        vm.Provider = AiProvider.DeepSeek;
        Assert.Equal(3, vm.CurrentModelList.Count); // DeepSeek fallback

        vm.Provider = AiProvider.Ollama;
        Assert.Empty(vm.CurrentModelList);
    }
}

/// <summary>
/// Stub IModelLister：返回预设模型列表（也可用于模拟空响应 = 网络失败）。
/// </summary>
internal sealed class StubModelLister : IModelLister
{
    private readonly IReadOnlyList<ModelInfo> _models;
    public StubModelLister(IReadOnlyList<ModelInfo> models) => _models = models;
    public string ProviderName => "stub";
    public Task<IReadOnlyList<ModelInfo>> ListAsync(string? apiKey = null, string? endpoint = null, CancellationToken ct = default)
        => Task.FromResult(_models);
}

/// <summary>
/// Stub factory：固定返回一个 Lister。
/// </summary>
internal sealed class StubModelListerFactory : IModelListerFactory
{
    private readonly IModelLister _lister;
    public StubModelListerFactory(IModelLister lister) => _lister = lister;
    public IModelLister GetLister(AiProvider provider) => _lister;
}

/// <summary>
/// 包装 RelayCommand 以便测试异步命令。
/// </summary>
internal static class RelayCommandStub
{
    public static Task ExecuteAsync(this CommunityToolkit.Mvvm.Input.IAsyncRelayCommand cmd, object? parameter)
        => cmd.ExecuteAsync(parameter);
}