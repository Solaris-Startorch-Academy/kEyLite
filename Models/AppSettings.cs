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

    /// <summary>关闭主页面后保留至后台（托盘）运行。</summary>
    public bool BackgroundKeep { get; set; }

    /// <summary>开机自动启动（仅允许在启用 BackgroundKeep 时开启）。</summary>
    public bool AutoStart { get; set; }

    /// <summary>关闭主页面后自动锁定的分钟数：0 = 立即锁定，-1 = 从不。</summary>
    public int LockTimeoutMinutes { get; set; } = 5;

    /// <summary>Material Design 主色 Swatch 名称（默认天蓝色 lightBlue）。</summary>
    public string PrimaryColor { get; set; } = "lightBlue";

    /// <summary>Material Design 备选色 Swatch 名称（默认天蓝色 lightBlue）。</summary>
    public string SecondaryColor { get; set; } = "lightBlue";
}
