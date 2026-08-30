using System.Windows;
using System.Windows.Media.Imaging;

namespace kEyLite.Views.Dialogs;

public enum ResultDialogKind
{
    /// <summary>昔涟_收到 —— 操作成功。</summary>
    Success,
    /// <summary>白厄_掉线 —— 操作失败。</summary>
    Error,
}

/// <summary>带贴纸表情的成功/失败提示对话框。</summary>
public partial class ResultDialog : Window
{
    private ResultDialog(ResultDialogKind kind, string title, string? message, Window? owner)
    {
        InitializeComponent();
        Owner = owner;
        TitleText.Text = title;

        if (!string.IsNullOrWhiteSpace(message))
        {
            MessageText.Text = message;
            MessageText.Visibility = Visibility.Visible;
        }
        else
        {
            MessageText.Visibility = Visibility.Collapsed;
        }

        string sticker = kind switch
        {
            ResultDialogKind.Success => "昔涟_收到.png",
            _ => "白厄_掉线.png",
        };
        StickerImage.Source = new BitmapImage(
            new Uri($"pack://application:,,,/kEyLite;component/Assets/Stickers/{sticker}", UriKind.Absolute));
    }

    public static void ShowSuccess(Window? owner, string title = "真棒，已成功添加密钥！", string? message = null)
        => new ResultDialog(ResultDialogKind.Success, title, message, owner).ShowDialog();

    public static void ShowError(Window? owner, string title, string? message = null)
        => new ResultDialog(ResultDialogKind.Error, title, message, owner).ShowDialog();

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
