using Scadex.RemoteDesk.Windows.ViewModels;
using System.Windows;

namespace Scadex.RemoteDesk.Windows.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowVM viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}