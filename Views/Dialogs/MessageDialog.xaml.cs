using System.Windows;
using MaterialDesignThemes.Wpf;

namespace kEyLite.Views.Dialogs;

public enum MessageDialogKind
{
    Information,
    Warning,
    Error,
    Confirmation,
}

public partial class MessageDialog : System.Windows.Controls.UserControl
{
    public string ConfirmText
    {
        set => ConfirmButton.Content = value;
    }

    public MessageDialog(string title, string message, MessageDialogKind kind, bool showCancel)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        KindIcon.Kind = kind switch
        {
            MessageDialogKind.Warning => PackIconKind.AlertOutline,
            MessageDialogKind.Error => PackIconKind.CloseCircleOutline,
            MessageDialogKind.Confirmation => PackIconKind.HelpCircleOutline,
            _ => PackIconKind.InformationOutline,
        };
        KindIcon.Foreground = kind switch
        {
            MessageDialogKind.Warning => FindResource("MaterialDesignValidationErrorBrush") as System.Windows.Media.Brush,
            MessageDialogKind.Error => FindResource("MaterialDesignValidationErrorBrush") as System.Windows.Media.Brush,
            _ => FindResource("PrimaryHueMidBrush") as System.Windows.Media.Brush,
        };
    }
}
