using Scadex.RemoteDesk.Windows.Helpers;
using System.Windows.Input;

namespace Scadex.RemoteDesk.Windows.ViewModels;

public class HomeVM : BaseViewModel
{
    private string _title = "Welcome to Scadex Remote Desktop";
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }
    public ICommand TitleChangeCommand { get; private set; }

    public HomeVM()
    {
        TitleChangeCommand = new RelayCommand(o =>
        {
            Title = "Home Title Changed!";
        });
    }
}