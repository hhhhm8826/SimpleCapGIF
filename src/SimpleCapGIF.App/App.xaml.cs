using System.Globalization;
using System.Windows;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        UiCultureResolver.ApplyFromWindows(CultureInfo.CurrentUICulture);
        base.OnStartup(e);
        new MainWindow().Show();
    }
}
