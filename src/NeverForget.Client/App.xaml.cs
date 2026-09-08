using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace NeverForget.Client;

public partial class App : Application
{
    public App()
    {
        var englishCulture = CultureInfo.GetCultureInfo("en-GB");
        CultureInfo.DefaultThreadCurrentCulture = englishCulture;
        CultureInfo.DefaultThreadCurrentUICulture = englishCulture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(englishCulture.IetfLanguageTag)));
    }
}
