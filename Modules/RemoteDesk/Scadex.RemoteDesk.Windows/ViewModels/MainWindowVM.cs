using Scadex.RemoteDesk.Windows.Helpers;

namespace Scadex.RemoteDesk.Windows.ViewModels;

public class MainWindowVM : BaseViewModel
{
    private BaseViewModel _currentView;
    public BaseViewModel CurrentView
    {
        get => _currentView;
        private set
        {
            if (_currentView == value) return;
            _currentView = value;
            OnPropertyChanged();
        }
    }

    public MainWindowVM(HomeVM homeVM)
    {
        _currentView = homeVM;
        CurrentView = homeVM;
    }
}