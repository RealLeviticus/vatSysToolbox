using System.Windows;
using System.Windows.Controls;

namespace EurocatMenusPlugin.Views;

public partial class CallsignPopup : UserControl
{
    /// <param name="callsign">The callsign at the top of the popup.</param>
    /// <param name="items">The buttons, from the top to the bottom.</param>
    /// <param name="close">
    /// Closes the popup. A click on a button closes the popup first, and then does the action of the button. Thus, the
    /// action can open a new window.
    /// </param>
    public CallsignPopup(string callsign, IEnumerable<PopupMenuItem> items, Action close)
    {
        InitializeComponent();

        CallsignText.Text = callsign;

        foreach (var item in items)
        {
            var button = new Button { Content = item.Header };
            button.SetResourceReference(StyleProperty, "CallsignPopupButtonStyle");
            button.Click += (_, _) =>
            {
                close();
                item.Click?.Invoke();
            };

            ButtonStack.Children.Add(button);
        }
    }
}
