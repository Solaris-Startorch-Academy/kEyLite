using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using kEyLite.ViewModels;

namespace kEyLite.Views;

public partial class FloatingKeyWindow : Window
{
    private readonly ObservableCollection<KeyItem> _items = new();
    private readonly DispatcherTimer _timer;

    public FloatingKeyWindow()
    {
        InitializeComponent();
        KeysList.ItemsSource = _items;
        PositionInWorkArea();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => UpdateItems();
        Loaded += (_, _) =>
        {
            AppState.Unlocked += OnVaultChanged;
            AppState.Locked += OnVaultChanged;
            Rebuild();
            _timer.Start();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            AppState.Unlocked -= OnVaultChanged;
            AppState.Locked -= OnVaultChanged;
        };
    }

    private void PositionInWorkArea()
    {
        Left = SystemParameters.WorkArea.Right - Width - 18;
        Top = SystemParameters.WorkArea.Bottom - Height - 18;
    }

    private void OnVaultChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(OnVaultChanged);
            return;
        }

        if (AppState.IsLocked)
        {
            Close();
            return;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        _items.Clear();
        var vault = AppState.Vault;
        if (vault is null) return;

        foreach (var key in vault.Keys)
        {
            var item = new KeyItem(key);
            item.Update();
            _items.Add(item);
        }
    }

    private void UpdateItems()
    {
        if (AppState.IsLocked)
        {
            Close();
            return;
        }

        foreach (var item in _items)
            item.Update();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: KeyItem item }) return;
        if (AppState.IsLocked) return;

        try
        {
            Clipboard.SetText(item.RawCode);
            MainWindow.Instance?.Enqueue("验证码已复制到剪贴板");
        }
        catch
        {
            MainWindow.Instance?.Enqueue("复制验证码失败");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void OpenMainWindow_Click(object sender, RoutedEventArgs e)
        => App.Instance.ActivateMainWindow();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        => App.Instance.OnFloatingWindowClosed(this);
}
