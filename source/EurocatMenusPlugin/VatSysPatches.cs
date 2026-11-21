using System.Reflection;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using HarmonyLib;
using vatsys;
using vatSysToolbox;

namespace EurocatMenusPlugin;

/// <summary>
/// Harmony patches that send vatSys events (for example, <see cref="DisplaySettingsChangedMessage"/>) with
/// <see cref="WeakReferenceMessenger.Default"/>. vatSys does not send events for these changes.
/// </summary>
/// <remarks>
/// The JIT compiler can inline small methods, and then the patch does not run. Thus, these patches are on event
/// handlers, virtual methods, and methods with exception handling, which the JIT compiler does not inline.
/// </remarks>
public static class VatSysPatches
{
    const string HarmonyId = "dev.eoinmotherway.eurocatmenus";

    // The vatSys types that are internal
    const string MainFormTypeName = "vatsys.MainForm";
    const string AsdTypeName = "vatsys.ASDControlDX";
    const string StripPanelTypeName = "vatsys.StripPanelDX";

    // The vatSys menu items that change the display settings
    static readonly string[] MainFormDisplaySettingsMethods =
    [
        "velocityMenuItem_Click",
        "historyMenuItem_Click",
        "timeMenuItem_Click",
        "distanceMenuItem_Click",
        "srpdefaultToolStripMenuItem_Click",
        "srpoffToolStripMenuItem_Click_1",
        "showSSRToolStripMenuItem_Click",
        "extendedToolStripMenuItem_Click",
        "fPASDToolStripMenuItem_Click",
        "sharedLabelsToolStripMenuItem_Click"
    ];

    // The filters in the vatSys Filters menu
    static readonly string[] FiltersDisplaySettingsMethods =
    [
        "UpdateSSRFilter1",
        "UpdateSSRFilter2",
        "SetLowerLevelFilter",
        "SetUpperLevelFilter"
    ];

    // The range, and the keys and mouse buttons that change the status (for example, RST) and the short route probe
    static readonly string[] AsdDisplaySettingsMethods =
    [
        "SetRange",
        "SetZoom",
        "ASD_MouseDown",
        "ASD_PreviewKeyDown"
    ];

    // vatSys can call the patched methods on any thread
    static IErrorReporter? _errorReporter;

    static CallsignMenu? _callsignMenu;

    /// <param name="errorReporter">Reports the exceptions in the patches.</param>
    /// <param name="callsignMenu">Opens in place of the vatSys aircraft menu.</param>
    /// <exception cref="MissingMemberException">
    /// A vatSys member is not found (for example, after a vatSys update).
    /// </exception>
    public static void Apply(IErrorReporter errorReporter, CallsignMenu callsignMenu)
    {
        _errorReporter = errorReporter;
        _callsignMenu = callsignMenu;

        var harmony = new Harmony(HarmonyId);
        var mainFormType = FindType(MainFormTypeName);
        var asdType = FindType(AsdTypeName);

        harmony.Patch(FindMethod(mainFormType, "UpdateClock"), postfix: Patch(nameof(ClockPostfix)));

        // A click on a callsign in the ASD, the strips, or the sequence ladder opens the aircraft menu
        harmony.Patch(
            FindMethod(typeof(MMI), nameof(MMI.OpenAircraftMenu)),
            prefix: Patch(nameof(OpenAircraftMenuPrefix)));

        foreach (var methodName in MainFormDisplaySettingsMethods)
            PatchDisplaySettings(harmony, mainFormType, methodName);

        foreach (var methodName in FiltersDisplaySettingsMethods)
            PatchDisplaySettings(harmony, typeof(FiltersMenuControl), methodName);

        foreach (var methodName in AsdDisplaySettingsMethods)
            PatchDisplaySettings(harmony, asdType, methodName);

        // The short route probe keys in the strips
        PatchDisplaySettings(harmony, FindType(StripPanelTypeName), "OnPreviewKeyDown");

        // All windows, also the windows of other plugins
        harmony.Patch(
            FindMethod(typeof(Form), "OnVisibleChanged"),
            postfix: Patch(nameof(WindowChangedPostfix)));
        harmony.Patch(
            FindMethod(typeof(Form), "OnActivated"),
            postfix: Patch(nameof(WindowChangedPostfix)));
        harmony.Patch(
            FindMethod(typeof(Form), "OnFormClosed"),
            postfix: Patch(nameof(WindowClosedPostfix)));
    }

    static void PatchDisplaySettings(Harmony harmony, Type type, string methodName)
    {
        harmony.Patch(FindMethod(type, methodName), postfix: Patch(nameof(DisplaySettingsPostfix)));
    }

    // Opens the callsign menu in place of the vatSys aircraft menu. If the callsign menu is not for this track, or if it
    // cannot open, vatSys opens its aircraft menu. The names of the parameters must be the same as in the vatSys method.
    static bool OpenAircraftMenuPrefix(Track Source, bool isASMGCS)
    {
        if (_callsignMenu is null)
            return true;

        try
        {
            return !_callsignMenu.TryOpen(Source, isASMGCS);
        }
        catch (Exception ex)
        {
            _errorReporter?.ReportError(ex, "Cannot open the callsign menu");
            return true;
        }
    }

    static void ClockPostfix()
    {
        Send(new ClockUpdatedMessage());
    }

    static void DisplaySettingsPostfix()
    {
        Send(new DisplaySettingsChangedMessage());
    }

    static void WindowChangedPostfix(Form __instance)
    {
        Send(new WindowChangedMessage(__instance));
    }

    static void WindowClosedPostfix(Form __instance)
    {
        Send(new WindowClosedMessage(__instance));
    }

    // The messenger calls the recipients on this thread. An exception goes to the vatSys code that called the patched
    // method. Thus, report it here.
    static void Send<TMessage>(TMessage message)
        where TMessage : class
    {
        try
        {
            WeakReferenceMessenger.Default.Send(message);
        }
        catch (Exception ex)
        {
            _errorReporter?.ReportError(ex, $"Cannot send the vatSys event {typeof(TMessage).Name}");
        }
    }

    static HarmonyMethod Patch(string methodName) => new(typeof(VatSysPatches), methodName);

    static Type FindType(string typeName)
    {
        return typeof(MMI).Assembly.GetType(typeName) ?? throw new MissingMemberException(typeName);
    }

    static MethodInfo FindMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName) ?? throw new MissingMemberException(type.Name, methodName);
    }
}
