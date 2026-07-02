using DeskPilot.App.ViewModels;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.25.0: ChatMessage 时间戳测试
/// </summary>
public class ChatMessageTimestampTests
{
    [Fact]
    public void Constructor_SetsTimestampToUtcNow()
    {
        // v0.25.0: 构造函数必须设置 Timestamp 为当前时间
        var before = DateTime.UtcNow.AddSeconds(-1);
        var msg = new ChatMessage("user", "hi");
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(msg.Timestamp, before, after);
    }

    [Fact]
    public void DefaultConstructor_TimestampIsDefault()
    {
        // v0.25.0: 默认构造函数的 Timestamp 是 DateTime.MinValue 之前的某个时间（不是 default）
        var msg = new ChatMessage();
        // [ObservableProperty] 的默认值是 DateTime.MinValue（因为字段初始化为 DateTime.UtcNow 在属性生成器中可能被覆盖）
        // 实际上 [ObservableProperty] 字段初始化器会保留：默认 = DateTime.UtcNow
        Assert.True(msg.Timestamp > DateTime.MinValue);
    }

    [Fact]
    public void LocalTimeText_FormatsAsHms()
    {
        // v0.25.0: LocalTimeText 必须格式化为 "HH:mm:ss"
        var msg = new ChatMessage
        {
            Timestamp = new DateTime(2026, 7, 1, 12, 30, 45, DateTimeKind.Utc)
        };
        // 本地时间取决于机器时区，但格式必须匹配
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}$", msg.LocalTimeText);
    }

    [Fact]
    public void LocalTimeText_UsesLocalTimezone()
    {
        // v0.25.0: LocalTimeText 转换到本地时区（CST = UTC+8）
        // 构造一个 UTC 时间 12:00:00，本地时间应该是 20:00:00
        var msg = new ChatMessage
        {
            Timestamp = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)
        };
        var localTime = msg.Timestamp.ToLocalTime();
        Assert.Equal(localTime.ToString("HH:mm:ss"), msg.LocalTimeText);
    }
}