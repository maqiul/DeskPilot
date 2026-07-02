using DeskPilot.App.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.23.0: UpdateCheckService 单元测试
/// 测试 IsNewer 比较逻辑（不依赖网络）
/// </summary>
public class UpdateCheckServiceTests
{
    [Theory]
    [InlineData("0.23.0", "0.22.1", true)]   // 新主版本
    [InlineData("1.0.0", "0.99.99", true)]   // 大版本跳跃
    [InlineData("0.22.1", "0.22.1", false)]  // 相同版本
    [InlineData("0.22.0", "0.22.1", false)]  // 旧版本
    [InlineData("0.23.0", "0.22.99", true)]  // 新次版本
    public void IsNewer_ReturnsExpectedResult(string latest, string current, bool expected)
    {
        // v0.23.0: SemanticVersion 数值比较必须正确
        Assert.Equal(expected, UpdateCheckService.IsNewer(latest, current));
    }

    [Fact]
    public void IsNewer_InvalidVersion_ReturnsFalse()
    {
        // v0.23.0: 无效版本号返回 false（不更新）
        Assert.False(UpdateCheckService.IsNewer("not-a-version", "0.22.1"));
        Assert.False(UpdateCheckService.IsNewer("0.23.0", "not-a-version"));
    }

    [Fact]
    public void Constructor_SetsCurrentVersionFromAssembly()
    {
        // v0.23.0: CurrentVersion 必须从 Assembly 版本号获取
        var service = new UpdateCheckService();
        Assert.NotNull(service.CurrentVersion);
        Assert.NotEmpty(service.CurrentVersion);
        // CurrentVersion 格式必须是 X.Y.Z
        Assert.Matches(@"^\d+\.\d+\.\d+", service.CurrentVersion);
    }
}