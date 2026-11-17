using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace vatSysToolbox.Controls;

/// <summary>
/// A text box with the vatSys style.
/// </summary>
public partial class TextInput : UserControl
{
    /// <summary>
    /// Makes a text box.
    /// </summary>
    public TextInput()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Identifies the <see cref="MaxCharacters"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MaxCharactersProperty =
        DependencyProperty.Register(nameof(MaxCharacters), typeof(int), typeof(TextInput), new PropertyMetadata(100));

    /// <summary>
    /// The maximum number of characters. The default is 100.
    /// </summary>
    public int MaxCharacters
    {
        get => (int)GetValue(MaxCharactersProperty);
        set => SetValue(MaxCharactersProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DigitsOnly"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DigitsOnlyProperty =
        DependencyProperty.Register(nameof(DigitsOnly), typeof(bool), typeof(TextInput), new PropertyMetadata(false));

    /// <summary>
    /// If <c>true</c>, the text box accepts only the digits 0 to 9.
    /// </summary>
    public bool DigitsOnly
    {
        get => (bool)GetValue(DigitsOnlyProperty);
        set => SetValue(DigitsOnlyProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="CapturesCursor"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CapturesCursorProperty =
        DependencyProperty.Register(nameof(CapturesCursor), typeof(bool), typeof(TextInput), new PropertyMetadata(false));

    /// <summary>
    /// If <c>true</c>, the mouse cursor stays in the text box while the text box has the keyboard focus. See
    /// <see cref="CursorCapture"/>.
    /// </summary>
    public bool CapturesCursor
    {
        get => (bool)GetValue(CapturesCursorProperty);
        set => SetValue(CapturesCursorProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Value"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(TextInput), new PropertyMetadata(""));

    /// <summary>
    /// The text in the text box.
    /// </summary>
    public string Value
    {
        get => InputBox.Text;
        set => InputBox.Text = value;
    }

    void InputBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (DigitsOnly)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]*$");
        }
    }

    void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DigitsOnly)
        {
            var digitsOnly = Regex.Replace(InputBox.Text, "[^0-9]", "");
            if (InputBox.Text != digitsOnly)
            {
                var caretIndex = InputBox.CaretIndex;
                InputBox.Text = digitsOnly;
                InputBox.CaretIndex = caretIndex > digitsOnly.Length ? digitsOnly.Length : caretIndex;
            }
        }
    }
}
