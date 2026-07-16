using System.Globalization;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.SKCharts;
using LiveChartsCore.SkiaSharpView.VisualElements;
using SkiaSharp;

namespace SnapshotTests;

// The angular gauge had no pixel coverage: the Gauge* snapshots are radial gauges and never draw a
// NeedleVisual or an AngularTicksVisual. This mirrors the Pies/AngularGauge sample, so the needle
// (one geometry) and the ticks (an arc, ticks, subticks and labels) are both pinned.
[TestClass]
public sealed class AngularGaugeTests
{
    private static void SetStyle(float outer, float width, PieSeries<ObservableValue> series)
    {
        series.OuterRadiusOffset = outer;
        series.MaxRadialColumnWidth = width;
    }

    private static SKPieChart BuildGauge() =>
        new()
        {
            Width = 600,
            Height = 600,
            Background = SKColors.White,
            Series = GaugeGenerator.BuildAngularGaugeSections(
                new GaugeItem(60, s => SetStyle(130, 20, s)),
                new GaugeItem(30, s => SetStyle(130, 20, s)),
                new GaugeItem(10, s => SetStyle(130, 20, s))),
            VisualElements = [
                new AngularTicksVisual
                {
                    Labeler = value => value.ToString("N1", CultureInfo.InvariantCulture),
                    LabelsSize = 16,
                    LabelsOuterOffset = 15,
                    OuterOffset = 65,
                    TicksLength = 20
                },
                new NeedleVisual { Value = 45 }
            ],
            InitialRotation = -225,
            MaxAngle = 270,
            MinValue = 0,
            MaxValue = 100,
        };

    [TestMethod]
    public void AngularGauge()
    {
        var chart = BuildGauge();

        chart.AssertSnapshotMatches($"{nameof(AngularGaugeTests)}_{nameof(AngularGauge)}");
    }

    // The needle sits on top of the sections and the ticks; a value at the range edge also pins the
    // tick opacity rules, which hide the ticks that fall outside MinValue.
    [TestMethod]
    public void AngularGaugeAtMinValue()
    {
        var chart = BuildGauge();
        chart.VisualElements = [
            new AngularTicksVisual
            {
                Labeler = value => value.ToString("N1", CultureInfo.InvariantCulture),
                LabelsSize = 16,
                LabelsOuterOffset = 15,
                OuterOffset = 65,
                TicksLength = 20
            },
            new NeedleVisual { Value = 0 }
        ];

        chart.AssertSnapshotMatches($"{nameof(AngularGaugeTests)}_{nameof(AngularGaugeAtMinValue)}");
    }
}
