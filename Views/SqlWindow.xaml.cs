using System.Windows;
using System.Windows.Input;
using Pomidoro.ViewModels;

namespace Pomidoro.Views;

public partial class SqlWindow : Window
{
    public SqlWindow()
    {
        InitializeComponent();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        bool isRun = e.Key == Key.F5 ||
                     (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control));

        if (!isRun)
        {
            return;
        }

        Run();
        e.Handled = true;
    }

    private void OnRunClick(object sender, RoutedEventArgs e)
    {
        Run();
    }

    // Выделенный фрагмент достаём здесь: SelectedText не является свойством
    // зависимости, и привязать его к модели нечем.
    private void Run()
    {
        if (DataContext is SqlConsoleViewModel viewModel)
        {
            viewModel.Run(QueryBox.SelectionLength > 0 ? QueryBox.SelectedText : null);
        }
    }
}
