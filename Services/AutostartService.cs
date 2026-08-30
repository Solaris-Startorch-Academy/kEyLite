using Microsoft.Win32;

namespace kEyLite.Services;

/// <summary>
/// 开机自启管理：写入 HKCU 的 Run 键（当前用户级别，无需管理员权限）。
/// 启动参数附带 --autostart，使程序开机后驻留托盘而不弹出主窗口。
/// </summary>
public static class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "kEyLite";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    /// <exception cref="InvalidOperationException">无法获取程序路径时抛出。</exception>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
                throw new InvalidOperationException("无法获取程序路径。");

            key.SetValue(ValueName, $"\"{exe}\" --autostart");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
