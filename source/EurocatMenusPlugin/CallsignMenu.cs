using System.Windows.Forms;
using EurocatMenusPlugin.Views;
using vatsys;
using vatSysToolbox;
using vatSysToolbox.Windows;

namespace EurocatMenusPlugin;

/// <summary>
/// The menu that opens when the callsign of an aircraft is clicked. It replaces the vatSys aircraft menu.
/// </summary>
public class CallsignMenu(WindowManager windowManager, IErrorReporter errorReporter)
{
    /// <summary>
    /// Opens the menu at the mouse cursor. Gives <c>false</c> if the menu is not for this track. Then vatSys must open
    /// its aircraft menu.
    /// </summary>
    /// <remarks>
    /// vatSys shows a different menu in supervisor mode, and for a track that has no flight plan (for example, a quick
    /// tag). This menu does not replace those menus.
    /// </remarks>
    /// <param name="track">The track of the aircraft.</param>
    /// <param name="isAsmgcs"><c>true</c> if the callsign was clicked on a ground display.</param>
    public bool TryOpen(Track track, bool isAsmgcs)
    {
        if (MMI.SupervisorMode)
            return false;

        var fdr = track.GetFDR(isAsmgcs);
        if (fdr is null)
            return false;

        var items = CreateItems(track, fdr);
        windowManager.ShowPopup(windowHandle => new CallsignPopup(fdr.Callsign, items, windowHandle.Close));
        return true;
    }

    // These are the same vatSys actions as the items of the vatSys aircraft menu
    List<PopupMenuItem> CreateItems(Track track, FDP2.FDR fdr)
    {
        var items = new List<PopupMenuItem>
        {
            CreateItem("FPL", () => MMI.OpenFPWindow(fdr)),
            CreateItem("RTE", () => ToggleRoute(track)),
            CreateItem("DCT", () => MMI.OpenDirectToMenu(fdr, Cursor.Position)),
            CreateItem("RER", () => MMI.StartGraphicReroute(track))
        };

        if (track.State == MMI.HMIStates.HandoverIn)
            items.Add(CreateItem("ACC", () => MMI.AcceptJurisdiction(track)));

        items.Add(CreateItem("HND", () => MMI.OpenHandoffMenu(track)));
        return items;
    }

    // vatSys shows the route, or hides it if it shows
    static void ToggleRoute(Track track)
    {
        if (track.GraphicRTE)
            MMI.HideGraphicRoute(track);
        else
            MMI.ShowGraphicRoute(track);
    }

    PopupMenuItem CreateItem(string header, Action action)
    {
        return new PopupMenuItem(header, () =>
        {
            // An exception that is not caught on the UI thread stops vatSys.
            try
            {
                action();
            }
            catch (Exception ex)
            {
                errorReporter.ReportError(ex, $"Cannot do {header}");
            }
        });
    }
}
