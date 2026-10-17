namespace Pomidoro.Localization;

// Язык выбирается при сборке, а не во время работы: русская и английская версии
// выпускаются отдельными файлами, переключателя в интерфейсе нет.
// Английскую сборку включает константа ENGLISH — см. Pomidoro.csproj.
internal static class Loc
{
    public static readonly IReadOnlyList<Strings> All = [new RussianStrings(), new EnglishStrings()];

#if ENGLISH
    public static Strings S { get; } = new EnglishStrings();
#else
    public static Strings S { get; } = new RussianStrings();
#endif
}
