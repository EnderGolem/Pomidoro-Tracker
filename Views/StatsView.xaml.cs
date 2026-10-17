using System.Windows;
using System.Windows.Controls;
using Pomidoro.ViewModels;

namespace Pomidoro.Views;

public partial class StatsView : UserControl
{
    public StatsView()
    {
        InitializeComponent();

        // The panel size and the model arrive in no fixed order, so both events are awaited:
        // whichever comes second hands the model the final size.
        DataContextChanged += (_, _) => PushPlotSize();
    }

    // Bars are sized in pixels from the available space, and the size can't be bound
    // directly: ActualWidth is read-only.
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
