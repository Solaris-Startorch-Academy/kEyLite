using System.Windows.Media;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;

namespace kEyLite.Services;

/// <summary>Material Design 主题应用（明暗模式 + 主色 / 备选色）。</summary>
public static class ThemeService
{
    /// <summary>应用主题。primaryName / accentName 为 Material Design 内置 Swatch 名称（如 lightBlue）。</summary>
    public static void Apply(string? mode = null, string? primaryName = null, string? accentName = null)
    {
        bool dark = mode switch
        {
            "Dark" => true,
            "Light" => false,
            _ => IsSystemDark(),
        };

        var helper = new PaletteHelper();
        var theme = helper.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);

        var swatches = new SwatchesProvider().Swatches.ToList();
        var primary = FindSwatch(swatches, primaryName) ?? FindSwatch(swatches, "lightBlue");
        var accent = FindSwatch(swatches, accentName) ?? primary;

        if (primary?.ExemplarHue?.Color is Color pc)
            theme.SetPrimaryColor(pc);
        if (accent?.SecondaryExemplarHue?.Color is Color ac)
            theme.SetSecondaryColor(ac);

        helper.SetTheme(theme);
        App.Instance.IsDarkTheme = dark;
    }

    private static Swatch? FindSwatch(IEnumerable<Swatch> swatches, string? name)
        => name is null
            ? null
            : swatches.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}
