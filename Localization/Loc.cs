namespace Pomidoro.Localization;

// The language is chosen at build time, not at runtime: the Russian and English versions
// ship as separate files, and there is no switch in the UI.
// The English build is enabled by the ENGLISH constant; see Pomidoro.csproj.
internal static class Loc
{
    public static readonly IReadOnlyList<Strings> All = [new RussianStrings(), new EnglishStrings()];

#if ENGLISH
    public static Strings S { get; } = new EnglishStrings();
#else
    public static Strings S { get; } = new RussianStrings();
#endif
}
