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

using System.Linq;
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

// The library builds custom visuals two ways: the older VisualElement and the newer Visual, and
// IChartView.VisualElements takes IChartElement, so it holds either. GetVisualsAt cast every
// element to VisualElement and threw an InvalidCastException on anything built on Visual, which
// is every visual in the General/VisualElements samples.
[TestClass]
public class GetVisualsAtTests
{
    private const int X = 100;
    private const int Y = 100;
    private const int Size = 40;

    // Sits at (100, 100) and spans 40x40, so (110, 110) hits and (10, 10) misses.
    private class TestVisual : Visual
    {
        private readonly RectangleGeometry _rectangle = new()
        {
            X = X,
            Y = Y,
            Width = Size,
            Height = Size,
            Fill = new SolidColorPaint(SKColors.Red)
        };

        protected internal override IDrawnElement? DrawnElement => _rectangle;

        protected override void Measure(Chart chart)
        { }
    }

    private static SKCartesianChart BuildChart(params IChartElement[] visuals) =>
        new()
        {
            Width = 300,
            Height = 300,
            Series = [new LineSeries<double> { Values = [1, 2, 3] }],
            VisualElements = visuals
        };

    [TestMethod]
    public void VisualIsHit()
    {
        var visual = new TestVisual();
        var chart = BuildChart(visual);

        _ = chart.GetImage();

        var hit = chart.GetVisualsAt(new LvcPointD(X + Size * 0.5, Y + Size * 0.5)).ToArray();

        Assert.AreEqual(1, hit.Length, "The visual must be hit inside its bounds.");
        Assert.AreSame(visual, hit[0]);
    }

    [TestMethod]
    public void VisualIsNotHitOutsideItsBounds()
    {
        var chart = BuildChart(new TestVisual());

        _ = chart.GetImage();

        var hit = chart.GetVisualsAt(new LvcPointD(X - Size, Y - Size)).ToArray();

        Assert.AreEqual(0, hit.Length, "The visual must not be hit outside its bounds.");
    }

    [TestMethod]
    public void VisualElementIsStillHit()
    {
        // The older family must keep working, it runs its own IsHitBy.
        var visualElement = new GeometryVisual<RectangleGeometry>
        {
            X = X,
            Y = Y,
            Width = Size,
            Height = Size,
            Fill = new SolidColorPaint(SKColors.Red),
            LocationUnit = MeasureUnit.Pixels,
            SizeUnit = MeasureUnit.Pixels
        };

        var chart = BuildChart(visualElement);

        _ = chart.GetImage();

        var hit = chart.GetVisualsAt(new LvcPointD(X + Size * 0.5, Y + Size * 0.5)).ToArray();

        Assert.AreEqual(1, hit.Length, "A VisualElement must still be hit.");
        Assert.AreSame(visualElement, hit[0]);
    }

    [TestMethod]
    public void BothFamiliesCoexistInTheSameCollection()
    {
        // The real regression: one Visual in the collection threw before any element was tested,
        // so a chart mixing both families could not be hit tested at all.
        var visual = new TestVisual();
        var visualElement = new GeometryVisual<RectangleGeometry>
        {
            X = X,
            Y = Y,
            Width = Size,
            Height = Size,
            Fill = new SolidColorPaint(SKColors.Blue),
            LocationUnit = MeasureUnit.Pixels,
            SizeUnit = MeasureUnit.Pixels
        };

        var chart = BuildChart(visualElement, visual);

        _ = chart.GetImage();

        var hit = chart.GetVisualsAt(new LvcPointD(X + Size * 0.5, Y + Size * 0.5)).ToArray();

        Assert.AreEqual(2, hit.Length, "Both families must be hit when they overlap.");
        CollectionAssert.Contains(hit, visual);
        CollectionAssert.Contains(hit, visualElement);
    }
}
