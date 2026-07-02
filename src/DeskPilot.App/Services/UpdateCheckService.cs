using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeskPilot.App.Services;

/// <summary>
/// v0.23.0: 自动检查更新服务（GitHub Releases API）
/// 通过 https://api.github.com/repos/maqiul/DeskPilot/releases/latest 获取最新版本号
/// 与当前程序集版本号比较，决定是否提示更新
/// </summary>
public sealed class UpdateCheckService
{
    private const string ReleasesUrl = "https://api.github.com/repos/maqiul/DeskPilot/releases/latest";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public string CurrentVersion { get; }

    public UpdateCheckService()
    {
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        CurrentVersion = ver != null
            ? $"{ver.Major}.{ver.Minor}.{ver.Build}"
            : "0.0.0";
    }

    /// <summary>
    /// 从 GitHub 获取最新版本号，失败时返回 null
    /// </summary>
    public async Task<string?> GetLatestVersionAsync()
    {
        try
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("DeskPilot-UpdateChecker");
            var json = await Http.GetStringAsync(ReleasesUrl);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("tag_name", out var tag))
            {
                var raw = tag.GetString() ?? string.Empty;
                // 去掉 "v" 前缀（如 "v0.23.0" → "0.23.0"）
                return raw.TrimStart('v', 'V');
            }
        }
        catch
        {
            // 网络失败、API 限流、JSON 解析错误统一吞掉
        }
        return null;
    }

    /// <summary>
    /// 比较 latest 是否比 current 新（用 SemanticVersion 数值比较）
    /// </summary>
    public static bool IsNewer(string latest, string current)
    {
        if (!Version.TryParse(latest, out var latestVer)) return false;
        if (!Version.TryParse(current, out var currentVer)) return false;
        return latestVer > currentVer;
    }
}