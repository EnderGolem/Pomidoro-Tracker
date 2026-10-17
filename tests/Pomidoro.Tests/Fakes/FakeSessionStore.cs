using Pomidoro.Domain;
using Pomidoro.Storage;

namespace Pomidoro.Tests.Fakes;

// In-memory store: it's the reason ISessionStore was extracted into an interface.
internal sealed class FakeSessionStore : ISessionStore
{
    private readonly List<WorkSession> _sessions = [];
    private readonly List<string> _catalog = [];
    private readonly Dictionary<string, int> _settings = [];

    private long _nextId = 1;

    public List<string> UpdatedTypes { get; } = [];

    public List<long> DeletedIds { get; } = [];

    public void Add(WorkSession session)
    {
        _sessions.Add(session with { Id = _nextId });
        _nextId++;
    }

    public void AddRange(params WorkSession[] sessions)
    {
        foreach (WorkSession session in sessions)
        {
            Add(session);
        }
    }

    public IReadOnlyList<WorkSession> GetRange(DayRange range, DayBoundary boundary) => _sessions
        .Where(session => range.Contains(boundary.DayOf(session.EndLocal)))
        .OrderByDescending(session => session.EndUtc)
        .ToList();

    public void UpdateType(long id, string type)
    {
        UpdatedTypes.Add(type);
        Replace(id, session => session with { Type = type });
    }

    public void UpdateDuration(long id, int durationSec) =>
        Replace(id, session => session with { DurationSec = durationSec });

    public void Delete(long id)
    {
        DeletedIds.Add(id);
        _sessions.RemoveAll(session => session.Id == id);
    }

    public IReadOnlyList<string> GetTypeCatalog() => [.. _catalog];

    public void AddTypeToCatalog(string name)
    {
        if (!WorkTypes.IsReservedName(name) && !_catalog.Contains(name, WorkTypes.Comparer))
        {
            _catalog.Add(name);
        }
    }

    public void RemoveTypeFromCatalog(string name) =>
        _catalog.RemoveAll(stored => WorkTypes.Comparer.Equals(stored, name));

    public int GetSetting(string key, int fallback) =>
        _settings.TryGetValue(key, out int value) ? value : fallback;

    public void SetSetting(string key, int value) => _settings[key] = value;

    private void Replace(long id, Func<WorkSession, WorkSession> change)
    {
        int index = _sessions.FindIndex(session => session.Id == id);

        if (index >= 0)
        {
            _sessions[index] = change(_sessions[index]);
        }
    }
}
