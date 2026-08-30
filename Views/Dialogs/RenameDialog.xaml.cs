using System.Windows;
using MaterialDesignThemes.Wpf;

namespace kEyLite.Views.Dialogs;

public partial class RenameDialog : Window
{
    /// <summary>用户输入的新显示名称（可能为空串，表示使用签发者名称）。</summary>
    public string NewName { get; private set; } = "";

    public RenameDialog(string currentName, string? issuerHint = null)
    {
        InitializeComponent();
        NameBox.Text = currentName ?? "";
        if (!string.IsNullOrEmpty(issuerHint))
            HintAssist.SetHelperText(NameBox, $"留空时将显示签发者：{issuerHint}");
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        NewName = NameBox.Text?.Trim() ?? "";
        DialogResult = true;
    }
}
