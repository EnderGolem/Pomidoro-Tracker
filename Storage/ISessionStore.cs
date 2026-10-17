using Pomidoro.Domain;

namespace Pomidoro.Storage;

// Хранилище истории и настроек. Выделено в интерфейс ради двух вещей: тесты подставляют
// сюда список в памяти, а SQLite при необходимости меняется на что-то другое целиком.
// Временем жизни хранилища распоряжается App, поэтому IDisposable здесь не нужен.
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
