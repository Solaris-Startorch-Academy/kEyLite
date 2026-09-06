using System.Windows;
using kEyLite.Views.Dialogs;

namespace kEyLite.Services;

public static class MaterialDialogService
{
    public static void ShowMessage(
        Window? owner,
        string title,
        string message,
        MessageDialogKind kind = MessageDialogKind.Information)
    {
        var dialog = new MessageDialog(title, message, kind, showCancel: false);
        new DialogHostWindow(dialog, GetUsableOwner(owner)).ShowDialog();
    }

    public static bool ShowConfirmation(
        Window? owner,
        string title,
        string message,
        string confirmText = "确认")
    {
        var dialog = new MessageDialog(title, message, MessageDialogKind.Confirmation, showCancel: true)
        {
            ConfirmText = confirmText,
        };
        var host = new DialogHostWindow(dialog, GetUsableOwner(owner));
        host.ShowDialog();
        return host.Result is true;
    }

    private static Window? GetUsableOwner(Window? owner)
        => owner is { IsLoaded: true, IsVisible: true } ? owner : null;
}
