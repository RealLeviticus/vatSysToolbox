using System.ComponentModel.Composition;
using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using TagListPlugin.ViewModels;
using TagListPlugin.Views;
using vatSysToolbox;
using vatSysToolbox.Extensions;
using vatSysToolbox.Messaging;
using vatSysToolbox.Windows;

namespace TagListPlugin;

/// <summary>
/// Shows the quick tags in a list. Other plugins can show the list, and add or remove quick tags, with
/// <see cref="TagListMessages"/>.
/// </summary>
[Export(typeof(IPlugin))]
public class Plugin : IPlugin
{
#if DEBUG
    public const string Name = "Tag List - Debug";
#else
    public const string Name = "Tag List";
#endif

    /// <summary>
    /// The name that other plugins use to send messages to this plugin.
    /// </summary>
    public const string MailboxName = "dev.eoinmotherway.tag-list";

    const string WindowKey = "TagList";
    const string WindowTitle = "Tag List";

    // The messages that the plugin sends have no data
    static readonly MessageContent EmptyContent = new("application/json", [.. "{}"u8]);

    readonly IGuiInvoker _guiInvoker;
    readonly IErrorReporter _errorReporter;
    readonly IPluginMessenger _messenger;
    readonly WindowManager _windowManager;
    readonly ToolStripMenuItem _tagListMenuItem;

    string IPlugin.Name => Name;

    public Plugin()
    {
        DpiAwareness.EnsureDpiAwareness();

        var serviceProvider = new ServiceCollection()
            .AddLogging()
            .AddVatSysToolbox(Name, MailboxName)
            .BuildServiceProvider();

        _guiInvoker = serviceProvider.GetRequiredService<IGuiInvoker>();
        _errorReporter = serviceProvider.GetRequiredService<IErrorReporter>();
        _messenger = serviceProvider.GetRequiredService<IPluginMessenger>();
        _windowManager = serviceProvider.GetRequiredService<WindowManager>();

        _tagListMenuItem = AddMenuItem("Tag List", ToggleTagList);
        AddMenuItem("Rad Tag", OpenQuickTagWindow);
        AddMenuItem("Del Tag", RemoveSelectedQuickTag);

        _messenger.Subscribe(TagListMessages.Toggle, ToggleTagList);
        _messenger.Subscribe(TagListMessages.AddQuickTag, OpenQuickTagWindow);
        _messenger.Subscribe(TagListMessages.RemoveQuickTag, RemoveSelectedQuickTag);

        _messenger.Start();

        // vatSys does not send events for the quick tags and the radar tracks. Thus, the patches send the events.
        try
        {
            VatSysPatches.Apply(_errorReporter);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex, "Cannot send the vatSys events. The Tag List does not show the changes.");
        }
    }

    void OpenTagList()
    {
        _windowManager.TryCreateWindow(
            WindowKey,
            WindowTitle,
            _ => new TagList(new TagListViewModel(WeakReferenceMessenger.Default, _guiInvoker, _errorReporter)),
            sizeToContent: false,
            canClose: false,
            canMinimise: false);
    }

    // Adds an item to the vatSys Tools menu. The click occurs on the UI thread.
    ToolStripMenuItem AddMenuItem(string text, Action click)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) =>
        {
            // An exception that is not caught on the UI thread stops vatSys.
            try
            {
                click();
            }
            catch (Exception ex)
            {
                _errorReporter.ReportError(ex);
            }
        };

        MMI.AddCustomMenuItem(new CustomToolStripMenuItem(
            CustomToolStripMenuItemWindowType.Main,
            CustomToolStripMenuItemCategory.Tools,
            item));

        return item;
    }

    // These actions can come from a menu item on the UI thread, or from a message on a background thread. They change
    // the UI or the vatSys state, thus they run on the UI thread. On the UI thread, InvokeOnGUI runs the action
    // immediately.

    void ToggleTagList()
    {
        _guiInvoker.InvokeOnGUI(_ =>
        {
            // On the UI thread, the window manager opens and closes the window immediately. Thus, two quick toggles
            // cannot both open the window.
            if (_windowManager.TryGetWindow(WindowKey, out _))
            {
                _windowManager.CloseWindow(WindowKey);
                _tagListMenuItem.Checked = false;
                SendState(TagListMessages.Closed);
            }
            else
            {
                OpenTagList();
                _tagListMenuItem.Checked = true;
                SendState(TagListMessages.Opened);
            }
        });
    }

    void OpenQuickTagWindow()
    {
        _guiInvoker.InvokeOnGUI(mainForm =>
        {
            // A quick tag is only for a radar track that is not coupled to a flight plan. vatSys uses the same rule to
            // enable Tools, Quick Tag.
            var selectedTrack = MMI.SelectedTrack;
            if (selectedTrack?.SourceData is not RDP.RadarTrack || selectedTrack.GetFDR() is not null)
                return;

            var screen = Screen.FromControl(mainForm).WorkingArea;
            using var window = CreateQuickTagWindow(selectedTrack.GetRadarTrack());
            window.StartPosition = FormStartPosition.Manual;
            window.Location = new Point(
                screen.Left + (screen.Width - window.Width) / 2,
                screen.Top + (screen.Height - window.Height) / 2);

            window.ShowDialog();
        });
    }
    
    void RemoveSelectedQuickTag()
    {
        _guiInvoker.InvokeOnGUI(_ =>
        {
            var selectedTrack = MMI.SelectedTrack;
            if (selectedTrack?.SourceData is not RDP.RadarTrack)
                return;

            RDP.RemoveQuickTag(selectedTrack.GetQuickTag());

            // vatSys does not change the state of the track when the quick tag is removed. Thus, do the same as vatSys
            // and set the state again, so that the track is not shown as jurisdiction.
            MMI.SetTrackState(selectedTrack);
        });
    }
    
    Form CreateQuickTagWindow(RDP.RadarTrack radarTrack)
    {
        const string quickTagWindowTypeName = "vatsys.QuickTagWindow";
        var type = typeof(MMI).Assembly.GetType(quickTagWindowTypeName)
                   ?? throw new TypeLoadException($"Cannot find the type {quickTagWindowTypeName}");

        try
        {
            return (Form)Activator.CreateInstance(type, radarTrack);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Show the exception from vatSys, and not the exception from reflection
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    // Tells the other plugins (for example, the Tag List button of the Eurocat Menus plugin) that the Tag List is shown
    // or hidden
    void SendState(string messageType)
    {
        try
        {
            _messenger.Broadcast(messageType, EmptyContent);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex, "Cannot send the state of the Tag List");
        }
    }

    public void OnFDRUpdate(FDP2.FDR updated) { }

    public void OnRadarTrackUpdate(RDP.RadarTrack updated) { }
}
