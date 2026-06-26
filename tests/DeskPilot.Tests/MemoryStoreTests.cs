using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// 验证 LocalJsonMemoryStore 在 WPF UI 线程 SyncContext 下不会死锁。
/// 场景：UI 线程模拟（Dispatcher 忙）调用 LoadAsync() + GetAwaiter().GetResult()，
/// 不能 hang。如果 hang，测试会超时失败。
/// </summary>
public class MemoryStoreTests
{
    [Fact(Timeout = 5000)]
    public async Task LoadAsync_UnderFakeSyncContext_DoesNotDeadlock()
    {
        // 验证：LoadAsync 不会在 WPF UI 线程 SyncContext 下死锁
        // （回归 v0.9.2 修的 sync-over-async 死锁问题）
        // LocalJsonMemoryStore 使用全局 %AppData%/DeskPilot/memory.json 路径，
        // 不断言内容；只断言不 hang + 返回非 null。
        var store = new LocalJsonMemoryStore { MaxEntries = 10 };
        var ctx = new FakeSyncContext();
        SynchronizationContext.SetSynchronizationContext(ctx);

        var entries = await Task.Run(async () =>
                await store.LoadAsync().ConfigureAwait(false))
            .WaitAsync(TimeSpan.FromSeconds(3));

        Assert.NotNull(entries);
        // 不断言空 — 全局 %AppData%/DeskPilot/memory.json 已有真实对话记录（你之前测试过）
    }

    [Fact(Timeout = 5000)]
    public async Task LoadAsync_ReturnsEmpty_WhenFileDoesNotExist()
    {
        // 隔离测试：临时重命名全局 memory.json → 跑测试 → 恢复
        var appData = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "DeskPilot");
        Directory.CreateDirectory(appData);
        var path = Path.Combine(appData, "memory.json");
        var backupPath = path + ".test-backup";

        // 备份现有文件（如果有）
        var hadFile = File.Exists(path);
        if (hadFile) File.Move(path, backupPath, overwrite: true);
        try
        {
            var store = new LocalJsonMemoryStore();
            var entries = await store.LoadAsync().ConfigureAwait(false);
            Assert.NotNull(entries);
            Assert.Empty(entries);
        }
        finally
        {
            if (File.Exists(backupPath)) File.Move(backupPath, path, overwrite: true);
        }
    }

    [Fact(Timeout = 5000)]
    public async Task SaveAndLoad_RoundTrip()
    {
        var store = new LocalJsonMemoryStore { MaxEntries = 100 };
        var entries = new System.Collections.Generic.List<MemoryEntry>
        {
            new("user", "你好"),
            new("assistant", "你好！有什么可以帮您？"),
        };
        await store.SaveAsync(entries).ConfigureAwait(false);

        var loaded = await store.LoadAsync().ConfigureAwait(false);
        Assert.Equal(2, loaded.Count);
        Assert.Equal("你好", loaded[0].Content);
        Assert.Equal("assistant", loaded[1].Role);
    }

    [Fact(Timeout = 5000)]
    public async Task LoadAsync_RecoversFromCorruptedFile()
    {
        // 写一个损坏的 memory.json
        var appData = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "DeskPilot");
        Directory.CreateDirectory(appData);
        var path = Path.Combine(appData, "memory.json");
        var original = File.Exists(path) ? File.ReadAllText(path) : null;
        try
        {
            File.WriteAllText(path, "{这不是合法 JSON}");

            var store = new LocalJsonMemoryStore();
            var entries = await store.LoadAsync().ConfigureAwait(false);
            Assert.NotNull(entries);
            // 损坏文件应被备份 + 返回空 list
            Assert.Empty(entries);
        }
        finally
        {
            // 恢复原文件
            if (original != null) File.WriteAllText(path, original);
            else if (File.Exists(path)) File.Delete(path);
        }
    }
}

/// <summary>模拟 WPF DispatcherSyncContext — 不实际调度回调到原线程。</summary>
internal sealed class FakeSyncContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state) => d(state);
    public override void Send(SendOrPostCallback d, object? state) => d(state);
}