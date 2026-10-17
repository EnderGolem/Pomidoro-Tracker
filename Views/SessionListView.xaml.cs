using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Pomidoro.Views;

// Код здесь только про ввод с клавиатуры: что делать с набранным, решает модель строки.
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

    // Привязка отдаёт значение в модель по потере фокуса, а Enter фокус не уводит.
    // Keyboard.ClearFocus() здесь не годится: он снимает фокус клавиатуры, но не
    // логический, и событие LostFocus не наступает — правка молча не доходила.
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
