using ScottPlot;
using ScottPlot.Colormaps;

namespace BeatDetectorApp.Models;

public class NonNormalizedColormap(ArgbColormapBase Inner) : IColormap {
    public Color GetColor(double position) => Inner.GetColor(position);

    public Color GetColor(double position, Range range) {
        if (double.IsNaN(position))
            return Colors.Transparent;
        return Inner.GetColor(position);
    }

    public string Name => Inner.Name;
}