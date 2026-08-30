using System.Windows;

namespace kEyLite.Views.Dialogs;

public partial class PasswordDialog : Window
{
    /// <summary>在“修改密码”模式下输入的当前密码。</summary>
    public string OldInput { get; private set; } = "";

    /// <summary>确认后的新密码。</summary>
    public string NewInput { get; private set; } = "";

    /// <param name="requireOld">是否需要验证当前密码（修改密码模式）。</param>
    public PasswordDialog(string title, string subtitle, bool requireOld)
    {
        InitializeComponent();
        Title = title;
        HeaderText.Text = title;
        SubText.Text = subtitle;
        OldBox.Visibility = requireOld ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (OldBox.Visibility == Visibility.Visible)
        {
            if (OldBox.Password != AppState.Password)
            {
                ShowError("当前密码不正确。");
                return;
            }
            OldInput = OldBox.Password;
        }

        string pwd = NewBox.Password;
        if (pwd.Length < 4)
        {
            ShowError("新密码至少需要 4 位。");
            return;
        }

        if (pwd != ConfirmBox.Password)
        {
            ShowError("两次输入的密码不一致。");
            return;
        }

        NewInput = pwd;
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
