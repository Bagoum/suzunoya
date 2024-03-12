using Avalonia.Controls;
using BeatDetectorApp.ViewModels;
using ScottPlot.Avalonia;
using System;
using BagoumLib.Tasks;

namespace BeatDetectorApp.Views;

public partial class MainWindow : Window {
    private long lastRedraw = -1;
    public MainWindow(MainWindowViewModel ctx) {
        InitializeComponent();
        AvaPlot spectral = this.Find<AvaPlot>("Spectral")!;
        AvaPlot history = this.Find<AvaPlot>("History")!;
        ctx.Plotter.Subscribe(action => {
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (now - lastRedraw > 4) {
                lastRedraw = now;
                _ = action(spectral, history).ContinueWithSync();
            }
        });
    }
}