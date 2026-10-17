namespace Pomidoro.Storage;

// Консоль разработчика вынесена из ISessionStore: произвольный SQL — это свойство
// конкретной реализации на SQLite, а не часть контракта «где лежит история».
internal interface ISqlConsole
{
    string DatabasePath { get; }

    SqlQueryResult Execute(string sql);
}
