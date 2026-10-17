using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pomidoro.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnDigitsOnlyInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Digits().IsMatch(e.Text);
    }

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex Digits();
}
