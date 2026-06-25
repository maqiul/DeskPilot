using DeskPilot.App.Models;
using DeskPilot.App.Services;
using DeskPilot.App.ViewModels;
using System.Collections.Generic;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// SettingsViewModel 的单元测试。
/// 用 InMemorySettingsService 替代 DPAPI，避免真实文件 IO。
/// </summary>
public class SettingsViewModelTests
{
    [Fact]
    public void Constructor_LoadsExistingSettings()
    {
        // Arrange
        var service = new InMemorySettingsService
        {
            Stored = new AppSettings
            {
                Provider = AiProvider.DeepSeek,
                DeepSeekApiKey = "sk-test-key",
                DeepSeekModel = "deepseek-reasoner"
            }
        };

        // Act
        var vm = new SettingsViewModel(service);

        // Assert
        Assert.Equal(AiProvider.DeepSeek, vm.Provider);
        Assert.Equal("sk-test-key", vm.DeepSeekApiKey);
        Assert.Equal("deepseek-reasoner", vm.DeepSeekModel);
        Assert.True(vm.IsDeepSeekSelected);
        Assert.False(vm.IsOpenAiSelected);
        Assert.Equal(1, service.LoadCallCount);
    }

    [Fact]
    public void Provider_Change_UpdatesComputedProperties()
    {
        // Arrange
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service);

        // Act
        vm.Provider = AiProvider.Ollama;

        // Assert
        Assert.True(vm.IsOllamaSelected);
        Assert.False(vm.IsOpenAiSelected);
        Assert.False(vm.IsDeepSeekSelected);
        Assert.False(vm.ShowApiKey); // Ollama 不需要 Key
    }

    [Fact]
    public void ShowApiKey_TrueForOpenAI_TrueForDeepSeek_FalseForOllama()
    {
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service);

        vm.Provider = AiProvider.OpenAI;
        Assert.True(vm.ShowApiKey);

        vm.Provider = AiProvider.DeepSeek;
        Assert.True(vm.ShowApiKey);

        vm.Provider = AiProvider.Ollama;
        Assert.False(vm.ShowApiKey);
    }

    [Fact]
    public void ToggleApiKeyVisibility_FlipsFlag()
    {
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service);

        Assert.False(vm.IsApiKeyVisible);
        vm.ToggleApiKeyVisibilityCommand.Execute(null);
        Assert.True(vm.IsApiKeyVisible);
        vm.ToggleApiKeyVisibilityCommand.Execute(null);
        Assert.False(vm.IsApiKeyVisible);
    }

    [Fact]
    public void Validate_OpenAIWithoutKey_ReturnsFalse()
    {
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service)
        {
            Provider = AiProvider.OpenAI,
            OpenAiApiKey = ""  // 空 Key
        };

        var ok = vm.Validate(out var error);

        Assert.False(ok);
        Assert.Contains("OpenAI", error);
        Assert.Contains("API Key", error);
    }

    [Fact]
    public void Validate_OpenAIWithKey_ReturnsTrue()
    {
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service)
        {
            Provider = AiProvider.OpenAI,
            OpenAiApiKey = "sk-valid"
        };

        var ok = vm.Validate(out var error);

        Assert.True(ok);
        Assert.Empty(error);
    }

    [Fact]
    public void Validate_OllamaWithoutKey_ReturnsTrue()
    {
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service)
        {
            Provider = AiProvider.Ollama
            // 不需要 Key
        };

        var ok = vm.Validate(out var error);

        Assert.True(ok);
        Assert.Empty(error);
    }

    [Fact]
    public void BuildSettings_TrimsWhitespace_AppliesDefaults()
    {
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service)
        {
            Provider = AiProvider.DeepSeek,
            DeepSeekApiKey = "  sk-abc  ",
            DeepSeekModel = "",  // 空 → 应回退默认
            OllamaEndpoint = "  ",  // 全空格 → 应回退默认
            OllamaModel = "custom-model"
        };

        var s = vm.BuildSettings();

        Assert.Equal(AiProvider.DeepSeek, s.Provider);
        Assert.Equal("sk-abc", s.DeepSeekApiKey);
        Assert.Equal("deepseek-chat", s.DeepSeekModel); // 默认值
        Assert.Equal("http://localhost:11434", s.OllamaEndpoint); // 默认值
        Assert.Equal("custom-model", s.OllamaModel);
    }

    [Fact]
    public void Save_ValidSettings_PersistsAndFiresEvent()
    {
        // Arrange
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service, closeWindow: null)
        {
            Provider = AiProvider.OpenAI,
            OpenAiApiKey = "sk-newkey",
            OpenAiModel = "gpt-4o"
        };

        AppSettings? eventArg = null;
        int eventCount = 0;
        vm.ChatServiceChanged += (_, s) => { eventArg = s; eventCount++; };

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        Assert.Equal(1, service.SaveCallCount);
        Assert.Equal(1, eventCount);
        Assert.NotNull(eventArg);
        Assert.Equal(AiProvider.OpenAI, eventArg!.Provider);
        Assert.Equal("sk-newkey", eventArg.OpenAiApiKey);
        Assert.Equal("gpt-4o", eventArg.OpenAiModel);

        // 验证状态条
        Assert.True(vm.HasStatus);
        Assert.False(vm.IsError);
        Assert.Contains("OpenAI", vm.StatusMessage);

        // 验证 InMemory 服务已更新
        Assert.Equal("sk-newkey", service.Stored.OpenAiApiKey);
    }

    [Fact]
    public void Save_InvalidSettings_DoesNotPersist_DoesNotFireEvent()
    {
        // Arrange
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service, closeWindow: null)
        {
            Provider = AiProvider.OpenAI,
            OpenAiApiKey = ""  // 无效
        };

        int eventCount = 0;
        vm.ChatServiceChanged += (_, _) => eventCount++;

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        Assert.Equal(0, service.SaveCallCount);
        Assert.Equal(0, eventCount);
        Assert.True(vm.IsError);
        Assert.Contains("API Key", vm.StatusMessage);
    }

    [Fact]
    public void Cancel_InvokesCloseWindowCallback()
    {
        var service = new InMemorySettingsService();
        int closeCount = 0;
        var vm = new SettingsViewModel(service, closeWindow: () => closeCount++);

        vm.CancelCommand.Execute(null);

        Assert.Equal(1, closeCount);
    }

    [Fact]
    public void Cancel_WithNullCallback_DoesNotThrow()
    {
        var service = new InMemorySettingsService();
        var vm = new SettingsViewModel(service, closeWindow: null);

        // 不应抛异常
        var ex = Record.Exception(() => vm.CancelCommand.Execute(null));
        Assert.Null(ex);
    }

    [Fact]
    public void DefaultSettings_LoadedOnFirstRun()
    {
        var service = new InMemorySettingsService
        {
            Stored = AppSettings.Default
        };
        var vm = new SettingsViewModel(service);

        Assert.Equal(AiProvider.OpenAI, vm.Provider);
        Assert.Equal("gpt-4o-mini", vm.OpenAiModel);
        Assert.Equal("deepseek-chat", vm.DeepSeekModel);
        Assert.Equal("http://localhost:11434", vm.OllamaEndpoint);
        Assert.Equal("qwen2.5:7b", vm.OllamaModel);
        Assert.Empty(vm.OpenAiApiKey);
        Assert.Empty(vm.DeepSeekApiKey);
    }
}