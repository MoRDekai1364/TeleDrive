using System.Windows;
using Microsoft.Win32;
using TeleDrive.Core.Models;

namespace TeleDrive.WPF.Themes;

public static class ThemeManager
{
    public static void Apply(AppTheme theme)
    {
        var effectiveTheme = theme == AppTheme.System ? DetectSystemTheme() : theme;
        var themeUri = effectiveTheme == AppTheme.Light
            ? new Uri("Themes/Light.xaml", UriKind.Relative)
            : new Uri("Themes/Dark.xaml", UriKind.Relative);

        var resources = Application.Current.Resources.MergedDictionaries;
        var existing = resources.FirstOrDefault(d => d.Source is not null
            && (d.Source.OriginalString.EndsWith("Dark.xaml") || d.Source.OriginalString.EndsWith("Light.xaml")));

        var newDictionary = new ResourceDictionary { Source = themeUri };

        if (existing is not null)
        {
            var index = resources.IndexOf(existing);
            resources[index] = newDictionary;
        }
        else
        {
            resources.Insert(0, newDictionary);
        }
    }

    public static AppTheme DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue)
            {
                return intValue == 0 ? AppTheme.Dark : AppTheme.Light;
            }
        }
        catch
        {
        }

        return AppTheme.Dark;
    }
}
