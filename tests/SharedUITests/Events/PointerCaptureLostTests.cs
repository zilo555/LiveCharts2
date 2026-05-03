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

using Factos;
using LiveChartsCore.Kernel.Sketches;
using SharedUITests.Helpers;
using Xunit;

#if WPF_UI_TESTING
using System.Reflection;
using System.Windows;
using System.Windows.Input;
#endif

#if AVALONIA_UI_TESTING
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Threading;
#endif

namespace SharedUITests.Events;

public class PointerCaptureLostTests
{
    public AppController App => AppController.Current;

#if WPF_UI_TESTING
    // regression for https://github.com/Live-Charts/LiveCharts2/issues/1576
    // when an ancestor (e.g. a ToggleButton wrapping the chart) calls CaptureMouse
    // during the same mouse-down burst, capture is stolen from the chart and the
    // chart never sees MouseUp; pan/drag state stays armed and any subsequent
    // MouseMove keeps panning. The chart must treat capture loss as a synthetic
    // pointer-up so the drag state always releases.
    [AppTestMethod]
    public async Task Wpf_lost_mouse_capture_releases_drag_state()
    {
        var sut = await App.NavigateTo<Samples.General.FirstChart.View>();
        await sut.Chart.WaitUntilChartRenders();

        var chart = (FrameworkElement)sut.Chart;
        var view = (IChartView)sut.Chart;

        var sourceGenChartType = WalkBaseTypes(chart.GetType(), "SourceGenChart");
        Assert.NotNull(sourceGenChartType);

        var isPointerDownField = sourceGenChartType!.GetField(
            "_isPointerDown", BindingFlags.Instance | BindingFlags.NonPublic);
        var onLostCapture = sourceGenChartType.GetMethod(
            "OnLostMouseCapture", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(isPointerDownField);
        Assert.NotNull(onLostCapture);

        var coreChart = view.CoreChart;
        var chartType = WalkBaseTypes(coreChart.GetType(), "Chart");
        Assert.NotNull(chartType);
        var isPanningField = chartType!.GetField(
            "_isPanning", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(isPanningField);

        await chart.Dispatcher.InvokeAsync(() =>
        {
            // Simulate the state right after a MouseDown on the chart: the WPF-side
            // flag is set and the core chart is panning.
            isPointerDownField!.SetValue(chart, true);
            isPanningField!.SetValue(coreChart, true);

            // An ancestor steals capture (the #1576 ToggleButton scenario). No MouseUp
            // will arrive at the chart; the LostMouseCapture handler must clean up.
            var args = new MouseEventArgs(Mouse.PrimaryDevice, 0)
            {
                RoutedEvent = Mouse.LostMouseCaptureEvent
            };
            _ = onLostCapture!.Invoke(chart, [chart, args]);

            Assert.False(
                (bool)isPointerDownField.GetValue(chart)!,
                "_isPointerDown should be reset by OnLostMouseCapture");
            Assert.False(
                (bool)isPanningField.GetValue(coreChart)!,
                "Chart._isPanning should be reset by OnLostMouseCapture");
        });
    }
#endif

#if AVALONIA_UI_TESTING
    // regression mirror for https://github.com/Live-Charts/LiveCharts2/issues/1576
    // Avalonia has the same class of issue when an ancestor re-captures the pointer
    // mid-gesture (reported by MajesticBevans in the same thread). The chart must
    // treat capture loss as a synthetic pointer-up.
    [AppTestMethod]
    public async Task Avalonia_pointer_capture_lost_releases_drag_state()
    {
        var sut = await App.NavigateTo<Samples.General.FirstChart.View>();
        await sut.Chart.WaitUntilChartRenders();

        var chart = (Control)sut.Chart;
        var view = (IChartView)sut.Chart;

        var sourceGenChartType = WalkBaseTypes(chart.GetType(), "SourceGenChart");
        Assert.NotNull(sourceGenChartType);

        var isPointerDownField = sourceGenChartType!.GetField(
            "_isPointerDown", BindingFlags.Instance | BindingFlags.NonPublic);
        var onCaptureLost = sourceGenChartType.GetMethod(
            "OnPointerCaptureLost", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(isPointerDownField);
        Assert.NotNull(onCaptureLost);

        var coreChart = view.CoreChart;
        var chartType = WalkBaseTypes(coreChart.GetType(), "Chart");
        Assert.NotNull(chartType);
        var isPanningField = chartType!.GetField(
            "_isPanning", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(isPanningField);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            isPointerDownField!.SetValue(chart, true);
            isPanningField!.SetValue(coreChart, true);

            // PointerCaptureLostEventArgs has no public constructor; pass null since
            // OnPointerCaptureLost does not read its argument.
            _ = onCaptureLost!.Invoke(chart, [chart, null]);

            Assert.False(
                (bool)isPointerDownField.GetValue(chart)!,
                "_isPointerDown should be reset by OnPointerCaptureLost");
            Assert.False(
                (bool)isPanningField.GetValue(coreChart)!,
                "Chart._isPanning should be reset by OnPointerCaptureLost");
        });
    }
#endif

#if WPF_UI_TESTING || AVALONIA_UI_TESTING
    private static Type? WalkBaseTypes(Type? start, string name)
    {
        for (var t = start; t is not null; t = t.BaseType)
            if (t.Name == name) return t;
        return null;
    }
#endif
}
