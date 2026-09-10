// kEyLite 2FA Client Beta
// Copyright (C) 2026 Startorch Academy Team - Clara Herta
//
// 本程序是自由软件：你可以根据自由软件基金会发布的 GNU 通用公共许可证
// （第 3 版或更高版本）对其再分发和/或修改。详见 LICENSE 文件。

using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
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
    private bool _navCollapsed;
    private int _navAnimationVersion;
    private string _currentPageTitle = "密钥概览";

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
        _currentPageTitle = index switch
        {
            1 => "添加密钥",
            2 => "设置",
            3 => "关于",
            _ => "密钥概览",
        };
        AnimatePageTransition();
    }

    private void AnimatePageTransition()
    {
        PageHost.BeginAnimation(OpacityProperty, null);
        PageHost.RenderTransform.BeginAnimation(TranslateTransform.YProperty, null);

        var opacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        var offset = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };

        PageHost.BeginAnimation(OpacityProperty, opacity);
        PageHost.RenderTransform.BeginAnimation(TranslateTransform.YProperty, offset);
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        _navCollapsed = !_navCollapsed;
        int animationVersion = ++_navAnimationVersion;
        NavColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
        double from = NavColumn.ActualWidth;
        double to = _navCollapsed ? 72 : 232;
        var widthAnimation = new GridLengthAnimation
        {
            From = new GridLength(from),
            To = new GridLength(to),
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        widthAnimation.Completed += (_, _) =>
        {
            if (animationVersion != _navAnimationVersion) return;
            NavColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            NavColumn.Width = new GridLength(to);
        };
        NavColumn.BeginAnimation(ColumnDefinition.WidthProperty, widthAnimation);

        var textElements = new UIElement[]
        {
            NavOverviewText, NavAddText, NavSettingsText, NavAboutText,
            BrandName, VersionText,
        };
        if (_navCollapsed)
        {
            foreach (var element in textElements)
            {
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
                element.BeginAnimation(UIElement.OpacityProperty, fade);
            }
        }
        else
        {
            foreach (var element in textElements)
            {
                element.Visibility = Visibility.Visible;
                element.Opacity = 0;
                element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                {
                    BeginTime = TimeSpan.FromMilliseconds(100),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                });
            }
        }

        CollapseIcon.Kind = _navCollapsed ? PackIconKind.Menu : PackIconKind.MenuOpen;
        CollapseButton.ToolTip = _navCollapsed ? "展开导航栏" : "折叠导航栏";
    }

    private sealed class GridLengthAnimation : AnimationTimeline
    {
        public GridLength From { get; set; }
        public GridLength To { get; set; }
        public IEasingFunction? EasingFunction { get; set; }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            double progress = animationClock.CurrentProgress ?? 0;
            progress = EasingFunction?.Ease(progress) ?? progress;
            double value = From.Value + (To.Value - From.Value) * progress;
            return new GridLength(value, GridUnitType.Pixel);
        }
    }

    private void LogButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu is { } menu)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void FloatingButton_Click(object sender, RoutedEventArgs e)
        => App.Instance.ToggleFloatingWindow();

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        App.Instance.LockNow();
        Hide();
    }

    private void ExportDetailedLog_Click(object sender, RoutedEventArgs e)
    {
        ExportDetailedLog();
    }

    private void ExportDetailedLog()
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出详细日志",
            Filter = "文本文件|*.txt|所有文件|*.*",
            FileName = $"kEyLite-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
        };
        if (dialog.ShowDialog(this) != true) return;

        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly?.GetName().Version?.ToString()
                      ?? "未知";
        var settings = AppState.Vault?.Settings;
        var report = new StringBuilder()
            .AppendLine("kEyLite 详细诊断日志")
            .AppendLine("====================")
            .AppendLine($"生成时间: {DateTimeOffset.Now:O}")
            .AppendLine($"应用版本: {version}")
            .AppendLine($"系统版本: {Environment.OSVersion}")
            .AppendLine($"运行时: {Environment.Version}")
            .AppendLine($"进程架构: {Environment.Is64BitProcess}")
            .AppendLine($"应用路径: {Environment.ProcessPath}")
            .AppendLine()
            .AppendLine("应用状态")
            .AppendLine("--------")
            .AppendLine($"保险库已解锁: {!AppState.IsLocked}")
            .AppendLine($"密钥数量: {AppState.Vault?.Keys.Count ?? 0}")
            .AppendLine($"保留至后台: {App.Instance.BackgroundKeepActive}")
            .AppendLine($"当前页面: {_currentPageTitle}")
            .AppendLine($"主题模式: {settings?.ThemeMode ?? "未知"}")
            .AppendLine($"主色: {settings?.PrimaryColor ?? "未知"}")
            .AppendLine($"备选色: {settings?.SecondaryColor ?? "未知"}")
            .AppendLine()
            .AppendLine("说明: 此报告不包含密钥、密码或其他敏感数据。");

        try
        {
            System.IO.File.WriteAllText(dialog.FileName, report.ToString(), Encoding.UTF8);
            Enqueue("详细日志已导出");
        }
        catch (Exception ex)
        {
            Enqueue($"日志导出失败：{ex.Message}");
        }
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
