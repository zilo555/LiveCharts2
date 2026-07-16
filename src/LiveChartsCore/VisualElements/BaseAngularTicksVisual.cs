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

using System;
using System.Collections.Generic;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Painting;
using LiveChartsCore.Themes;

namespace LiveChartsCore.VisualElements;

/// <summary>
/// Defines a visual in a chart that draws the ticks of an angular gauge.
/// </summary>
public abstract class BaseAngularTicksVisual : Visual
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseAngularTicksVisual"/> class.
    /// </summary>
    protected BaseAngularTicksVisual()
    {
        // The ticks are drawn on top of the pie slices, and below the pie data labels; they used
        // to get there by pushing this z-index onto the Stroke and LabelsPaint tasks, but a Visual
        // has one task for the whole visual, so the z-index belongs to the visual now, and the
        // labels sit above the ticks by being added to the layout after them. A user-set ZIndex
        // still wins, it just overwrites this default.
        ZIndex = (int)PaintConstants.AngularTicksStrokeZIndex;
    }

    /// <summary>
    /// Gets or sets the labels paint.
    /// </summary>
    public Paint? LabelsPaint
    {
        get;
        set => SetPaintProperty(ref field, value, PaintStyle.Text);
    }

    /// <summary>
    /// Gets or sets the fill paint.
    /// </summary>
    public Paint? Stroke
    {
        get;
        set => SetPaintProperty(ref field, value, PaintStyle.Stroke);
    }

    /// <summary>
    /// Gets or sets the outer offset, the distance between the  edge of the chart and the arc and ticks.
    /// </summary>
    public double OuterOffset { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets or sets the labels outer offset, the distance between the edge of the chart and the labels.
    /// </summary>
    public double LabelsOuterOffset { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets or sets the ticks lenght.
    /// </summary>
    public double TicksLength { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets or sets the labels size.
    /// </summary>
    public double LabelsSize { get; set => SetProperty(ref field, value); } = 12;

    /// <summary>
    /// Gets or sets the labeler, a function that receives a number and return the label content as string.
    /// </summary>
    public Func<double, string> Labeler { get; set => SetProperty(ref field, value); } = Labelers.Default;

    /// <inheritdoc cref="Visual.ApplyStyle(Theme)"/>
    protected override void ApplyStyle(Theme theme) =>
        theme.ApplyStyleTo<BaseAngularTicksVisual>(this);
}

/// <summary>
/// Defines a visual in a chart that draws the ticks of an angular gauge.
/// </summary>
/// <typeparam name="TArcGeometry">The type of the arc geometry.</typeparam>
/// <typeparam name="TLineGeometry">The type of the line geometry.</typeparam>
/// <typeparam name="TLabelGeometry">The type of the label.</typeparam>
public abstract class BaseAngularTicksVisual<TArcGeometry, TLineGeometry, TLabelGeometry> : BaseAngularTicksVisual
    where TArcGeometry : BaseArcGeometry, new()
    where TLineGeometry : BaseLineGeometry, new()
    where TLabelGeometry : BaseLabelGeometry, new()
{
    private const int SubSections = 5;
    private readonly Dictionary<string, TickVisual> _visuals = [];
    private readonly TArcGeometry _arc = new();
    private readonly List<IDrawnElement> _children = [];

    /// <summary>
    /// Hands the geometries this visual drew to the layout that hosts them. A Visual draws one
    /// element, so the ticks live in a layout, and only a typed subclass can build one: a layout
    /// is generic over the drawing context, this class deliberately is not.
    /// </summary>
    /// <param name="children">The geometries to draw, in draw order.</param>
    protected abstract void SetChildren(IReadOnlyList<IDrawnElement> children);

    /// <inheritdoc cref="Visual.Measure(Chart)"/>
    protected override void Measure(Chart chart)
    {
        if (chart is not PieChartEngine pieChart)
            throw new Exception("The AngularTicksVisual can only be added to a pie chart");

        var drawLocation = pieChart.DrawMarginLocation;
        var drawMarginSize = pieChart.DrawMarginSize;

        var minDimension = drawMarginSize.Width < drawMarginSize.Height
            ? drawMarginSize.Width
            : drawMarginSize.Height;

        var view = (IPieChartView)pieChart.View;
        var initialRotation = (float)Math.Truncate(view.InitialRotation);
        var completeAngle = (float)view.MaxAngle;

        var startValue = view.MinValue;
        var endValue = view.MaxValue;

        var cx = drawLocation.X + drawMarginSize.Width * 0.5f;
        var cy = drawLocation.Y + drawMarginSize.Height * 0.5f;

        var h = minDimension;

        var outerRadius = h * 0.5f;
        var ticksDiameter = h - (float)OuterOffset;
        var ticksRadius = ticksDiameter * 0.5f;
        var innerRadius = ticksDiameter * 0.5f - (float)TicksLength;
        var subtickInnerRadius = ticksDiameter * 0.5f - (float)TicksLength * .5f;
        var labelsRadius = outerRadius - (float)LabelsOuterOffset;

        var sweep = completeAngle - 0.1f;

        _arc.CenterX = cx;
        _arc.CenterY = cy;
        _arc.X = drawLocation.X + (drawMarginSize.Width - ticksDiameter) * 0.5f;
        _arc.Y = drawLocation.Y + (drawMarginSize.Height - ticksDiameter) * 0.5f;
        _arc.Width = ticksDiameter;
        _arc.Height = ticksDiameter;
        _arc.StartAngle = initialRotation;
        _arc.SweepAngle = sweep;

        var max = endValue;
        var min = startValue;

        var range = max - min;
        if (range == 0) range = min;

        var separations = 10;
        var minimum = range / separations;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log(minimum) / Math.Log(10)));

        var residual = minimum / magnitude;
        var tick = residual > 5
            ? 10 * magnitude :
            residual > 2
                ? 5 * magnitude
                : residual > 1
                    ? 2 * magnitude
                    : magnitude;

        var labelsSize = (float)LabelsSize;

        var updateId = new object();
        const double toRadians = Math.PI / 180d;

        for (var i = Math.Truncate(min / tick) * tick - tick; i <= max; i += tick)
        {
            var beta = (i - min) / range * sweep;
            beta += initialRotation;
            beta *= toRadians;

            var nextBeta = (i - min + tick) / range * sweep;
            nextBeta += initialRotation;
            nextBeta *= toRadians;

            if (!_visuals.TryGetValue(i.ToString(), out var visual))
            {
                visual = new TickVisual(new(), new(), new TLineGeometry[SubSections]);
                _visuals[i.ToString()] = visual;
            }

            visual.Tick.X = cx + (float)Math.Cos(beta) * innerRadius;
            visual.Tick.Y = cy + (float)Math.Sin(beta) * innerRadius;
            visual.Tick.X1 = cx + (float)Math.Cos(beta) * ticksRadius;
            visual.Tick.Y1 = cy + (float)Math.Sin(beta) * ticksRadius;

            visual.Label.Text = Labeler(i);
            visual.Label.X = cx + (float)Math.Cos(beta) * labelsRadius;
            visual.Label.Y = cy + (float)Math.Sin(beta) * labelsRadius;
            visual.Label.TextSize = labelsSize;
            visual.Label.Paint = LabelsPaint;

            if (i + tick <= max)
            {
                for (var j = 0; j < visual.Subseparator.Length - 1; j++)
                {
                    var subtick = visual.Subseparator[j];
                    subtick ??= visual.Subseparator[j] = new();

                    var alpha = beta + (nextBeta - beta) * (j + 1) / visual.Subseparator.Length;

                    subtick.X = cx + (float)Math.Cos(alpha) * ticksRadius;
                    subtick.Y = cy + (float)Math.Sin(alpha) * ticksRadius;
                    subtick.X1 = cx + (float)Math.Cos(alpha) * subtickInnerRadius;
                    subtick.Y1 = cy + (float)Math.Sin(alpha) * subtickInnerRadius;

                    subtick.Opacity = i + tick * (j + 1) / visual.Subseparator.Length >= min ? 1 : 0;
                }
            }

            var opacity = i >= min ? 1 : 0;
            visual.Label.Opacity = opacity;
            visual.Tick.Opacity = opacity;

            visual.UpdateId = updateId;
        }

        foreach (var key in _visuals.Keys.ToArray())
        {
            if (_visuals[key].UpdateId == updateId) continue;

            // A stale tick is simply not handed to the layout again; there is no paint task to
            // detach it from now that the geometries carry their own paints.
            _ = _visuals.Remove(key);
        }

        BuildChildren();
        SetChildren(_children);
    }

    // Draw order is child order: the arc and the ticks first, the labels last so they read on top,
    // which is what the separate stroke (998) and labels (999) paint z-indexes used to buy.
    private void BuildChildren()
    {
        _children.Clear();

        StyleStroke(_arc);
        _children.Add(_arc);

        foreach (var visual in _visuals.Values)
        {
            StyleStroke(visual.Tick);
            _children.Add(visual.Tick);

            foreach (var subtick in visual.Subseparator)
            {
                if (subtick is null) continue;
                StyleStroke(subtick);
                _children.Add(subtick);
            }
        }

        foreach (var visual in _visuals.Values)
            _children.Add(visual.Label);
    }

    private void StyleStroke(IDrawnElement geometry)
    {
        geometry.Stroke = Stroke;

        // A geometry that carries its own paint is drawn with the thickness set on the geometry,
        // the one on the paint is ignored, so a Stroke with a thickness would silently draw a 1px
        // hairline. Carry it over.
        if (Stroke is not null) geometry.StrokeThickness = Stroke.StrokeThickness;
    }

    private class TickVisual(TLabelGeometry label, TLineGeometry line, TLineGeometry[] subseparator)
    {
        public TLabelGeometry Label { get; set; } = label;
        public TLineGeometry Tick { get; set; } = line;
        public TLineGeometry[] Subseparator { get; set; } = subseparator;
        public object UpdateId { get; set; } = new();
    }
}
