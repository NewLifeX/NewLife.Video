using Avalonia.Controls;
using Avalonia.Interactivity;
using NewLife.Video.Studio.ViewModels;

namespace NewLife.Video.Studio.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // 默认选中第一个导航项
        if (DataContext is MainWindowViewModel vm && vm.NavItems.Count > 0)
        {
            vm.SelectedNav = vm.NavItems[0];
        }
    }
}