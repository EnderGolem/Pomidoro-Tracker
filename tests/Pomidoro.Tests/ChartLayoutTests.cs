using Pomidoro.Stats;
using Xunit;

namespace Pomidoro.Tests;

public class ChartLayoutTests
{
    [Fact]
    public void Empty_chart_has_no_scale_and_no_grid()
    {
        ChartScale scale = ChartLayout.Scale(0);

        Assert.True(scale.IsEmpty);
        Assert.Empty(ChartLayout.GridHours(scale));
    }

    [Theory]
    [InlineData(30 * 60, 0.25)]
    [InlineData(3 * 3600, 1.0)]
    [InlineData(10 * 3600, 3.0)]
    [InlineData(23 * 3600, 6.0)]
    public void Grid_step_grows_with_the_longest_day(long maxDaySeconds, double expectedStep)
    {
        Assert.Equal(expectedStep, ChartLayout.Scale(maxDaySeconds).StepHours);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(45 * 60)]
    [InlineData(5 * 3600 + 17 * 60)]
    [InlineData(24 * 3600)]
    public void Axis_never_cuts_the_longest_day_and_keeps_at_most_five_lines(long maxDaySeconds)
    {
        ChartScale scale = ChartLayout.Scale(maxDaySeconds);

        Assert.True(scale.MaxSeconds >= maxDaySeconds);
        Assert.InRange(ChartLayout.GridHours(scale).Count, 2, 5);
    }

    [Fact]
    public void Axis_ceiling_is_a_whole_number_of_steps()
    {
        ChartScale scale = ChartLayout.Scale(5 * 3600 + 17 * 60);

        Assert.Equal(6.0, scale.MaxHours);
        Assert.Equal(2.0, scale.StepHours);
    }

    [Fact]
    public void Zero_line_is_at_the_bottom_and_the_top_line_is_at_the_top()
    {
        ChartScale scale = ChartLayout.Scale(2 * 3600);

        Assert.Equal(100.0, ChartLayout.GridLineTop(0.0, scale, 100.0));
        Assert.Equal(0.0, ChartLayout.GridLineTop(scale.MaxHours, scale, 100.0));
    }

    [Theory]
    [InlineData(400.0, 7, ChartLayout.MaxColumnWidth)]
    [InlineData(400.0, 31, 9.903225806451612)]
    [InlineData(100.0, 31, ChartLayout.MinColumnWidth)]
    [InlineData(400.0, 0, ChartLayout.MinColumnWidth)]
    public void Column_width_is_shared_between_days_within_limits(double plotWidth, int days, double expected)
    {
        Assert.Equal(expected, ChartLayout.ColumnWidth(plotWidth, days), 6);
    }

    [Fact]
    public void Short_session_still_gets_a_visible_segment()
    {
        ChartScale scale = ChartLayout.Scale(8 * 3600);

        Assert.Equal(ChartLayout.MinSegmentHeight, ChartLayout.SegmentHeight(30, scale, 150.0));
    }

    [Fact]
    public void Segment_of_the_longest_day_fills_its_share_of_the_plot()
    {
        ChartScale scale = ChartLayout.Scale(2 * 3600);

        Assert.Equal(75.0, ChartLayout.SegmentHeight(1 * 3600 + 30 * 60, scale, 100.0), 6);
    }

    [Fact]
    public void Plot_never_gets_smaller_than_its_minimum()
    {
        Assert.Equal(ChartLayout.MinPlotHeight, ChartLayout.PlotHeight(10.0));
        Assert.Equal(0.0, ChartLayout.PlotWidth(10.0));
    }
}
