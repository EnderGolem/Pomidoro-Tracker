using System.Windows.Input;
using Pomidoro.Formatting;
using Pomidoro.Localization;
using Pomidoro.Mvvm;
using Pomidoro.Storage;

namespace Pomidoro.ViewModels;

internal sealed class SqlConsoleViewModel : ObservableObject
{
    private const string SampleQuery = "SELECT * FROM WorkSessions ORDER BY Id DESC LIMIT 20;";

    private readonly ISqlConsole _console;

    private string _query = SampleQuery;
    private string _output = string.Empty;
    private string _status = string.Empty;

    public SqlConsoleViewModel(ISqlConsole console)
    {
        _console = console;
        ClearCommand = new RelayCommand(Clear);
    }

    // Запрос мог изменить что угодно, вплоть до удаления таблиц, поэтому главное
    // окно после каждого выполнения перечитывает базу заново.
    public event EventHandler? Executed;

    public ICommand ClearCommand { get; }

    public string DatabasePath => _console.DatabasePath;

    public string Query
    {
        get => _query;
        set => Set(ref _query, value);
    }

    public string Output
    {
        get => _output;
        private set => Set(ref _output, value);
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    // Выделение приходит из окна: TextBox.SelectedText не является свойством зависимости
    // и привязать его к модели нельзя.
    public void Run(string? selectedText)
    {
        string sql = string.IsNullOrWhiteSpace(selectedText) ? Query : selectedText;

        if (string.IsNullOrWhiteSpace(sql))
        {
            return;
        }

        SqlQueryResult result = _console.Execute(sql);

        Output = SqlResultText.Render(result);
        Status = SqlResultText.Status(result);

        Executed?.Invoke(this, EventArgs.Empty);
    }

    public void ReportRefreshFailure(Exception exception)
    {
        Status = Loc.S.SqlRefreshFailedStatus;
        Output = Output +
            Environment.NewLine + Environment.NewLine +
            Loc.S.SqlRefreshFailedText + Environment.NewLine +
            exception.GetType().Name + ": " + exception.Message;
    }

    private void Clear()
    {
        Output = string.Empty;
        Status = string.Empty;
    }
}
