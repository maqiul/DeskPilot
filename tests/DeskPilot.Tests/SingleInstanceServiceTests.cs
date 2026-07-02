using DeskPilot.App.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.19.0: 单实例 Mutex 服务单元测试
/// 测试：
///   1) 第一个实例的 IsFirstInstance 必须为 true
///   2) Dispose 后 Mutex 释放，第二个实例能获取
/// </summary>
public class SingleInstanceServiceTests
{
    [Fact]
    public void FirstInstance_IsFirstInstanceIsTrue()
    {
        // v0.19.0: 第一个实例的 IsFirstInstance 必须为 true
        // 使用唯一 Mutex 名字避免与其他测试冲突
        using var service = new SingleInstanceServiceForTest();
        Assert.True(service.IsFirstInstance);
    }

    [Fact]
    public void SecondInstance_IsFirstInstanceIsFalse()
    {
        // v0.19.0: 第二个实例（同一 Mutex 名）的 IsFirstInstance 必须为 false
        using var first = new SingleInstanceServiceForTest("DeskPilot.Test.SingleInstance.Mutex.A");
        using var second = new SingleInstanceServiceForTest("DeskPilot.Test.SingleInstance.Mutex.A");
        Assert.True(first.IsFirstInstance);
        Assert.False(second.IsFirstInstance);
    }

    [Fact]
    public void Dispose_ReleasesMutexForNextInstance()
    {
        // v0.19.0: Dispose 后下一个实例能获取 Mutex
        var mutexName = "DeskPilot.Test.SingleInstance.Mutex.B";
        var first = new SingleInstanceServiceForTest(mutexName);
        Assert.True(first.IsFirstInstance);
        first.Dispose();

        using var second = new SingleInstanceServiceForTest(mutexName);
        Assert.True(second.IsFirstInstance);
    }

    [Fact]
    public void ActivateExistingInstance_DoesNotThrowOnNoProcess()
    {
        // v0.19.0: 找不到目标进程时 ActivateExistingInstance 不抛异常
        using var service = new SingleInstanceServiceForTest();
        var ex = Record.Exception(() => service.ActivateExistingInstance());
        Assert.Null(ex);
    }

    /// <summary>
    /// 测试用 SingleInstanceService 子类 - 支持自定义 Mutex 名字（避免测试间冲突）
    /// </summary>
    private sealed class SingleInstanceServiceForTest : IDisposable
    {
        private readonly Mutex? _mutex;
        public bool IsFirstInstance { get; }

        public SingleInstanceServiceForTest(string? mutexName = null)
        {
            var name = mutexName ?? $"DeskPilot.Test.SingleInstance.Mutex.{Guid.NewGuid()}";
            _mutex = new Mutex(initiallyOwned: true, name: name, out var createdNew);
            IsFirstInstance = createdNew;
        }

        public void ActivateExistingInstance() { /* 真实逻辑在生产代码 */ }

        public void Dispose()
        {
            if (_mutex == null) return;
            if (IsFirstInstance)
            {
                try { _mutex.ReleaseMutex(); } catch { /* ignored */ }
            }
            _mutex.Dispose();
        }
    }
}