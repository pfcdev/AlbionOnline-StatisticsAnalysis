using StatisticsAnalysisTool.Trade.Market;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StatisticsAnalysisTool.UserControls;

public partial class MarketActivityControl : UserControl
{
    public MarketActivityControl()
    {
        InitializeComponent();
    }

    private void ActivityList_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var dependencyObject = e.OriginalSource as DependencyObject;
        while (dependencyObject != null && dependencyObject is not ListViewItem)
        {
            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        if (dependencyObject is not ListViewItem { DataContext: MarketActivityRow row })
        {
            return;
        }

        ActivityDetailsPopup.DataContext = row;
        ActivityDetailsPopup.IsOpen = true;
    }
}
