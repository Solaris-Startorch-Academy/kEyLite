using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using kEyLite.Services;
using kEyLite.Views.Dialogs;

namespace kEyLite.Views;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        Loaded += (_, _) => DataPathText.Text = VaultService.FilePath;
    }

    private void OpenLicense_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink { NavigateUri: var uri })
        {
            Process.Start(new ProcessStartInfo(uri.ToString())
            {
                UseShellExecute = true,
            });
        }
    }

    /// <summary>打开随应用分发的 HarmonyOS Sans SC 字体许可证。</summary>
    private void OpenFontLicense_Click(object sender, RoutedEventArgs e)
    {
        const string resourceName = "kEyLite.Assets.Fonts.LICENSE.txt";
        using Stream? resource = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName);
        if (resource is null)
        {
            MaterialDialogService.ShowMessage(
                Window.GetWindow(this),
                "kEyLite - 注意",
                "未能找到字体许可证嵌入资源。",
                MessageDialogKind.Warning);
            return;
        }

        string path = Path.Combine(Path.GetTempPath(), "kEyLite-HarmonyOS-Sans-SC-LICENSE.txt");
        using (FileStream file = File.Create(path))
        {
            resource.CopyTo(file);
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
