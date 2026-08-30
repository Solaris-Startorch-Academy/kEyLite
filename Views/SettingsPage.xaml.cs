using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using kEyLite.Models;
using kEyLite.Services;
using kEyLite.Views.Dialogs;

namespace kEyLite.Views;

public partial class SettingsPage : UserControl
{
    private bool _initialized;
    private IReadOnlyList<Swatch>? _swatches;

    /// <summary>当前保险库（设置页仅在解锁状态下交互，调用点均已确保解锁）。</summary>
    private static VaultData Vault => AppState.Vault!;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Initialize();
    }

    private void Initialize()
    {
        _initialized = false;

        if (AppState.IsLocked) return;

        var s = Vault.Settings;

        switch (s.ThemeMode)
        {
            case "Light": ThemeLight.IsChecked = true; break;
            case "Dark": ThemeDark.IsChecked = true; break;
            default: ThemeSystem.IsChecked = true; break;
        }

        UpdatePasswordUi();
        UpdateColorUi();

        int clear = s.ClipboardClearSeconds;
        SelectByTag(ClipboardClearBox, clear);

        SelectByTag(LockTimeoutBox, s.LockTimeoutMinutes);

        BackgroundKeepToggle.IsChecked = s.BackgroundKeep;
        AutoStartToggle.IsChecked = s.AutoStart;
        UpdateAutoStartUi();

        _initialized = true;
    }

    private static void SelectByTag(ComboBox box, int value)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (int.TryParse(item.Tag?.ToString(), out int v) && v == value)
            {
                box.SelectedItem = item;
                return;
            }
        }
    }

    // ---------- 外观 ----------

    private void ThemeLight_Checked(object sender, RoutedEventArgs e) => ApplyTheme("Light");
    private void ThemeDark_Checked(object sender, RoutedEventArgs e) => ApplyTheme("Dark");
    private void ThemeSystem_Checked(object sender, RoutedEventArgs e) => ApplyTheme("System");

    private void ApplyTheme(string mode)
    {
        if (!_initialized || Vault.Settings.ThemeMode == mode) return;

        Vault.Settings.ThemeMode = mode;
        ApplyCurrentTheme();
        MainWindow.Instance?.Enqueue("主题已更新");
    }

    /// <summary>按当前设置（模式 + 主色 + 备选色）应用主题并保存。</summary>
    private void ApplyCurrentTheme()
    {
        var s = Vault.Settings;
        ThemeService.Apply(s.ThemeMode, s.PrimaryColor, s.SecondaryColor);
        AppState.Save();
    }

    // ---------- 调色盘 ----------

    private IReadOnlyList<Swatch> Swatches => _swatches ??= new SwatchesProvider().Swatches.ToList();

    private void UpdateColorUi()
    {
        var s = Vault.Settings;
        BuildPalette(PrimaryPaletteHost, primary: true);
        BuildPalette(SecondaryPaletteHost, primary: false);

        SetSwatchUi(PrimaryColorSwatch, PrimaryColorName, s.PrimaryColor, "lightBlue");
        SetSwatchUi(SecondaryColorSwatch, SecondaryColorName, s.SecondaryColor, "lightBlue");
    }

    /// <summary>在代码中直接构建圆形色块（Border 渲染无模板依赖；MD3 隐式 Button 模板会忽略本地 Background）。</summary>
    private void BuildPalette(System.Windows.Controls.WrapPanel host, bool primary)
    {
        host.Children.Clear();
        foreach (var swatch in Swatches)
        {
            if (swatch.ExemplarHue?.Color is not Color hueColor) continue;

            string colorName = swatch.Name;
            var block = new System.Windows.Controls.Border
            {
                Width = 30,
                Height = 30,
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(15),
                Background = new SolidColorBrush(hueColor),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = colorName,
            };
            block.MouseLeftButtonDown += (_, _) =>
            {
                if (AppState.IsLocked) return;

                if (primary) Vault.Settings.PrimaryColor = colorName;
                else Vault.Settings.SecondaryColor = colorName;

                ApplyCurrentTheme();
                UpdateColorUi();

                if (primary) PrimaryColorPopup.IsPopupOpen = false;
                else SecondaryColorPopup.IsPopupOpen = false;

                MainWindow.Instance?.Enqueue($"已更改为 {colorName}");
            };
            host.Children.Add(block);
        }
    }

    private void SetSwatchUi(System.Windows.Controls.Border swatch, TextBlock name, string? colorName, string fallback)
    {
        var match = Swatches.FirstOrDefault(sw =>
            string.Equals(sw.Name, colorName, StringComparison.OrdinalIgnoreCase))
            ?? Swatches.FirstOrDefault(sw => string.Equals(sw.Name, fallback, StringComparison.OrdinalIgnoreCase));

        if (match is null) return;

        if (match.ExemplarHue?.Color is Color c)
            swatch.Background = new SolidColorBrush(c);
        name.Text = match.Name;
    }

    private void PrimarySwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Swatch swatch } || AppState.IsLocked) return;

        Vault.Settings.PrimaryColor = swatch.Name;
        ApplyCurrentTheme();
        UpdateColorUi();
        PrimaryColorPopup.IsPopupOpen = false;
        MainWindow.Instance?.Enqueue($"主题色已更改为 {swatch.Name}");
    }

    private void SecondarySwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Swatch swatch } || AppState.IsLocked) return;

        Vault.Settings.SecondaryColor = swatch.Name;
        ApplyCurrentTheme();
        UpdateColorUi();
        SecondaryColorPopup.IsPopupOpen = false;
        MainWindow.Instance?.Enqueue($"备选色已更改为 {swatch.Name}");
    }

    // ---------- 访问密码 ----------

    private void UpdatePasswordUi()
    {
        bool enabled = Vault.Settings.PasswordEnabled;

        PasswordStatus.Text = enabled
            ? "已启用：数据使用你的密码进行 AES 加密，每次启动都需要输入密码解锁。"
            : "未启用：数据回退至 DPAPI 保护，仅本机当前用户可读取。建议设置访问密码以获得更强保护。";

        BtnSetPassword.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        BtnChangePassword.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        BtnRemovePassword.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetPassword_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PasswordDialog(
            "设置访问密码",
            "设置后，本地数据将使用该密码加密，每次启动都需要输入密码解锁。密码丢失后数据将无法恢复，请务必牢记。",
            requireOld: false)
        {
            Owner = Window.GetWindow(this),
        };

        if (dlg.ShowDialog() != true) return;

        AppState.Password = dlg.NewInput;
        Vault.Settings.PasswordEnabled = true;
        AppState.Save();
        UpdatePasswordUi();
        MainWindow.Instance?.Enqueue("访问密码已启用");
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PasswordDialog(
            "修改访问密码",
            "请先输入当前密码进行验证，然后设置新密码。修改后所有数据将使用新密码重新加密。",
            requireOld: true)
        {
            Owner = Window.GetWindow(this),
        };

        if (dlg.ShowDialog() != true) return;

        AppState.Password = dlg.NewInput;
        AppState.Save(); // 触发以新密码重新加密
        UpdatePasswordUi();
        MainWindow.Instance?.Enqueue("访问密码已修改");
    }

    private void RemovePassword_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            Window.GetWindow(this),
            "移除访问密码后将回退至 DPAPI 保护，本机其他用户账户将无法读取。确定移除吗？",
            "移除访问密码",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        Vault.Settings.PasswordEnabled = false;
        AppState.Password = null;
        AppState.Save();
        UpdatePasswordUi();
        MainWindow.Instance?.Enqueue("访问密码已移除");
    }

    // ---------- 备份 ----------

    private Window? OwnerWindow => Window.GetWindow(this);

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (Vault.Keys.Count == 0)
        {
            MainWindow.Instance?.Enqueue("当前没有可导出的密钥");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "导出备份",
            Filter = "JSON 文件|*.json|所有文件|*.*",
            FileName = $"kEyLite-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json",
        };

        if (dlg.ShowDialog(OwnerWindow) != true) return;

        try
        {
            File.WriteAllText(dlg.FileName, BackupService.ExportToJson(Vault.Keys), new UTF8Encoding(false));
            MainWindow.Instance?.Enqueue($"已导出 {Vault.Keys.Count} 个密钥");
        }
        catch (Exception ex)
        {
            ResultDialog.ShowError(OwnerWindow, "导出失败", ex.Message);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "导入备份",
            Filter = "JSON 文件|*.json|所有文件|*.*",
        };

        if (dlg.ShowDialog(OwnerWindow) != true) return;

        try
        {
            string content = File.ReadAllText(dlg.FileName);
            var imported = BackupService.Import(content);

            int added = 0, skipped = 0;
            foreach (var key in imported)
            {
                bool duplicate = Vault.Keys.Any(existing =>
                    existing.Secret == key.Secret &&
                    existing.Issuer == key.Issuer &&
                    existing.Label == key.Label);

                if (duplicate)
                {
                    skipped++;
                    continue;
                }

                Vault.Keys.Add(key);
                added++;
            }

            if (added > 0) AppState.Save();

            if (added > 0)
            {
                ResultDialog.ShowSuccess(OwnerWindow, message: $"新增 {added} 个密钥，跳过 {skipped} 个重复项。");
                MainWindow.Instance?.ShowPage(0);
            }
            else
            {
                MainWindow.Instance?.Enqueue($"导入完成：没有新增（已跳过 {skipped} 个重复项）");
            }
        }
        catch (Exception ex)
        {
            ResultDialog.ShowError(OwnerWindow, "导入失败", ex.Message);
        }
    }

    // ---------- 后台与启动 ----------

    private void BackgroundKeep_Checked(object sender, RoutedEventArgs e) => SetBackgroundKeep(true);
    private void BackgroundKeep_Unchecked(object sender, RoutedEventArgs e) => SetBackgroundKeep(false);

    private void SetBackgroundKeep(bool value)
    {
        if (!_initialized) return;
        var s = Vault.Settings;
        if (s.BackgroundKeep == value) return;

        s.BackgroundKeep = value;
        App.Instance.SetBackgroundKeep(value);

        // 依赖约束：关闭保留后台时必须同时关闭开机自启
        if (!value && s.AutoStart)
        {
            try
            {
                AutostartService.SetEnabled(false);
                s.AutoStart = false;
            }
            catch { /* 注册表操作失败时保持原状 */ }
        }

        AppState.Save();
        UpdateAutoStartUi();
    }

    private void AutoStart_Checked(object sender, RoutedEventArgs e) => SetAutoStart(true);
    private void AutoStart_Unchecked(object sender, RoutedEventArgs e) => SetAutoStart(false);

    private void SetAutoStart(bool value)
    {
        if (!_initialized) return;
        var s = Vault.Settings;
        if (s.AutoStart == value) return;

        try
        {
            AutostartService.SetEnabled(value);
            s.AutoStart = value;
            AppState.Save();
            MainWindow.Instance?.Enqueue(value ? "开机自启已开启" : "开机自启已关闭");
        }
        catch (Exception ex)
        {
            MainWindow.Instance?.Enqueue($"设置开机自启失败：{ex.Message}");

            // 回滚开关显示
            _initialized = false;
            AutoStartToggle.IsChecked = !value;
            _initialized = true;
        }
    }

    private void UpdateAutoStartUi()
    {
        bool keep = AppState.Vault?.Settings.BackgroundKeep ?? false;
        AutoStartToggle.IsEnabled = keep;
    }

    // ---------- 高级 ----------

    private void LockTimeout_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;

        if (LockTimeoutBox.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out int value) &&
            value != Vault.Settings.LockTimeoutMinutes)
        {
            Vault.Settings.LockTimeoutMinutes = value;
            AppState.Save();
            App.Instance.RescheduleLockIfHidden();
        }
    }

    private void ClipboardClear_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;

        if (ClipboardClearBox.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out int value) &&
            value != Vault.Settings.ClipboardClearSeconds)
        {
            Vault.Settings.ClipboardClearSeconds = value;
            AppState.Save();
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{VaultService.FilePath}\"")
        {
            UseShellExecute = true,
        });
    }
}
