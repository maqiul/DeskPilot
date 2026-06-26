using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskPilot.Core.Models;
using DeskPilot.Core.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DeskPilot.App.ViewModels;

/// <summary>
/// v0.11: 技能详情弹窗 ViewModel。
/// 显示完整 Description + Prompt 模板预览 + Tools 列表 + 安装/卸载/检查更新按钮。
/// Prompt 预览为只读 TextBox + 复制按钮。
/// </summary>
public partial class SkillDetailViewModel : ObservableObject
{
    private readonly ISkillService? _skillService;
    private readonly ISkillMarket? _skillMarket;
    private readonly MarketSkillRow _row;
    private Skill? _fullSkill;

    public SkillDetailViewModel(MarketSkillRow row, ISkillService? skillService, ISkillMarket? skillMarket)
    {
        _row = row;
        _skillService = skillService;
        _skillMarket = skillMarket;

        // 基础信息直接来自 row（卡片已有）
        Id = row.Id;
        Name = row.Name;
        Description = row.Description;
        Icon = row.Icon;
        Category = row.Category;
        Author = row.AuthorName;
        Version = row.Version;
        Rating = row.Rating;
        Downloads = row.Downloads;
        ScreenshotUrl = row.ScreenshotUrl;
        IsInstalled = row.IsInstalled;
        HasUpdate = row.HasUpdate;
        StatusMessage = "🔄 正在拉取完整技能...";

        // 异步加载完整技能（带 PromptTemplate + Tools）
        _ = LoadFullSkillAsync();
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Icon { get; }
    public string Category { get; }
    public string Author { get; }
    public string Version { get; }
    public double Rating { get; }
    public int Downloads { get; }
    public string ScreenshotUrl { get; }

    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private bool _hasUpdate;
    [ObservableProperty] private bool _isWorking;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private string _promptPreview = string.Empty;
    public ObservableCollection<string> Tools { get; } = new();

    [ObservableProperty] private string _ratingText = string.Empty;
    [ObservableProperty] private string _downloadsText = string.Empty;

    private async Task LoadFullSkillAsync()
    {
        if (_skillMarket == null)
        {
            StatusMessage = "⚠️ 市场服务未配置，无法加载完整技能";
            return;
        }

        try
        {
            _fullSkill = await _skillMarket.FetchSkillAsync(_row.Id).ConfigureAwait(true);
            PromptPreview = _fullSkill.PromptTemplate;
            Tools.Clear();
            foreach (var t in _fullSkill.Tools) Tools.Add(t);
            RatingText = Rating > 0 ? $"★ {Rating:F1}" : "★ -";
            DownloadsText = Downloads > 0 ? $"📥 {Downloads:N0} 次下载" : "📥 -";
            StatusMessage = "✅ 已加载完整技能";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 加载失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyPrompt()
    {
        if (string.IsNullOrEmpty(PromptPreview)) return;
        try
        {
            System.Windows.Clipboard.SetText(PromptPreview);
            StatusMessage = "✅ 已复制 Prompt 到剪贴板";
        }
        catch
        {
            StatusMessage = "⚠️ 复制失败（剪贴板不可用）";
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (_skillService == null || _fullSkill == null) return;
        IsWorking = true;
        try
        {
            await _skillService.InstallAsync(_fullSkill).ConfigureAwait(true);
            IsInstalled = true;
            HasUpdate = false;
            _row.IsInstalled = true;
            StatusMessage = $"✅ 已安装 {Name} v{Version}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 安装失败：{ex.Message}";
        }
        finally
        {
            IsWorking = false;
        }
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        if (_skillService == null) return;
        IsWorking = true;
        try
        {
            await _skillService.UninstallAsync(_row.Id).ConfigureAwait(true);
            IsInstalled = false;
            _row.IsInstalled = false;
            StatusMessage = $"✅ 已卸载 {Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 卸载失败：{ex.Message}";
        }
        finally
        {
            IsWorking = false;
        }
    }
}