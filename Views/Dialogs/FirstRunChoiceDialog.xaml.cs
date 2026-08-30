using System.Windows;

namespace kEyLite.Views.Dialogs;

public enum FirstRunChoice
{
    AddKey,
    Import,
    Later,
}

/// <summary>首次启动时询问用户是否立刻添加密钥或导入数据。</summary>
public partial class FirstRunChoiceDialog : Window
{
    public FirstRunChoice Result { get; private set; } = FirstRunChoice.Later;

    public FirstRunChoiceDialog()
    {
        InitializeComponent();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        Result = FirstRunChoice.AddKey;
        DialogResult = true;
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        Result = FirstRunChoice.Import;
        DialogResult = true;
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        Result = FirstRunChoice.Later;
        DialogResult = true;
    }
}
