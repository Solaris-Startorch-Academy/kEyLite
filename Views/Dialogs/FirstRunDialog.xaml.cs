using System.Windows;

namespace kEyLite.Views.Dialogs;

public partial class FirstRunDialog : Window
{
    /// <summary>用户设置的密码；选择跳过时为 null。</summary>
    public string? Password { get; private set; }

    public FirstRunDialog()
    {
        InitializeComponent();
    }

    private void Set_Click(object sender, RoutedEventArgs e)
    {
        string pwd = NewBox.Password;
        if (pwd.Length < 4)
        {
            ShowError("密码至少需要 4 位。");
            return;
        }

        if (pwd != ConfirmBox.Password)
        {
            ShowError("两次输入的密码不一致。");
            return;
        }

        Password = pwd;
        DialogResult = true;
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Password = null;
        DialogResult = false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
