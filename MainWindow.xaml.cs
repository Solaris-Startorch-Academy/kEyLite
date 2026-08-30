// kEyLite 2FA Client Beta
// Copyright (C) 2026 Startorch Academy Team - Clara Herta
//
// 本程序是自由软件：你可以根据自由软件基金会发布的 GNU 通用公共许可证
// （第 3 版或更高版本）对其再分发和/或修改。详见 LICENSE 文件。

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using kEyLite.Views;

namespace kEyLite;

public partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }

    private readonly OverviewPage _overviewPage = new();
    private readonly AddKeyPage _addKeyPage = new();
    private readonly SettingsPage _settingsPage = new();
    private readonly AboutPage _aboutPage = new();

    private readonly SnackbarMessageQueue _snackbar = new(TimeSpan.FromSeconds(3));

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        MainSnackbar.MessageQueue = _snackbar;

        AppState.Locked += OnAppLockStateChanged;
        AppState.Unlocked += OnAppLockStateChanged;

        NavList.SelectedIndex = 0;
        UpdateNavLockState();
    }

    private void OnAppLockStateChanged() => Dispatcher.Invoke(UpdateNavLockState);

    /// <summary>锁定时禁用除概览外的所有导航项（须先解锁）。</summary>
    private void UpdateNavLockState()
    {
        bool locked = AppState.IsLocked;
        for (int i = 1; i < NavList.Items.Count; i++)
            ((ListBoxItem)NavList.Items[i]).IsEnabled = !locked;

        if (locked && NavList.SelectedIndex != 0)
            NavList.SelectedIndex = 0;
    }

    /// <summary>从托盘恢复窗口。</summary>
    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>切换到指定导航索引的页面（锁定时仅允许概览页）。</summary>
    public void ShowPage(int index)
    {
        if (AppState.IsLocked && index != 0) index = 0;
        if (NavList.SelectedIndex != index)
        {
            NavList.SelectedIndex = index;
        }
    }

    public void Enqueue(string message) => _snackbar.Enqueue(message);

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int index = NavList.SelectedIndex;
        if (AppState.IsLocked && index != 0)
        {
            NavList.SelectedIndex = 0;
            return;
        }

        PageHost.Content = index switch
        {
            1 => _addKeyPage,
            2 => _settingsPage,
            3 => _aboutPage,
            _ => _overviewPage,
        };
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (App.IsExiting) return;

        // 保留后台：隐藏到托盘并启动锁定倒计时；否则直接退出应用
        if (App.Instance.BackgroundKeepActive)
        {
            e.Cancel = true;
            Hide();
            App.Instance.OnMainWindowHiddenToTray();
        }
    }
}
