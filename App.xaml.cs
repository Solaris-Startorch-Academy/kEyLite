// kEyLite —— 一款轻量的开源 TOTP 两步验证器
// Copyright (C) 2026 Startorch Academy Team - Clara Herta
//
// 本程序是自由软件：你可以根据自由软件基金会发布的 GNU 通用公共许可证
// （第 3 版或更高版本）对其再分发和/或修改。本程序按“现状”提供，
// 不附带任何担保。详见随本程序分发的 LICENSE 文件。

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using H.NotifyIcon;
using kEyLite.Models;
using kEyLite.Services;
using kEyLite.Views;
using kEyLite.Views.Dialogs;

namespace kEyLite;

public partial class App : Application
{
    private const string MutexId = "kEyLite_SingleInstance_6C3F2C1E";
    private const string ActivateId = "kEyLite_SingleInstance_Activate";
    private const int HotkeyId = 0x4B45;
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VkK = 0x4B;

    private enum FirstRunAction { None, ShowAddPage }

    public static App Instance { get; private set; } = null!;

    /// <summary>应用是否正在退出（用于区分“关闭窗口”与“退出程序”）。</summary>
    public static bool IsExiting { get; private set; }

    /// <summary>当前是否处于“保留后台”模式。</summary>
    public bool BackgroundKeepActive => _backgroundKeep;
    public bool IsDarkTheme { get; set; }

    private Mutex? _mutex;
    private EventWaitHandle? _activateEvent;
    private TaskbarIcon? _trayIcon;
    private FloatingKeyWindow? _floatingWindow;
    private HwndSource? _hotkeySource;
    private bool _hotkeyRegistered;
    private DispatcherTimer? _lockTimer;
    private bool _backgroundKeep;
    private FirstRunAction _pendingFirstRunAction = FirstRunAction.None;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Instance = this;

