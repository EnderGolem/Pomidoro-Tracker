using System.Windows;

namespace Pomidoro.Views;

// Окно одно на всё приложение, экраны внутри переключаются видимостью.
// Всё поведение живёт в моделях представления, а окном управляет App.
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
