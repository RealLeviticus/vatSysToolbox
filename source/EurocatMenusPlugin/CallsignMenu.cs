using System.Windows.Input;
using EurocatMenusPlugin.Views;
using vatsys;
using vatSysToolbox;

namespace EurocatMenusPlugin;

/// <summary>
/// The menu that opens when the callsign of an aircraft is clicked. It replaces the vatSys aircraft menu.
/// </summary>
/// <remarks>
/// The menu is a WPF popup, and not a form. The ASD focuses itself when the mouse moves, and a form closes when it is
/// not the active form. A popup does not become the active form. While the popup is open, it gets all the mouse input,
/// thus the ASD does not get the mouse moves. A click outside the popup closes it.
/// </remarks>
public class CallsignMenu(IErrorReporter errorReporter)
{
    /// <summary>
    /// <c>true</c> while the menus show. When the menus are hidden, vatSys opens its aircraft menu.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Opens the menu at the mouse cursor. Gives <c>false</c> if the menu is not enabled, or is not for this track. Then
    /// vatSys must open its aircraft menu.
    /// </summary>
    /// <remarks>
    /// vatSys shows a different menu in supervisor mode, and for a track that has no flight plan (for example, a quick
    /// tag). This menu does not replace those menus.
    /// </remarks>
    /// <param name="track">The track of the aircraft.</param>
    /// <param name="isAsmgcs"><c>true</c> if the callsign was clicked on a ground display.</param>
    public bool TryOpen(Track track, bool isAsmgcs)
    {
        if (!IsEnabled || MMI.SupervisorMode)
            return false;

        var fdr = track.GetFDR(isAsmgcs);
        if (fdr is null)
            return false;

        // The top-left corner of the popup is at the tip of the mouse cursor, the same as the vatSys aircraft menu. The
        // mouse button that opened the popup is released on the popup, thus the release does not close it. vatSys menus
        // open and close at once. A popup has no animation by default.
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
            StaysOpen = false,
            AllowsTransparency = true
        };

        var view = new CallsignPopup(fdr.Callsign, CreateItems(track, fdr, isAsmgcs), () => popup.IsOpen = false);

        // A middle-click on the popup closes it
        view.MouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Middle)
                return;

            e.Handled = true;
            popup.IsOpen = false;
        };

        popup.Child = view;
        popup.IsOpen = true;
        return true;
    }

    // These are the same vatSys actions as the items of the vatSys aircraft menu. The buttons show in the same conditions
    // as the items of the vatSys aircraft menu.
    List<PopupMenuItem> CreateItems(Track track, FDP2.FDR fdr, bool isAsmgcs)
    {
        var items = new List<PopupMenuItem>
        {
            CreateItem("FPL", () => MMI.OpenFPWindow(fdr)),
            CreateItem("RTE", () => ToggleRoute(track))
        };

        if (CanChangeRoute(track.State, fdr, isAsmgcs))
        {
            items.Add(CreateItem("DCT", () => MMI.OpenDirectToMenu(fdr, System.Windows.Forms.Cursor.Position)));
            items.Add(CreateItem("RER", () => MMI.StartGraphicReroute(track)));
        }

        if (CanAccept(track.State, fdr, isAsmgcs))
            items.Add(CreateItem("ACC", () => MMI.AcceptJurisdiction(track)));

        if (CanHandOff(track.State, fdr, isAsmgcs))
            items.Add(CreateItem("HND", () => MMI.OpenHandoffMenu(track)));

        return items;
    }

    // The conditions of the Direct and the Reroute items of the vatSys aircraft menu
    static bool CanChangeRoute(MMI.HMIStates state, FDP2.FDR fdr, bool isAsmgcs)
    {
        if (isAsmgcs)
            return false;

        return state switch
        {
            MMI.HMIStates.Jurisdiction => true,
            MMI.HMIStates.Announced or MMI.HMIStates.GhostJurisdiction => fdr.HavePermission,
            _ => false
        };
    }

    // The conditions of the Accept item of the vatSys aircraft menu
    static bool CanAccept(MMI.HMIStates state, FDP2.FDR fdr, bool isAsmgcs)
    {
        if (isAsmgcs)
            return false;

        return state switch
        {
            MMI.HMIStates.NonJurisdiction or MMI.HMIStates.PostJurisdiction => !fdr.IsTracked,
            MMI.HMIStates.Preactive =>
                fdr.State != FDP2.FDR.FDRStates.STATE_PREACTIVE &&
                fdr.State > FDP2.FDR.FDRStates.STATE_COORDINATED &&
                !fdr.IsTracked,
            MMI.HMIStates.Announced or MMI.HMIStates.GhostJurisdiction => fdr.HavePermission
                ? !fdr.IsTrackedByMe && !fdr.IsTracked && fdr.State == FDP2.FDR.FDRStates.STATE_UNCONTROLLED
                : !fdr.IsTracked,
            MMI.HMIStates.HandoverIn or MMI.HMIStates.HandoverOut => true,
            _ => false
        };
    }

    // The conditions of the Handoff item of the vatSys aircraft menu
    static bool CanHandOff(MMI.HMIStates state, FDP2.FDR fdr, bool isAsmgcs)
    {
        if (isAsmgcs)
            return false;

        return state switch
        {
            MMI.HMIStates.Jurisdiction => true,
            MMI.HMIStates.Announced or MMI.HMIStates.GhostJurisdiction => fdr.HavePermission && fdr.IsTrackedByMe,
            _ => false
        };
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
