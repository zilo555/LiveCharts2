// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using LiveChartsCore.SkiaSharpView.VisualElements;
using LiveChartsCore.VisualElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;

namespace CoreTests.OtherTests;

// A geometry that carries its own paints is drawn by SkiaSharpDrawingContext from Fill, then
// Stroke, then Paint. BaseLabelGeometry aliases Fill onto Paint, so both reads returned the same
// instance and every label drawn that way -- chart titles, legends, tooltips, treemap labels --
// was drawn twice: twice the cost, and visibly heavier.
//
// The two ways of drawing a label must agree. A label hosted by a Visual carries its own paint and
// is drawn by the element paint; the same label hosted by the older LabelVisual is registered as a
// geometry of a paint task and is drawn by the active paint, which never double-drew. A
// translucent paint makes any extra pass obvious, because alpha compounds.
[TestClass]
public class LabelDrawnOnceTests
{
    private const string Text = "0000";
    private const float TextSize = 60;
    private const byte Alpha = 128;

    private static SolidColorPaint BuildPaint() =>
        new(SKColors.Black.WithAlpha(Alpha));

    private class LabelOnAVisual : Visual
    {
        protected internal override IDrawnElement? DrawnElement { get; } = new LabelGeometry
        {
            Text = Text,
            TextSize = TextSize,
            X = 40,
            Y = 40,
            HorizontalAlign = Align.Start,
            VerticalAlign = Align.Start,
            Paint = BuildPaint()
        };

        protected override void Measure(Chart chart)
        { }
    }

    private static byte DarkestPixel(IChartElement visual)
    {
        var chart = new SKCartesianChart
        {
            Width = 300,
            Height = 200,
            Background = SKColors.White,
            Series = [],
            VisualElements = [visual]
        };

        using var image = chart.GetImage();
        using var data = image.Encode();
        using var bmp = SKBitmap.Decode(data.ToArray());

        byte darkest = 255;
        for (var x = 0; x < bmp.Width; x++)
            for (var y = 0; y < bmp.Height; y++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Red < darkest) darkest = c.Red;
            }

        return darkest;
    }

    [TestMethod]
    public void ALabelOnAVisualIsDrawnOnceLikeALabelOnAPaintTask()
    {
        // The older family, drawn by a registered paint task: one pass, the reference.
        var onAPaintTask = DarkestPixel(new LabelVisual
        {
            Text = Text,
            TextSize = TextSize,
            X = 40,
            Y = 40,
            // Match the geometry above exactly: the default alignment is Middle, which would put
            // the two labels on different subpixels and make their antialiasing incomparable.
            HorizontalAlignment = Align.Start,
            VerticalAlignment = Align.Start,
            LocationUnit = MeasureUnit.Pixels,
            Paint = BuildPaint()
        });

        // The newer family, drawn by the paint the geometry carries.
        var onAVisual = DarkestPixel(new LabelOnAVisual());

        Assert.IsTrue(
            onAPaintTask < 255,
            "the reference label must actually be drawn, otherwise this test proves nothing");

        Assert.AreEqual(
            onAPaintTask, onAVisual, 4,
            $"A label on a Visual must be drawn once, like a label on a paint task. It came out at " +
            $"{onAVisual} against the reference {onAPaintTask}: darker means it was drawn more than " +
            $"once, and the translucent paint compounded.");
    }
}
