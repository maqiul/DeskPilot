using DeskPilot.Core.Tools;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DeskPilot.Core.Services;

/// <summary>
/// 工具权限服务。
///
/// 核心逻辑：
/// - 记录用户是否开启了"危险操作需确认"
/// - 危险工具首次调用时拦截，缓存确认凭证
/// - 用户确认后再次调用同一工具+参数 → 放行
/// </summary>
public interface IToolPermissionService
{
    /// <summary>是否需要确认。设置里开关。</summary>
    bool RequireConfirmation { get; set; }

    /// <summary>
    /// 检查一个工具调用是否需要拦截。
    /// - 如果工具 Safe → 放行
    /// - 如果工具 Destructive + RequireConfirmation=false → 放行
    /// - 如果工具 Destructive + RequireConfirmation=true + 已确认 → 消费凭证，放行
    /// - 如果工具 Destructive + RequireConfirmation=true + 未确认 → 拦截，生成凭证
    /// </summary>
    /// <returns>null = 放行，非 null = 需要确认（返回确认提示文本）</returns>
    string? CheckAndTrack(string toolName, string argumentsJson);
}

public sealed class ToolPermissionService : IToolPermissionService
{
    private readonly ConcurrentDictionary<string, byte> _confirmedCalls = new();
    public bool RequireConfirmation { get; set; } = true;

    public string? CheckAndTrack(string toolName, string argumentsJson)
    {
        var hash = HashArgs(argumentsJson);
        var key = $"{toolName}|{hash}";

        // 已确认 → 消费凭证，放行
        if (_confirmedCalls.TryRemove(key, out _))
            return null;

        // 需要确认 → 生成凭证，拦截
        if (RequireConfirmation)
        {
            _confirmedCalls.TryAdd(key, 1);
            return $"⚠️ 即将执行危险操作：{toolName}\n" +
                   $"参数：{Truncate(argumentsJson, 300)}\n\n" +
                   "请回复「确认」继续，或回复「取消」放弃。";
        }

        return null; // 开关关闭，放行
    }

    /// <summary>
    /// 将 argumentsJson 的参数重新注入到工具调用时使用的 key 中。
    /// 供 ToolCallObserver 使用。
    /// </summary>
    public string? ConsumeConfirmation(string toolName, string argumentsJson)
    {
        var hash = HashArgs(argumentsJson);
        var key = $"{toolName}|{hash}";
        return _confirmedCalls.TryRemove(key, out _) ? null : "需要确认";
    }

    private static string HashArgs(string json)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes)[..16]; // 16 字符足够
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 3)] + "...";
}
