using System.Collections.Generic;
using ScottPlot;

namespace BeatDetectorApp.Models;

public class ScNoAutoScaler : IAutoScaler {

    public bool InvertedX { get; set; } = false;
    public bool InvertedY { get; set; } = false;
    public AxisLimits GetAxisLimits(Plot plot, IXAxis xAxis, IYAxis yAxis) {
        return new AxisLimits(xAxis.Min, xAxis.Max, yAxis.Min, yAxis.Max);
    }

    public void AutoScaleAll(IEnumerable<IPlottable> plottables) {}
}