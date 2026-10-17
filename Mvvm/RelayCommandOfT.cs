using System.Windows.Input;

namespace Pomidoro.Mvvm;

internal sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;

    public RelayCommand(Action<T> execute)
    {
        _execute = execute;
    }

    // Whether such a command can execute depends only on whether the parameter has the right type,
    // so there is never a moment to re-evaluate it and nobody needs the subscription.
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => parameter is T;

    public void Execute(object? parameter)
    {
        if (parameter is T value)
        {
            _execute(value);
        }
    }
}
