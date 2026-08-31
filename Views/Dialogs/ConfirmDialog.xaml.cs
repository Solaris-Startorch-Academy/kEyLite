using System.Windows;

namespace kEyLite.Views.Dialogs;

/// <summary>
/// Material Design 风格的确认对话框（替代 MessageBox）。
/// </summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmText = "确认")
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
    }

    /// <summary>
    /// 显示确认对话框，返回 true 表示用户点击了确认。
    /// </summary>
    public static bool Show(Window? owner, string title, string message, string confirmText = "确认")
    {
        var dlg = new ConfirmDialog(title, message, confirmText)
        {
            Owner = owner,
        };
        return dlg.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
