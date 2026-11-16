using System.Reflection;
using CommunityToolkit.Mvvm.Messaging;
using HarmonyLib;
using vatsys;
using vatSysToolbox;

namespace TagListPlugin;

/// <summary>
/// Harmony patches that send vatSys events via <see cref="WeakReferenceMessenger.Default"/>.
/// vatSys does not send events for these changes, so we need to inject them ourselves.
/// </summary>
public static class VatSysPatches
{
    const string HarmonyId = "dev.eoinmotherway.tag-list";

    static IErrorReporter? _errorReporter;

    /// <exception cref="MissingMemberException">
    /// A vatSys member is not found (for example, after a vatSys update).
    /// </exception>
    public static void Apply(IErrorReporter errorReporter)
    {
        _errorReporter = errorReporter;

        var harmony = new Harmony(HarmonyId);

        harmony.Patch(
            FindMethod(typeof(RDP), nameof(RDP.AddQuickTag)),
            postfix: Patch(nameof(AddQuickTagPostfix)));

        harmony.Patch(
            FindMethod(typeof(RDP), nameof(RDP.EditQuickTag)),
            postfix: Patch(nameof(EditQuickTagPostfix)));

        harmony.Patch(
            FindMethod(typeof(RDP), nameof(RDP.RemoveQuickTag)),
            postfix: Patch(nameof(RemoveQuickTagPostfix)));

        harmony.Patch(
            FindSetter(typeof(QuickTag), nameof(QuickTag.LabelData)),
            postfix: Patch(nameof(LabelDataPostfix)));

        harmony.Patch(
            FindSetter(typeof(QuickTag), nameof(QuickTag.CFL)),
            postfix: Patch(nameof(CflPostfix)));

        // vatSys sets the altitude at each radar update, also when it does not change
        harmony.Patch(
            FindSetter(typeof(RDP.RadarTrack), nameof(RDP.RadarTrack.CorrectedAltitude)),
            prefix: Patch(nameof(CorrectedAltitudePrefix)),
            postfix: Patch(nameof(CorrectedAltitudePostfix)));
    }

    // The names of the parameters must be the same as in the vatSys methods

    static void AddQuickTagPostfix(QuickTag qt)
    {
        // vatSys does not add the quick tag if a parameter is null
        if (qt is null)
            return;
        
        Send(new QuickTagAddedMessage(qt));
    }

    static void EditQuickTagPostfix(QuickTag oldqt, QuickTag newqt)
    {
        if (oldqt is null || newqt is null)
            return;

        Send(new QuickTagRemovedMessage(oldqt));
        Send(new QuickTagAddedMessage(newqt));
    }

    static void RemoveQuickTagPostfix(QuickTag qt)
    {
        if (qt is null)
            return;
        
        Send(new QuickTagRemovedMessage(qt));
    }

    static void LabelDataPostfix(QuickTag __instance)
    {
        Send(new QuickTagLabelDataChangedMessage(__instance));
    }

    static void CflPostfix(QuickTag __instance)
    {
        Send(new QuickTagCflChangedMessage(__instance));
    }

    // Keeps the altitude before the change, so that the postfix sends the event only when the altitude changes
    static void CorrectedAltitudePrefix(RDP.RadarTrack __instance, out int __state)
    {
        __state = __instance.CorrectedAltitude;
    }

    static void CorrectedAltitudePostfix(RDP.RadarTrack __instance, int __state)
    {
        if (__instance.CorrectedAltitude == __state)
            return;
        
        Send(new RadarTrackAltitudeChangedMessage(__instance));
    }

    static void Send<TMessage>(TMessage message)
        where TMessage : class
    {
        try
        {
            WeakReferenceMessenger.Default.Send(message);
        }
        catch (Exception ex)
        {
            // Ensure all exceptions are caught here, otherwise they'll be thrown back to the patches vatSys method.
            _errorReporter?.ReportError(ex, $"Cannot send the vatSys event {typeof(TMessage).Name}");
        }
    }

    static HarmonyMethod Patch(string methodName) => new(typeof(VatSysPatches), methodName);

    static MethodInfo FindMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName) ?? throw new MissingMemberException(type.Name, methodName);
    }

    static MethodInfo FindSetter(Type type, string propertyName)
    {
        return AccessTools.PropertySetter(type, propertyName)
            ?? throw new MissingMemberException(type.Name, propertyName);
    }
}
