using Factos;
using SharedUITests.Helpers;
using Xunit;

// to run these tests, see the UITests project, specially the program.cs file.
// to enable IDE intellisense for these tests, go to Directory.Build.props and set UITesting to true.

namespace SharedUITests;

public class DisposeTests
{
    public AppController App => AppController.Current;

#if XAML_UI_TESTING
    // https://github.com/Live-Charts/LiveCharts2/issues/1725
    //
    // A chart removed from the visual tree must be collectable — its inner MotionCanvas /
    // SKCanvasView occupy substantial memory and the original report describes them piling up
    // page-after-page on Maui 8. The symptom no longer reproduces on Maui 10 (Application
    // events are routed through a WeakEventManager) but the same invariant — unloaded chart
    // is GC-able — should hold on every Xaml platform we ship. This canary fires if a future
    // platform release (or our own code) regresses to a strong app-level root on charts:
    // a `this`-capturing event subscription that's never detached, an accumulator field, a
    // static collection, etc.
    //
    // Each platform's Test/Dispose/View exposes `ChangeContent()` that swaps the inner
    // content for a fresh one and returns the chart objects from the swapped-out instance.
    // We weak-ref the charts directly because Maui 10's Element.Parent is a WeakReference,
    // so testing leaks via "did the parent page survive?" silently misses real chart leaks.
    [AppTestMethod]
    public async Task UnloadedChartsShouldBeCollectable_Issue1725()
    {
        var sut = await App.NavigateTo<Samples.Test.Dispose.View>();
        await Task.Delay(1000);

        var weakRefs = new List<WeakReference>();

        const int Iterations = 5;
        for (var i = 0; i < Iterations; i++)
        {
            // Add the weak refs in a non-async helper so the swapped-out chart references
            // are never bound to locals that the async state machine could hoist into
            // long-lived fields surviving across the awaits below.
            AddWeakRefs(weakRefs, sut.ChangeContent());
            await Task.Delay(500);
        }

        // One untracked extra swap to push the most-recent tracked instance out of any
        // transient reference the platform's layout/dispatcher holds for the latest unload.
        _ = sut.ChangeContent();

        // Let the platform's dispatcher drain any queued unload work.
        await Task.Delay(2000);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        var alive = weakRefs.Count(r => r.IsAlive);
        Assert.True(
            alive == 0 && weakRefs.Count >= Iterations,
            $"{alive}/{weakRefs.Count} unloaded chart instances were still alive after GC. " +
            "An unloaded chart must not be rooted by anything in the app. The likely cause is " +
            "a `this`-capturing event subscription in the chart that is never detached, or a " +
            "static collection / accumulator field that the chart was added to. See " +
            "https://github.com/Live-Charts/LiveCharts2/issues/1725.");
    }

    private static void AddWeakRefs(List<WeakReference> refs, object[] objects)
    {
        foreach (var o in objects)
            refs.Add(new WeakReference(o));
    }
#endif
}
