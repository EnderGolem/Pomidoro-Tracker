using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Pomidoro.Views;

// Code here is only about keyboard input: the row model decides what to do with the text.
public partial class SessionListView : UserControl
{
    public SessionListView()
    {
        InitializeComponent();
    }

    private void OnDigitsOnlyInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Digits().IsMatch(e.Text);
    }

    // The binding pushes the value to the model on focus loss, and Enter doesn't move focus.
    // Keyboard.ClearFocus() won't do here: it clears keyboard focus but not
    // logical focus, so LostFocus never fires and the edit was silently lost.
    private void OnCellKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box)
        {
            return;
        }

        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex Digits();
}
