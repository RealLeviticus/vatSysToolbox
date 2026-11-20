using vatsys;
using vatSysToolbox;
using vatSysToolbox.Windows;

namespace EurocatMenusPlugin.FlightPlan;

/// <summary>
/// Opens the Flight Plan Window of this plugin: <see cref="FlightPlanView"/> in a vatSys window. The window has the
/// same title bar as the other vatSys windows (close, minimise, and send to back), and it sizes to the view.
/// <see cref="FlightPlanWindowPatches"/> opens it in place of the vatSys Flight Plan Window.
/// </summary>
/// <param name="windowManager">Opens the windows.</param>
/// <param name="errorReporter">Reports the exceptions that occur in the windows.</param>
public class FlightPlanWindow(WindowManager windowManager, IErrorReporter errorReporter)
{
    const string KeyPrefix = "dev.eoinmotherway.eurocatmenus.flight-plan-window.";
    const string Title = "FLIGHT PLAN WINDOW";

    int _windowCount;

    /// <summary>
    /// Opens a new window. Call this method on the UI thread, so that the window is open when the method returns.
    /// </summary>
    /// <param name="fdr">The flight plan to show, or <c>null</c> for an empty window.</param>
    public void Open(FDP2.FDR? fdr)
    {
        // Each window has its own key, so that more than one window can be open, the same as the vatSys window
        var key = KeyPrefix + Interlocked.Increment(ref _windowCount);

        windowManager.Open(
            key,
            Title,
            windowHandle => new FlightPlanView(new FlightPlanViewModel(fdr, errorReporter), windowHandle));
    }
}
