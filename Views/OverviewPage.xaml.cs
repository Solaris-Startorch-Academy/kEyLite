using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using kEyLite.Services;
using kEyLite.ViewModels;
using kEyLite.Views.Dialogs;

namespace kEyLite.Views;

public partial class OverviewPage : UserControl
{
    private readonly System.Collections.ObjectModel.ObservableCollection<KeyItem> _items = new();
    private readonly DispatcherTimer _timer;
    private DispatcherTimer? _clipboardTimer;

    public OverviewPage()
    {
        InitializeComponent();
        KeysList.ItemsSource = _items;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => UpdateItems();

        Loaded += (_, _) => RefreshLockState();
        Unloaded += (_, _) => _timer.Stop();

        AppState.Locked += OnLockStateChanged;
        AppState.Unlocked += OnLockStateChanged;
    }

    private void OnLockStateChanged() => Dispatcher.Invoke(RefreshLockState);

    /// <summary>根据 AppState 的锁定状态切换“解锁面板 / 密钥内容”。</summary>
    private void RefreshLockState()
    {
        bool locked = AppState.IsLocked;

        UnlockPanel.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        ContentPanel.Visibility = locked ? Visibility.Collapsed : Visibility.Visible;

        if (locked)
        {
            _timer.Stop();
            _items.Clear();
            EmptyState.Visibility = Visibility.Collapsed;
            CountText.Text = "";
            UnlockErrorText.Visibility = Visibility.Collapsed;
            UnlockPasswordBox.Clear();
            UnlockPasswordBox.Focus();
        }
        else
        {
            Rebuild();
            _timer.Start();
        }
    }

    private void UnlockPasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            TryUnlock();
        }
    }

    private void Unlock_Click(object sender, RoutedEventArgs e) => TryUnlock();

    private void TryUnlock()
    {
        string password = UnlockPasswordBox.Password;
        if (password.Length == 0)
        {
            ShowUnlockError("请输入访问密码。");
            return;
        }

        try
        {
            byte[] blob = VaultService.ReadBlob();
            var vault = VaultService.TryUnlockWithPassword(blob, password);
            if (vault is null)
            {
                ShowUnlockError("密码错误，请重试。");
                UnlockPasswordBox.SelectAll();
                UnlockPasswordBox.Focus();
                return;
            }

            UnlockErrorText.Visibility = Visibility.Collapsed;
            AppState.Unlock(vault, password);
        }
        catch (Exception ex)
        {
            ShowUnlockError($"读取数据失败：{ex.Message}");
        }
    }

    private void ShowUnlockError(string message)
    {
        UnlockErrorText.Text = message;
        UnlockErrorText.Visibility = Visibility.Visible;
    }

    private void Rebuild()
    {
        _items.Clear();
        if (AppState.Vault is null) return;

        foreach (var key in AppState.Vault.Keys)
        {
            var item = new KeyItem(key);
            item.Update();
            _items.Add(item);
        }

        EmptyState.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = _items.Count == 0 ? "" : $"共 {_items.Count} 个密钥";
    }

    private void UpdateItems()
    {
        foreach (var item in _items)
        {
            item.Update();
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: KeyItem item }) return;

        try
        {
            Clipboard.SetText(item.RawCode);
        }
        catch
        {
            return;
        }

        MainWindow.Instance?.Enqueue("验证码已复制到剪贴板");

        int seconds = AppState.Vault?.Settings.ClipboardClearSeconds ?? 0;
        if (seconds <= 0) return;

        _clipboardTimer?.Stop();
        _clipboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _clipboardTimer.Tick += (_, _) =>
        {
            _clipboardTimer?.Stop();
            try { Clipboard.Clear(); } catch { /* 剪贴板可能被占用 */ }
        };
        _clipboardTimer.Start();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: KeyItem item }) return;

        var result = MessageBox.Show(
            Window.GetWindow(this),
            $"确定删除“{item.Title}”的密钥吗？此操作不可撤销。",
            "删除密钥",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        AppState.Vault?.Keys.Remove(item.Key);
        AppState.Save();
        Rebuild();
        MainWindow.Instance?.Enqueue("密钥已删除");
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: KeyItem item }) return;

        var dlg = new RenameDialog(item.Key.Label,
            string.IsNullOrEmpty(item.Key.Issuer) ? null : item.Key.Issuer)
        {
            Owner = Window.GetWindow(this),
        };

        if (dlg.ShowDialog() != true) return;

        string newName = dlg.NewName ?? "";
        if (newName == item.Key.Label) return;

        item.Key.Label = newName;
        AppState.Save();
        Rebuild();
        MainWindow.Instance?.Enqueue(string.IsNullOrEmpty(newName) ? "已清除显示名称" : "显示名称已更新");
    }

    private void GotoAdd_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.ShowPage(1);
    }
}
