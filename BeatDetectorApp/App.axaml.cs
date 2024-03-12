using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BeatDetectorApp.ViewModels;
using BeatDetectorApp.Views;

namespace BeatDetectorApp;

public partial class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var ctx = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow(ctx) {
                DataContext = ctx,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}