        // —— 单实例：已有实例在运行时激活其主窗口，而不是创建新实例 ——
        _mutex = new Mutex(true, MutexId, out bool createdNew);
        if (!createdNew)
        {
            try { EventWaitHandle.OpenExisting(ActivateId).Set(); } catch { /* 忽略 */ }
            Shutdown();
            return;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateId);
        ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, _) =>
            {
                try { Dispatcher.Invoke(ActivateMainWindow); }
                catch { /* 应用可能正在退出 */ }
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        Directory.CreateDirectory(VaultService.DataDir);

        bool autostart = e.Args.Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));

        // 解锁状态变化时同步后台模式开关
        AppState.Unlocked += () => _backgroundKeep = AppState.Vault?.Settings.BackgroundKeep ?? false;

        // 首启对话框以模态显示且主窗口尚未创建：若保持 OnLastWindowClose，
        // 对话框关闭会被判定为“最后一个窗口关闭”而调度应用关闭，
        // 导致首启完成后主窗口无法出现。启动期间改为显式关闭，主窗口显示后恢复。
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!InitVault()) { ExitApp(); return; }

        var vault = AppState.Vault;
        _backgroundKeep = vault?.Settings.BackgroundKeep ?? false;

        if (autostart)
        {
            // 开机自启：驻留托盘，不创建窗口、不要求输入密码；
            // 只有用户打开主页面时才会要求解锁（如有密码）。
        }
        else
        {
            ActivateMainWindow();
        }

        InitTrayIcon();
        RegisterGlobalHotkey();

        // 应用包含托盘和浮窗，生命周期由 ExitApp 显式控制。
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    // ————————————————————————————— 启动 / 初始化 —————————————————————————————

    /// <summary>初始化保险库。返回 false 表示应退出应用。</summary>
    private bool InitVault()
    {
        try
        {
            if (!VaultService.Exists())
            {
                // 首次启动
                WarnIfRiskyLocation();

                string? password = null;
                var pwdDlg = new FirstRunDialog();
                if (pwdDlg.ShowDialog() == true && !string.IsNullOrEmpty(pwdDlg.Password))
                    password = pwdDlg.Password;

                var vault = new VaultData();
                if (password is not null) vault.Settings.PasswordEnabled = true;
                AppState.Unlock(vault, password);
                AppState.Save();

                // 询问是否立刻添加密钥 / 导入数据
                var choice = new FirstRunChoiceDialog();
                if (choice.ShowDialog() == true)
                {
                    if (choice.Result == FirstRunChoice.AddKey)
                    {
                        _pendingFirstRunAction = FirstRunAction.ShowAddPage;
                    }
                    else if (choice.Result == FirstRunChoice.Import)
                    {
                        ImportBackup();
                    }
                }
                return true;
            }

            byte[] blob = VaultService.ReadBlob();
            if (VaultService.IsPasswordProtected(blob))
            {
                // 保持锁定：打开主页面时在概览页内输入密码解锁
                return true;
            }

            AppState.Unlock(VaultService.DecryptDpapi(blob), null);
            return true;
        }
        catch (Exception ex)
        {
            MaterialDialogService.ShowMessage(
                null,
                $"无法初始化数据文件：{ex.Message}\n\n文件位置：{VaultService.FilePath}",
                "kEyLite",
                MessageDialogKind.Error);
            return false;
        }
    }

    /// <summary>程序位于临时文件夹或桌面上时，建议用户移动位置。</summary>
    private static void WarnIfRiskyLocation()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            string dir = Path.GetFullPath(Path.GetDirectoryName(exe) ?? "");
            if (dir.Length == 0) return;

            string temp = Path.GetFullPath(Path.GetTempPath());
            string desktop = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));

            bool risky = dir.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                      || dir.StartsWith(desktop, StringComparison.OrdinalIgnoreCase);
            if (!risky) return;

            MaterialDialogService.ShowMessage(
                null,
                "检测到 kEyLite 位于临时文件夹或桌面上！\n\n" +
                "这些位置的文件可能被异常删除，导致数据丢失。您应该将程序移动到独立的安装目录！",
                "建议更改程序位置",
                MessageDialogKind.Warning);
        }
        catch
        {
            // 检测失败不影响启动
        }
    }

    /// <summary>
    /// 导入备份（首启向导与设置页共用）。
    /// 返回是否成功导入了密钥。
    /// </summary>
    internal static bool ImportBackup()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入备份",
            Filter = "JSON 文件|*.json|所有文件|*.*",
        };

        if (dlg.ShowDialog() != true) return false;

        try
        {
            string content = File.ReadAllText(dlg.FileName);
            var imported = BackupService.Import(content);

            int added = 0, skipped = 0;
            var vault = AppState.Vault!;
            foreach (var key in imported)
            {
                bool duplicate = vault.Keys.Any(existing =>
                    existing.Secret == key.Secret &&
                    existing.Issuer == key.Issuer &&
                    existing.Label == key.Label);

                if (duplicate)
                {
                    skipped++;
                    continue;
                }

                vault.Keys.Add(key);
                added++;
            }

            if (added > 0) AppState.Save();

            if (added > 0)
            {
                kEyLite.MainWindow.Instance?.Enqueue($"已导入 {added} 个密钥");
                return true;
            }

            kEyLite.MainWindow.Instance?.Enqueue($"导入完成：没有新增（已跳过 {skipped} 个重复项）");
            return false;
        }
        catch (Exception ex)
        {
            MaterialDialogService.ShowMessage(
                kEyLite.MainWindow.Instance,
                "kEyLite",
                $"导入失败：{ex.Message}",
                MessageDialogKind.Error);
            return false;
        }
    }

    // ————————————————————————————— 主窗口 —————————————————————————————

    /// <summary>打开（或激活）主窗口；从托盘恢复时同时停止锁定倒计时。</summary>
    public void ActivateMainWindow()
    {
        if (IsExiting) return;
        StopLockTimer();

        if (kEyLite.MainWindow.Instance is null)
        {
            var window = new kEyLite.MainWindow();
            window.Show();
            window.Activate();
        }
        else
        {
            kEyLite.MainWindow.Instance.ShowFromTray();
        }

        if (_pendingFirstRunAction == FirstRunAction.ShowAddPage)
        {
            _pendingFirstRunAction = FirstRunAction.None;
            kEyLite.MainWindow.Instance?.ShowPage(1);
        }
    }

    /// <summary>主窗口隐藏到托盘后：按设置计划自动锁定。</summary>
    public void OnMainWindowHiddenToTray()
    {
        try { AppState.Save(); } catch { /* 密码会话可能已失效 */ }

        if (IsAuxiliaryWindowVisible())
        {
            StopLockTimer();
            return;
        }

        int minutes = AppState.Vault?.Settings.LockTimeoutMinutes ?? 5;
        if (minutes < 0) return;      // 从不锁定
        if (minutes == 0) { LockNow(); return; }

        StopLockTimer();
        _lockTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(minutes) };
        _lockTimer.Tick += (_, _) =>
        {
            if (IsAuxiliaryWindowVisible())
            {
                StopLockTimer();
                return;
            }

            LockNow();
        };
        _lockTimer.Start();
    }

    public void LockNow()
    {
        StopLockTimer();
        AppState.Lock();
    }

    public void ToggleFloatingWindow()
    {
        if (IsExiting) return;

        if (AppState.IsLocked)
        {
            ActivateMainWindow();
            return;
        }

        if (_floatingWindow is { IsVisible: true })
        {
            _floatingWindow.Activate();
            return;
        }

        _floatingWindow = new FloatingKeyWindow();
        _floatingWindow.Show();
        _floatingWindow.Activate();
        StopLockTimer();
    }

    public void OnFloatingWindowClosed(FloatingKeyWindow window)
    {
        if (ReferenceEquals(_floatingWindow, window))
            _floatingWindow = null;

        if (kEyLite.MainWindow.Instance is { IsVisible: false } && !AppState.IsLocked && !IsExiting)
            OnMainWindowHiddenToTray();
    }

    private bool IsAuxiliaryWindowVisible()
        => _floatingWindow is { IsVisible: true };

    private void RegisterGlobalHotkey()
    {
        var parameters = new HwndSourceParameters("kEyLiteHotkey")
        {
            WindowStyle = 0,
        };
        _hotkeySource = new HwndSource(parameters);
        _hotkeySource.AddHook(HotkeyWindowProc);
        _hotkeyRegistered = RegisterHotKey(
            _hotkeySource.Handle,
            HotkeyId,
            ModControl | ModShift,
            VkK);
    }

    private IntPtr HotkeyWindowProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ToggleFloatingWindow();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void SetBackgroundKeep(bool value) => _backgroundKeep = value;

    /// <summary>锁定时间设置变化后，若窗口当前处于隐藏状态则重新计划。</summary>
    public void RescheduleLockIfHidden()
    {
        bool hidden = kEyLite.MainWindow.Instance is null || !kEyLite.MainWindow.Instance.IsVisible;
        if (hidden && !AppState.IsLocked) OnMainWindowHiddenToTray();
    }

    private void StopLockTimer()
    {
        _lockTimer?.Stop();
        _lockTimer = null;
    }

    // ————————————————————————————— 托盘 —————————————————————————————

    private void InitTrayIcon()
    {
        try
        {
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "kEyLite - 主程序",
                IconSource = new BitmapImage(new Uri(
                    "pack://application:,,,/kEyLite;component/Assets/App.ico")),
                ContextMenu = BuildTrayMenu(),
            };
            _trayIcon.TrayMouseDoubleClick += (_, _) => ActivateMainWindow();
            _trayIcon.ForceCreate();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"托盘初始化失败：{ex}");
        }
    }

    private System.Windows.Controls.ContextMenu BuildTrayMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        menu.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "MaterialDesignPaper");
        menu.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "MaterialDesignBody");

        var menuItemStyle = new System.Windows.Style(typeof(System.Windows.Controls.MenuItem));
        menuItemStyle.Setters.Add(new System.Windows.Setter(
            System.Windows.Controls.Control.BackgroundProperty,
            new System.Windows.DynamicResourceExtension("MaterialDesignPaper")));
        menuItemStyle.Setters.Add(new System.Windows.Setter(
            System.Windows.Controls.Control.ForegroundProperty,
            new System.Windows.DynamicResourceExtension("MaterialDesignBody")));
        menu.Resources.Add(typeof(System.Windows.Controls.MenuItem), menuItemStyle);

        menu.Items.Add(new MenuItem { Header = "kEyLite", IsEnabled = false });
        menu.Items.Add(new Separator());
        var openItem = new System.Windows.Controls.MenuItem { Header = "打开主页面" };
        openItem.Click += (_, _) => ActivateMainWindow();
        menu.Items.Add(openItem);

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "设置" };
        settingsItem.Click += (_, _) =>
        {
            ActivateMainWindow();
            if (!AppState.IsLocked) kEyLite.MainWindow.Instance?.ShowPage(2);
        };
        menu.Items.Add(settingsItem);
        menu.Items.Add(new System.Windows.Controls.Separator());

        var restartItem = new System.Windows.Controls.MenuItem { Header = "重启" };
        restartItem.Click += (_, _) => RestartApp();
        menu.Items.Add(restartItem);

        var exitItem = new System.Windows.Controls.MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => ExitApp();
        menu.Items.Add(exitItem);
        return menu;
    }

    // ————————————————————————————— 重启 / 退出 —————————————————————————————

    private void RestartApp()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                MaterialDialogService.ShowMessage(
                    kEyLite.MainWindow.Instance,
                    "kEyLite",
                    "重启时出现严重错误！",
                    MessageDialogKind.Error);
                return;
            }

            // 先释放单实例互斥体，避免新进程误判为重复启动
            try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { /* 未持有 */ }
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            ExitApp();
        }
        catch (Exception ex)
        {
            MaterialDialogService.ShowMessage(
                kEyLite.MainWindow.Instance,
                "重启失败",
                ex.Message,
                MessageDialogKind.Error);
        }
    }

    private void ExitApp()
    {
        IsExiting = true;
        StopLockTimer();
        try { AppState.Save(); } catch { /* 已锁定或会话失效 */ }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;

        if (_hotkeyRegistered && _hotkeySource is not null)
            UnregisterHotKey(_hotkeySource.Handle, HotkeyId);
        _hotkeySource?.RemoveHook(HotkeyWindowProc);
        _hotkeySource?.Dispose();
        _hotkeySource = null;

        _floatingWindow?.Close();
        _floatingWindow = null;

        if (_trayIcon is not null)
        {
            try
            {
                _trayIcon.Dispose();
            }
            catch { /* 忽略 */ }
            _trayIcon = null;
        }

        _mutex?.Dispose();
        _mutex = null;

        base.OnExit(e);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
