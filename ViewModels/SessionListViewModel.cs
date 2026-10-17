using System.Collections.ObjectModel;
using Pomidoro.Domain;
using Pomidoro.Mvvm;
using Pomidoro.Storage;

namespace Pomidoro.ViewModels;

// Один и тот же список используют оба экрана: «сегодня» показывает его без колонки
// даты, статистика — с ней.
internal sealed class SessionListViewModel : ObservableObject
{
    private readonly ISessionStore _store;
    private readonly bool _showDate;

    public SessionListViewModel(ISessionStore store, bool showDate, string emptyText)
    {
        _store = store;
        _showDate = showDate;
        EmptyText = emptyText;
        ShowsDate = showDate;
    }

    public event EventHandler<SessionRowViewModel>? RowChanged;

    public event EventHandler<SessionRowViewModel>? RowDeleted;

    public ObservableCollection<SessionRowViewModel> Rows { get; } = [];

    public string EmptyText { get; }

    public bool ShowsDate { get; }

    public bool IsEmpty => Rows.Count == 0;

    public void Load(IReadOnlyList<WorkSession> sessions, IReadOnlyList<string> typeCatalog)
    {
        foreach (SessionRowViewModel row in Rows)
        {
            row.Changed -= OnRowChanged;
            row.DeleteRequested -= OnRowDeleteRequested;
        }

        Rows.Clear();

        foreach (WorkSession session in sessions)
        {
            SessionRowViewModel row = new(session, typeCatalog, _showDate, _store);
            row.Changed += OnRowChanged;
            row.DeleteRequested += OnRowDeleteRequested;
            Rows.Add(row);
        }

        Raise(nameof(IsEmpty));
    }

    private void OnRowChanged(object? sender, EventArgs e)
    {
        if (sender is SessionRowViewModel row)
        {
            RowChanged?.Invoke(this, row);
        }
    }

    private void OnRowDeleteRequested(object? sender, EventArgs e)
    {
        if (sender is SessionRowViewModel row)
        {
            RowDeleted?.Invoke(this, row);
        }
    }
}
