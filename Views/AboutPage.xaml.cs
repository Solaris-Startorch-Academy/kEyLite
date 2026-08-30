using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using kEyLite.Services;

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
        // LICENSE.txt 已标记为 PreserveNewest，因此与 exe 同目录。
        string appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        string path = Path.Combine(appDir, "Assets", "Fonts", "LICENSE.txt");
        if (!File.Exists(path))
        {
            // 回退：用源码目录里的文件（方便开发时直接运行）
            string source = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "Assets", "Fonts", "LICENSE.txt"));
            if (!File.Exists(source))
            {
                MessageBox.Show(Window.GetWindow(this),
                    "未能找到字体许可证文件 Assets/Fonts/LICENSE.txt。",
                    "kEyLite",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            path = source;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
