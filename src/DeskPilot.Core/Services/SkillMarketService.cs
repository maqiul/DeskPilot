using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Models;

namespace DeskPilot.Core.Services;

/// <summary>
/// v0.10: 技能市场默认实现（GitHub raw）。
/// 索引：拉 skills/README.md，解析 Markdown 表格
/// 技能：拉 skills/{id}.json
/// HttpClient 注入：测试用 DelegatingHandler mock。
/// </summary>
public sealed class SkillMarketService : ISkillMarket
{
    public string BaseUrl { get; }

    private readonly HttpClient _http;

    public SkillMarketService(HttpClient http, string baseUrl =
        "https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills")
    {
        _http = http;
        BaseUrl = baseUrl.TrimEnd('/');
        if (_http.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
            _http.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<SkillIndex> FetchIndexAsync(CancellationToken ct = default)
    {
        var readmeUrl = $"{BaseUrl}/README.md";
        string md;
        try
        {
            md = await _http.GetStringAsync(readmeUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new MarketFetchException($"拉取市场索引失败：{readmeUrl}", ex);
        }
        return ParseIndexFromMarkdown(md);
    }

    public async Task<Skill> FetchSkillAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new SkillNotFoundException(id ?? "");

        var url = $"{BaseUrl}/{id}.json";
        string json;
        try
        {
            json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase))
        {
            throw new SkillNotFoundException(id);
        }
        catch (Exception ex)
        {
            throw new MarketFetchException($"拉取技能 {id} 失败：{url}", ex);
        }

        try
        {
            var skill = JsonSerializer.Deserialize<Skill>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (skill == null || string.IsNullOrWhiteSpace(skill.Id))
                throw new MarketFetchException($"技能 {id} JSON 解析为空");
            // 确保 Source 标记为市场
            if (string.IsNullOrWhiteSpace(skill.Source))
                skill = skill with { Source = "market:community" };
            return skill;
        }
        catch (JsonException ex)
        {
            throw new MarketFetchException($"技能 {id} JSON 格式错误", ex);
        }
    }

    public async Task<IReadOnlyDictionary<string, SkillUpdateInfo>> CheckUpdatesAsync(
        IEnumerable<Skill> installed, CancellationToken ct = default)
    {
        var installedList = installed.ToList();
        var result = new Dictionary<string, SkillUpdateInfo>();
        if (installedList.Count == 0) return result;

        // 拉一次市场索引
        SkillIndex index;
        try
        {
            index = await FetchIndexAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // 拉取失败 → 全部标记为不可更新（不抛，保持函数纯净）
            foreach (var s in installedList.Where(s => !s.IsBuiltIn))
                result[s.Id] = new SkillUpdateInfo(s.Id, s.Version, "", HasUpdate: false);
            return result;
        }

        foreach (var s in installedList.Where(s => !s.IsBuiltIn))
        {
            var manifest = index.FindById(s.Id);
            if (manifest == null)
            {
                result[s.Id] = new SkillUpdateInfo(s.Id, s.Version, "", HasUpdate: false);
                continue;
            }
            var hasUpdate = CompareVersions(s.Version, manifest.Version) < 0;
            result[s.Id] = new SkillUpdateInfo(s.Id, s.Version, manifest.Version, hasUpdate);
        }
        return result;
    }

    // --- 内部：Markdown 表格解析 ---

    /// <summary>
    /// 解析 skills/README.md 的 Markdown 表格为 SkillIndex。
    /// 表格格式：| id | name | description | icon | category | author | version |
    /// 行至少 7 列；空行 / 不以 | 起头 跳过。
    /// </summary>
    public static SkillIndex ParseIndexFromMarkdown(string md)
    {
        var index = new SkillIndex();
        if (string.IsNullOrWhiteSpace(md)) return index;

        foreach (var rawLine in md.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("|")) continue;
            if (line.Contains("---")) continue;  // 跳过分隔行 | --- | --- |
            if (line.StartsWith("| id ") || line.StartsWith("|---")) continue;  // 跳表头

            var cells = line.Split('|')
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .ToList();
            // 期望 7 列：id name description icon category author version
            if (cells.Count < 7) continue;

            try
            {
                var manifest = new SkillManifest(
                    Id: cells[0],
                    Name: cells[1],
                    Description: cells[2],
                    Icon: cells[3],
                    Category: cells[4],
                    Author: cells[5],
                    Version: cells[6],
                    Tags: Array.Empty<string>());
                if (string.IsNullOrWhiteSpace(manifest.Id)) continue;
                index.Skills.Add(manifest);
            }
            catch
            {
                // 单行解析失败不影响其他行
            }
        }
        return index;
    }

    /// <summary>语义化版本比较：v1 < v2 返回 -1；v1 == v2 返回 0；v1 > v2 返回 1。</summary>
    public static int CompareVersions(string? v1, string? v2)
    {
        if (string.IsNullOrWhiteSpace(v1) && string.IsNullOrWhiteSpace(v2)) return 0;
        if (string.IsNullOrWhiteSpace(v1)) return -1;
        if (string.IsNullOrWhiteSpace(v2)) return 1;

        var p1 = v1.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var p2 = v2.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var len = Math.Max(p1.Length, p2.Length);
        for (int i = 0; i < len; i++)
        {
            var a = i < p1.Length ? p1[i] : 0;
            var b = i < p2.Length ? p2[i] : 0;
            if (a < b) return -1;
            if (a > b) return 1;
        }
        return 0;
    }
}
