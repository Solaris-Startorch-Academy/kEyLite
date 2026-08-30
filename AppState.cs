using kEyLite.Models;
using kEyLite.Services;

namespace kEyLite;

/// <summary>
/// 全局共享的应用状态。
/// Vault 为 null 表示处于“锁定”状态（设置了访问密码且尚未解锁/已超时上锁）。
/// </summary>
public static class AppState
{
    public static VaultData? Vault { get; private set; }

    /// <summary>当前会话的访问密码；未启用密码保护时为 null。</summary>
    public static string? Password { get; internal set; }

    public static bool IsLocked => Vault is null;

    public static event Action? Locked;
    public static event Action? Unlocked;

    /// <summary>解锁保险库并应用其主题设置。</summary>
    public static void Unlock(VaultData vault, string? password)
    {
        Vault = vault;
        Password = password;
        ThemeService.Apply(vault.Settings.ThemeMode, vault.Settings.PrimaryColor, vault.Settings.SecondaryColor);
        Unlocked?.Invoke();
    }

    /// <summary>上锁：丢弃内存中的明文密钥与会话密码（磁盘数据本就始终加密）。</summary>
    public static void Lock()
    {
        if (Vault is null) return;
        Vault = null;
        Password = null;
        Locked?.Invoke();
    }

    public static void Save()
    {
        if (Vault is not null) VaultService.Save(Vault, Password);
    }
}
