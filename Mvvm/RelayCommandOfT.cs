using System.Windows.Input;

namespace Pomidoro.Mvvm;

internal sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;

    public RelayCommand(Action<T> execute)
    {
        _execute = execute;
    }

    // Доступность такой команды зависит только от того, пришёл ли параметр нужного типа,
    // поэтому пересчитывать её некогда и подписка никому не нужна.
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
