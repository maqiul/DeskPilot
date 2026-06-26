using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Models;
using DeskPilot.Core.Tools;

namespace DeskPilot.Core.Services;

/// <summary>v0.12: 单步执行状态（推送给 UI 用）。</summary>
public enum StepStatus
{
    Pending,
    Running,
    Done,
    Error,
    Skipped
}

/// <summary>v0.12: 单步进度（绑定 UI 用）。</summary>
public sealed class StepProgress
{
    public int Index { get; init; }
    public string ToolName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public string Summary { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public string StatusIcon => Status switch
    {
        StepStatus.Running => "⏳",
        StepStatus.Done => "✅",
        StepStatus.Error => "❌",
        StepStatus.Skipped => "⏭",
        _ => "⚪",
    };
}

/// <summary>v0.12: 多步执行结果。</summary>
public sealed class SkillExecutionResult
{
    public bool AllSuccess { get; init; }
    public int TotalSteps { get; init; }
    public int SuccessSteps { get; init; }
    public int FailedStepIndex { get; init; } = -1;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<StepProgress> Steps { get; init; } = Array.Empty<StepProgress>();
}

/// <summary>v0.12: 技能多步执行器接口。</summary>
public interface ISkillExecutor
{
    Task<SkillExecutionResult> ExecuteAsync(
        Skill skill,
        IProgress<StepProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>v0.12: 默认实现 — 按 Skill.Steps 顺序调用 IToolRegistry 中的工具。
/// 任何 step 失败：若 Optional 则继续；否则整体中断。</summary>
public sealed class SkillExecutor : ISkillExecutor
{
    private readonly IToolRegistry _registry;

    public SkillExecutor(IToolRegistry registry)
    {
        _registry = registry;
    }

    public async Task<SkillExecutionResult> ExecuteAsync(
        Skill skill,
        IProgress<StepProgress>? progress = null,
        CancellationToken ct = default)
    {
        var steps = skill.SafeSteps;
        if (steps.Count == 0)
        {
            return new SkillExecutionResult
            {
                AllSuccess = false,
                TotalSteps = 0,
                Summary = $"技能 '{skill.Name}' 没有定义步骤（Steps 为空），请用 PromptTemplate 路径触发",
            };
        }

        var progresses = steps.Select((s, i) => new StepProgress
        {
            Index = i + 1,
            ToolName = s.ToolName,
            Description = string.IsNullOrWhiteSpace(s.Description) ? s.ToolName : s.Description,
        }).ToList();

        int successCount = 0;
        int failedIdx = -1;
        bool allOk = true;

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var p = progresses[i];

            if (ct.IsCancellationRequested)
            {
                p.Status = StepStatus.Skipped;
                p.Summary = "用户取消";
                progress?.Report(p);
                allOk = false;
                failedIdx = i;
                break;
            }

            p.Status = StepStatus.Running;
            progress?.Report(p);

            var tool = _registry.Get(step.ToolName);
            if (tool == null)
            {
                p.Status = StepStatus.Error;
                p.ErrorMessage = $"工具 '{step.ToolName}' 未注册";
                p.Summary = p.ErrorMessage;
                progress?.Report(p);
                if (!step.Optional)
                {
                    allOk = false;
                    failedIdx = i;
                    break;
                }
                continue;
            }

            try
            {
                var argsJson = step.Args == null
                    ? "{}"
                    : JsonSerializer.Serialize(step.Args);
                var result = await tool.ExecuteAsync(argsJson, ct).ConfigureAwait(false);
                if (result.Success)
                {
                    p.Status = StepStatus.Done;
                    p.Summary = result.Summary;
                    successCount++;
                }
                else
                {
                    p.Status = StepStatus.Error;
                    p.ErrorMessage = result.ErrorMessage;
                    p.Summary = result.Summary;
                    if (!step.Optional)
                    {
                        allOk = false;
                        failedIdx = i;
                        progress?.Report(p);
                        break;
                    }
                }
                progress?.Report(p);
            }
            catch (Exception ex)
            {
                p.Status = StepStatus.Error;
                p.ErrorMessage = ex.Message;
                p.Summary = $"异常：{ex.Message}";
                progress?.Report(p);
                if (!step.Optional)
                {
                    allOk = false;
                    failedIdx = i;
                    break;
                }
            }
        }

        var summary = allOk
            ? $"✅ 技能 {skill.Name} 完成，{successCount}/{steps.Count} 步全部成功"
            : $"⚠️ 技能 {skill.Name} 中断于第 {failedIdx + 1} 步，成功 {successCount}/{steps.Count}";

        return new SkillExecutionResult
        {
            AllSuccess = allOk,
            TotalSteps = steps.Count,
            SuccessSteps = successCount,
            FailedStepIndex = failedIdx,
            Summary = summary,
            Steps = progresses,
        };
    }
}