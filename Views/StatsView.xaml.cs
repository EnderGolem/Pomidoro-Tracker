using System.Windows;
using System.Windows.Controls;
using Pomidoro.ViewModels;

namespace Pomidoro.Views;

public partial class StatsView : UserControl
{
    public StatsView()
    {
        InitializeComponent();

        // Размер панели и модель появляются в разном порядке, поэтому ждём оба события:
        // какое придёт вторым, то и отдаст модели окончательный размер.
        DataContextChanged += (_, _) => PushPlotSize();
    }

    // Столбики считаются в пикселях от доступного места, а привязать размер
    // напрямую нечем: ActualWidth только для чтения.
    private void OnChartHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        PushPlotSize();
    }

    private void PushPlotSize()
    {
        if (ChartHost.ActualWidth > 0.0 && ChartHost.ActualHeight > 0.0 && DataContext is StatsViewModel viewModel)
        {
            viewModel.SetPlotSize(ChartHost.ActualWidth, ChartHost.ActualHeight);
        }
    }
}
