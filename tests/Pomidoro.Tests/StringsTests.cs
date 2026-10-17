using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Pomidoro.Domain;
using Pomidoro.Localization;
using Xunit;

namespace Pomidoro.Tests;

// The language in test parameters is a flag, not an enum: AppLanguage is internal,
// while test methods have to be public.
//
// Tests don't switch the global language: Loc is one per process, and xunit runs classes
// in parallel. So the string sets themselves are checked, not swapping them on the fly.
public class StringsTests
{
    private static readonly Strings Russian = new RussianStrings();
    private static readonly Strings English = new EnglishStrings();

    // The build decides which language is selected, so what's checked is not the language
    // but that the selected set really is one of the two declared.
    [Fact]
    public void The_build_picks_one_of_the_two_languages()
    {
        Assert.Equal(2, Loc.All.Count);
        Assert.Equal(AppLanguage.Russian, Loc.All[0].Language);
        Assert.Equal(AppLanguage.English, Loc.All[1].Language);
        Assert.Contains(Loc.All, strings => strings.Language == Loc.S.Language);
    }

    // An empty label on screen isn't a build error but a hole the user
    // sees. That's why a test hunts for it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void No_caption_is_left_empty(bool english)
    {
        Strings strings = Of(english);

        foreach (PropertyInfo property in typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType != typeof(string))
            {
                continue;
            }

            string? value = (string?)property.GetValue(strings);

            Assert.False(string.IsNullOrWhiteSpace(value), property.Name + " пуста");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_period_has_its_own_title(bool english)
    {
        Strings strings = Of(english);

        List<string> titles = [.. StatsPeriods.All.Select(strings.PeriodTitle)];

        Assert.All(titles, title => Assert.False(string.IsNullOrWhiteSpace(title)));
        Assert.Equal(titles.Count, titles.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(0, "ни одной помидорки")]
    [InlineData(1, "1 помидорка")]
    [InlineData(3, "3 помидорки")]
    [InlineData(5, "5 помидорок")]
    [InlineData(11, "11 помидорок")]
    [InlineData(21, "21 помидорка")]
    [InlineData(114, "114 помидорок")]
    public void Pomodoros_follow_russian_rules(int count, string expected)
    {
        Assert.Equal(expected, Russian.Pomodoros(count));
    }

    [Theory]
    [InlineData(0, "no pomodoros")]
    [InlineData(1, "1 pomodoro")]
    [InlineData(2, "2 pomodoros")]
    [InlineData(21, "21 pomodoros")]
    public void Pomodoros_follow_english_rules(int count, string expected)
    {
        Assert.Equal(expected, English.Pomodoros(count));
    }

    [Fact]
    public void Hours_keep_the_number_untouched()
    {
        Assert.Equal("18.6 часа", Russian.Hours("18.6"));
        Assert.Equal("18.6 hours", English.Hours("18.6"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Day_boundary_hint_names_the_hour(bool english)
    {
        Strings strings = Of(english);

        Assert.NotEqual(strings.DayBoundaryHint(0), strings.DayBoundaryHint(4));
        Assert.Contains("04:00", strings.DayBoundaryHint(4), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Empty_key_is_shown_as_no_type(bool english)
    {
        Strings strings = Of(english);

        Assert.Equal(strings.NoType, strings.TypeDisplay(string.Empty));
        Assert.Equal("Работа", strings.TypeDisplay("Работа"));
    }

    // The "no type" label of either language must be closed to manual entry,
    // otherwise after a language switch a real type would collide with the bucket.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void No_type_label_is_a_reserved_name(bool english)
    {
        Assert.True(WorkTypes.IsReservedName(Of(english).NoType));
    }

    // A key in markup is a string, and the compiler won't catch a typo in it.
    // So a test hunts for it: every {loc:Text X} must have a property X.
    [Fact]
    public void Every_key_used_in_markup_exists()
    {
        List<string> missing = [];
        int found = 0;

        foreach (string file in Directory.GetFiles(Path.Combine(ProjectRoot(), "Views"), "*.xaml"))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\{loc:Text (\w+)\}"))
            {
                string key = match.Groups[1].Value;
                found++;

                if (typeof(Strings).GetProperty(key) is null)
                {
                    missing.Add(Path.GetFileName(file) + ": " + key);
                }
            }
        }

        Assert.Empty(missing);
        Assert.True(found > 40, "ключей найдено " + found + " — разметку, похоже, не нашли");
    }

    private static Strings Of(bool english) => english ? English : Russian;

    private static string ProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pomidoro.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Не нашли папку проекта от " + AppContext.BaseDirectory);
    }
}
