using DeskPilot.Core.Models;
using DeskPilot.Core.Services;
using DeskPilot.Core.Tools;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.12 SkillExecutor 测试。
/// 覆盖：空 Steps / 单步成功 / 多步成功 / Optional 失败继续 / Required 失败中断 / 工具未注册 / IProgress 推送 / cancel。
/// </summary>
public class SkillExecutorTests
{
    /// <summary>测试用工具：返回指定结果，可选抛异常。</summary>
    private sealed class FakeTool : ITool
    {
        public string Name { get; set; } = "FakeTool";
        public string Description => "test";
        public string InputSchemaJson => "{}";
        public RiskLevel Risk => RiskLevel.Safe;
        public Func<string, ToolResult>? Handler { get; set; }

        // ToolRegistry.Register 强制要求至少一个 [KernelFunction] 方法
        [Microsoft.SemanticKernel.KernelFunction("run")]
        public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
            => Task.FromResult(Handler?.Invoke(argumentsJson) ?? ToolResult.Ok("ok"));
    }

    private static IToolRegistry BuildRegistry(params (string name, Func<string, ToolResult> handler)[] tools)
    {
        var reg = new ToolRegistry();
        foreach (var (name, handler) in tools)
        {
            var t = new FakeTool { Name = name, Handler = handler };
            reg.Register(t);
        }
        return reg;
    }

    private static Skill BuildSkill(params (string tool, Dictionary<string, object?> args, bool optional)[] steps)
    {
        var stepList = new List<SkillStep>();
        for (int i = 0; i < steps.Length; i++)
        {
            var s = steps[i];
            stepList.Add(new SkillStep(s.tool, s.args, $"step {i + 1}", s.optional));
        }
        return new Skill(
            Id: "test-skill",
            Name: "Test Skill",
            Description: "for testing",
            Icon: "🧪",
            PromptTemplate: "",
            Tools: new List<string>(),
            Category: "test",
            IsEnabled: true,
            IsBuiltIn: true,
            Source: "builtin",
            Version: "1.0.0",
            Steps: stepList);
    }

    [Fact]
    public void SkillExecutor_EmptySteps_ReturnsFailure()
    {
        var reg = BuildRegistry();
        var exec = new SkillExecutor(reg);
        var skill = new Skill(
            Id: "x",
            Name: "X",
            Description: "",
            Icon: "🧪",
            PromptTemplate: "",
            Tools: new List<string>(),
            Category: "test",
            IsEnabled: true,
            IsBuiltIn: true,
            Source: "builtin",
            Version: "1.0.0",
            Steps: new List<SkillStep>());
        var result = exec.ExecuteAsync(skill).GetAwaiter().GetResult();
        Assert.False(result.AllSuccess);
        Assert.Equal(0, result.TotalSteps);
        Assert.Contains("没有定义步骤", result.Summary);
    }

    [Fact]
    public void SkillExecutor_SingleStep_Success()
    {
        var reg = BuildRegistry(("Echo", args => ToolResult.Ok("echoed")));
        var exec = new SkillExecutor(reg);
        var skill = BuildSkill(("Echo", new Dictionary<string, object?>(), false));
        var result = exec.ExecuteAsync(skill).GetAwaiter().GetResult();
        Assert.True(result.AllSuccess);
        Assert.Equal(1, result.TotalSteps);
        Assert.Equal(1, result.SuccessSteps);
        Assert.Equal(StepStatus.Done, result.Steps[0].Status);
        Assert.Equal("echoed", result.Steps[0].Summary);
    }

    [Fact]
    public void SkillExecutor_MultiStep_AllSuccess()
    {
        var reg = BuildRegistry(
            ("A", _ => ToolResult.Ok("A done")),
            ("B", _ => ToolResult.Ok("B done")),
            ("C", _ => ToolResult.Ok("C done")));
        var exec = new SkillExecutor(reg);
        var skill = BuildSkill(
            ("A", new Dictionary<string, object?>(), false),
            ("B", new Dictionary<string, object?>(), false),
            ("C", new Dictionary<string, object?>(), false));
        var result = exec.ExecuteAsync(skill).GetAwaiter().GetResult();
        Assert.True(result.AllSuccess);
        Assert.Equal(3, result.TotalSteps);
        Assert.Equal(3, result.SuccessSteps);
        Assert.Equal(StepStatus.Done, result.Steps[2].Status);
    }

    [Fact]
    public void SkillExecutor_RequiredFails_StopsExecution()
    {
        var callCount = 0;
        var reg = BuildRegistry(
            ("A", _ => { callCount++; return ToolResult.Ok("A"); }),
            ("B", _ => ToolResult.Fail("B boom")),
            ("C", _ => { callCount++; return ToolResult.Ok("C"); }));
        var exec = new SkillExecutor(reg);
        var skill = BuildSkill(
            ("A", new Dictionary<string, object?>(), false),
            ("B", new Dictionary<string, object?>(), false),
            ("C", new Dictionary<string, object?>(), false));
        var result = exec.ExecuteAsync(skill).GetAwaiter().GetResult();
        Assert.False(result.AllSuccess);
        Assert.Equal(1, result.SuccessSteps);
        Assert.Equal(2, result.FailedStepIndex + 1);
        Assert.Equal(StepStatus.Error, result.Steps[1].Status);
        Assert.Equal(1, callCount); // C 没被执行
    }

