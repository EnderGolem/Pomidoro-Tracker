using System.Windows;

namespace Pomidoro.Views;

// One window for the whole app; the screens inside are switched by visibility.
// All behavior lives in the view models, and App manages the window.
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
