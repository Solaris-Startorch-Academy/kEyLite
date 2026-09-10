namespace kEyLite.Models;

/// <summary>应用设置（随保险库一起加密存储）。</summary>
public class AppSettings
{
    /// <summary>主题模式：Light / Dark / System。</summary>
    public string ThemeMode { get; set; } = "System";

    /// <summary>是否启用了访问密码。</summary>
    public bool PasswordEnabled { get; set; }

    /// <summary>复制验证码后自动清空剪贴板的秒数；0 表示不启用。</summary>
    public int ClipboardClearSeconds { get; set; } = 30;

    /// <summary>是否开机自动启动并驻留托盘。</summary>
    public bool AutoStart { get; set; }

    /// <summary>关闭主页面后自动锁定的分钟数：0 = 立即锁定，-1 = 从不。</summary>
    public int LockTimeoutMinutes { get; set; } = 5;

    /// <summary>全局锁定快捷键，使用 WPF KeyGesture 格式。</summary>
    public string LockHotkey { get; set; } = "Ctrl+Shift+L";

    /// <summary>Material Design 主色 Swatch 名称（默认天蓝色 lightBlue）。</summary>
    public string PrimaryColor { get; set; } = "lightBlue";

    /// <summary>Material Design 备选色 Swatch 名称（默认天蓝色 lightBlue）。</summary>
    public string SecondaryColor { get; set; } = "lightBlue";
}