    [Fact]
    public void SkillExecutor_OptionalFails_Continue()
    {
        var reg = BuildRegistry(
            ("A", _ => ToolResult.Ok("A")),
            ("B", _ => ToolResult.Fail("B 可选失败")),
            ("C", _ => ToolResult.Ok("C")));
        var exec = new SkillExecutor(reg);
        var skill = BuildSkill(
            ("A", new Dictionary<string, object?>(), false),
            ("B", new Dictionary<string, object?>(), true), // Optional
            ("C", new Dictionary<string, object?>(), false));
        var result = exec.ExecuteAsync(skill).GetAwaiter().GetResult();
        Assert.True(result.AllSuccess);
        Assert.Equal(3, result.TotalSteps);
        Assert.Equal(2, result.SuccessSteps);
        Assert.Equal(StepStatus.Error, result.Steps[1].Status);
        Assert.Equal(StepStatus.Done, result.Steps[2].Status);
    }

    [Fact]
    public void SkillExecutor_UnknownTool_RequiredFailsStopsOptionalContinues()
    {
        var cCalled = false;
        var reg = BuildRegistry(("C", _ => { cCalled = true; return ToolResult.Ok("C"); }));
        var exec = new SkillExecutor(reg);
        var skill = BuildSkill(
            ("Missing", new Dictionary<string, object?>(), false),   // Required → 停
            ("C", new Dictionary<string, object?>(), false));

        var result = exec.ExecuteAsync(skill).GetAwaiter().GetResult();
        Assert.False(result.AllSuccess);
        Assert.False(cCalled);
        Assert.Equal(StepStatus.Error, result.Steps[0].Status);
        Assert.Contains("未注册", result.Steps[0].ErrorMessage);

        // Optional 分支
        var skill2 = BuildSkill(
            ("Missing", new Dictionary<string, object?>(), true),
            ("C", new Dictionary<string, object?>(), false));
        var result2 = exec.ExecuteAsync(skill2).GetAwaiter().GetResult();
        Assert.True(result2.AllSuccess);
        Assert.True(cCalled);
    }

    [Fact]
    public void SkillExecutor_Progress_ReportsRunningThenDone()
    {
        var reg = BuildRegistry(("A", _ => ToolResult.Ok("A ok")));
        var exec = new SkillExecutor(reg);
        var skill = BuildSkill(("A", new Dictionary<string, object?>(), false));
        var reports = new List<StepStatus>();
        // 用同步回调收集（Progress<T> 异步推送时序不稳定，sync 模式更可靠）
        var progress = new SyncProgress<StepProgress>(p => reports.Add(p.Status));
        var result = exec.ExecuteAsync(skill, progress).GetAwaiter().GetResult();
        Assert.True(result.AllSuccess);
        Assert.Contains(StepStatus.Running, reports);
        Assert.Contains(StepStatus.Done, reports);
    }

    /// <summary>同步 IProgress 实现（保证回调在调用线程同步触发，不走 SynchronizationContext 异步队列）。</summary>
    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SyncProgress(Action<T> handler) { _handler = handler; }
        public void Report(T value) => _handler(value);
    }

    [Fact]
    public async Task SkillExecutor_Cancellation_SkipsRemaining()
    {
        var reg = BuildRegistry(
            ("A", _ => ToolResult.Ok("A")),
            ("B", _ => ToolResult.Ok("B")));
        var exec = new SkillExecutor(reg);
        var skill = BuildSkill(
            ("A", new Dictionary<string, object?>(), false),
            ("B", new Dictionary<string, object?>(), false));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await exec.ExecuteAsync(skill, ct: cts.Token);
        Assert.False(result.AllSuccess);
        Assert.Equal(0, result.SuccessSteps);
    }

    [Fact]
    public void SkillExecutor_ToolThrows_RequiredStopsOptionalContinues()
    {
        var cCalled = false;
        var reg = new ToolRegistry();
        reg.Register(new FakeTool
        {
            Name = "Boom",
            Handler = _ => throw new System.InvalidOperationException("kaboom"),
        });
        reg.Register(new FakeTool
        {
            Name = "C",
            Handler = _ => { cCalled = true; return ToolResult.Ok("C"); },
        });
        var exec = new SkillExecutor(reg);
        var skill = BuildSkill(
            ("Boom", new Dictionary<string, object?>(), true),  // Optional
            ("C", new Dictionary<string, object?>(), false));
        var result = exec.ExecuteAsync(skill).GetAwaiter().GetResult();
        Assert.True(result.AllSuccess);
        Assert.True(cCalled);
        Assert.Equal(StepStatus.Error, result.Steps[0].Status);
        Assert.Contains("kaboom", result.Steps[0].ErrorMessage);
    }

    [Fact]
    public void SkillExecutor_ArgsSerialized_ToToolAsJson()
    {
        string? captured = null;
        var reg = BuildRegistry(("T", args => { captured = args; return ToolResult.Ok("ok"); }));
        var exec = new SkillExecutor(reg);
        var args = new Dictionary<string, object?> { ["pattern"] = "*.pdf", ["dir"] = "D:\\invoices" };
        var skill = BuildSkill(("T", args, false));
        exec.ExecuteAsync(skill).GetAwaiter().GetResult();
        Assert.NotNull(captured);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(captured!);
        Assert.Equal("*.pdf", parsed!["pattern"]?.ToString());
        Assert.Equal("D:\\invoices", parsed!["dir"]?.ToString());
    }
}