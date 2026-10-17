using Pomidoro.Domain;

namespace Pomidoro.Storage;

// History and settings store. Extracted into an interface for two reasons: tests substitute
// an in-memory list here, and SQLite can be swapped for something else entirely if needed.
// App owns the store's lifetime, so IDisposable isn't needed here.
internal interface ISessionStore
{
    void Add(WorkSession session);

    IReadOnlyList<WorkSession> GetRange(DayRange range, DayBoundary boundary);

    void UpdateType(long id, string type);

    void UpdateDuration(long id, int durationSec);

    void Delete(long id);

    IReadOnlyList<string> GetTypeCatalog();

    void AddTypeToCatalog(string name);

    void RemoveTypeFromCatalog(string name);

    int GetSetting(string key, int fallback);

    void SetSetting(string key, int value);
}
