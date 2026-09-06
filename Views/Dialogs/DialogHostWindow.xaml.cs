using System.Windows;
using MaterialDesignThemes.Wpf;

namespace kEyLite.Views.Dialogs;

public partial class DialogHostWindow : Window
{
    public object? Result { get; private set; }

    public DialogHostWindow(object content, Window? owner)
    {
        InitializeComponent();
        Owner = owner;
        DialogHost.DialogContent = content;
        DialogHost.DialogClosed += DialogHost_DialogClosed;
        Loaded += (_, _) => DialogHost.IsOpen = true;
    }

    private void DialogHost_DialogClosed(object sender, DialogClosedEventArgs e)
    {
        Result = e.Parameter;
        Close();
    }
}
