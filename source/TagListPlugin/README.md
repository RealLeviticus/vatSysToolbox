# Tag List Plugin

> [!NOTE]
> Even though the development of this plugin was inspired by real ATM systems such as those from Thales Group, the plugin is absolutely NOT affiliated with or endorsed by said company. The plugin and all associated files shall be used solely for the purpose of simulation on the VATSIM network.

A simple example plugin that uses vatSys Toolbox.

The Tag List shows the quick tags in vatSys. Each strip shows the aircraft ID (ACID), the SSR code, and the Mode C level of the quick tag. The list changes when vatSys adds, edits, or removes a quick tag.

## Use

Open the vatSys **Tools** menu. The plugin adds these items:

- **Tag List** shows or hides the Tag List.
- **Rad Tag** opens the vatSys Quick Tag window for the selected track.
- **Del Tag** removes the quick tag of the selected track.

In the Tag List:

- Click the global ops text to edit it. Press Enter or move the focus away to keep the change. Press Escape or move the mouse pointer away to cancel the change.
- Middle-click the ACID to remove the quick tag.

## Messages

The plugin uses two messengers:

- **`IPluginMessenger`** sends messages to other plugins, and receives messages from them. For example, the buttons of the Eurocat Menus plugin control the Tag List.
- **`WeakReferenceMessenger`** sends messages inside the plugin. vatSys does not send events for quick tag changes. Thus, Harmony patches (`VatSysPatches`) send these events to the Tag List view model.

To control the Tag List from your plugin, send these messages to the mailbox `dev.eoinmotherway.tag-list`:

| Message type | Action |
|---|---|
| `dev.eoinmotherway.tag-list.toggle` | Shows or hides the Tag List. |
| `dev.eoinmotherway.tag-list.add-quick-tag` | Opens the Quick Tag window for the selected track. |
| `dev.eoinmotherway.tag-list.remove-quick-tag` | Removes the quick tag of the selected track. |

The plugin sends these messages to all plugins:

| Message type | Meaning |
|---|---|
| `dev.eoinmotherway.tag-list.opened` | The Tag List is shown. |
| `dev.eoinmotherway.tag-list.closed` | The Tag List is hidden. |
