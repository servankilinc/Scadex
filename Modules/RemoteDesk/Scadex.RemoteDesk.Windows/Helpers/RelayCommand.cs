using System.Windows.Input;

namespace Scadex.RemoteDesk.Windows.Helpers;

public class RelayCommand : ICommand
{
    public event EventHandler? CanExecuteChanged;
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public void Execute(object? parameter) => _execute(parameter);
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
