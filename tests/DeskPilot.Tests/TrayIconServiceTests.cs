using DeskPilot.App.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.18.0: 系统托盘 TrayIconService 单元测试
/// 注意：TrayIconService 涉及 WPF Window + WinForms NotifyIcon，在 xUnit STA 测试中无法真实实例化窗口。
/// 仅测试不依赖 Window 的行为：构造参数校验。
/// Dispose/Show/Hide 幂等测试通过 App.xaml.cs smoke test 真实流程验证。
/// </summary>
public class TrayIconServiceTests
{
    [Fact]
    public void Constructor_NullWindow_ThrowsArgumentNullException()
    {
        // v0.18.0: ctor 必须校验主窗口非 null（避免运行时崩溃）
        var ex = Assert.Throws<System.ArgumentNullException>(() => new TrayIconService(null!));
        Assert.Equal("mainWindow", ex.ParamName);
    }
}