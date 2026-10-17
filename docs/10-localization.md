# Languages

The interface exists in Russian and English. **The language is chosen at build time, not at
run time:** two separate `.exe` files are released, and there is no switch in the settings.

This is a deliberate decision. A language drop-down in the menu under the gear looked
out of place, and two versions are more honest: the user downloads their own and never sees the other.
The side benefit is that the whole on-the-fly switching machinery left the code: events, the
subscriptions of five models, a setting in the database and the rebuilding of lists.

## Where the strings live

```
Localization/
├─ AppLanguage.cs      enumeration: Russian, English
├─ Strings.cs          abstract list of all UI text
├─ RussianStrings.cs   Russian values
├─ EnglishStrings.cs   English ones
├─ Loc.cs              which set this build has chosen
└─ TextExtension.cs    the {loc:Text Key} markup extension
```

`Strings` is **an abstract class, not a dictionary**. This is the key decision: a forgotten string
is found by the compiler, not by a user who sees an empty label.

```csharp
internal abstract class Strings
{
    public abstract string StartButton { get; }
    public abstract string Pomodoros(int count);
    ...
}
```

Add a property to `Strings`, and both subclasses stop compiling until
you write the translation.

## How the language gets into the build

The whole choice is four lines in `Localization/Loc.cs`:

```csharp
#if ENGLISH
    public static Strings S { get; } = new EnglishStrings();
#else
    public static Strings S { get; } = new RussianStrings();
#endif
```

The `ENGLISH` constant is turned on by the `AppLanguage=en` property, and this is done by a **target** in
`Pomidoro.csproj`, not by a conditional `PropertyGroup`:

```xml
<Target Name="AddLanguageConstant" BeforeTargets="BeforeCompile" Condition="'$(AppLanguage)' == 'en'">
  <PropertyGroup>
    <DefineConstants>$(DefineConstants);ENGLISH</DefineConstants>
  </PropertyGroup>
</Target>
```

Why a target: the property comes from the publish profile, and the profile is imported
**after** the body of the `.csproj` has already been evaluated. A conditional `PropertyGroup`
simply won't see it — this was verified, the English build came out Russian. A target runs
after all the imports and does see the value.

## How a string gets onto the screen

**In markup** — through its own markup extension:

```xml
<Button Content="{loc:Text StartButton}" />
```

`TextExtension` expands it into a one-time binding to `Loc.S`. The string could have been
substituted directly, but a binding is more useful: a typo in the key shows up in the Output window
as a binding error instead of silently turning into an empty label.

**In code** — through `Loc.S`:

```csharp
PhaseText = Loc.S.PhaseIdle;
CountText = Loc.S.Pomodoros(_sessions.Count);
```

No subscriptions or notifications: the language does not change while the application is running.

## Numbers and dates are not translated

`Formatting/Format` is now only about numbers and dates and works in the invariant culture:
`0.5`, `16.10`, `25:00`. The word around the number is added by the language:

```csharp
Loc.S.Hours(Format.HoursNumber(seconds))   // "3.2 часа" or "3.2 hours"
```

Dates are shown in the European form — `16.10.2026` — in both versions.
The application is personal, the format is familiar, and translating dates would have doubled
the formatting tests for no benefit.

## Plurals

Russian has three forms, English has two, and the rule lives where the language does:

```csharp
// RussianStrings
private static string PomodoroWord(int count)
{
    if (count % 100 is >= 11 and <= 14) { return "помидорок"; }

    return (count % 10) switch
    {
        1 => "помидорка",
        2 or 3 or 4 => "помидорки",
        _ => "помидорок"
    };
}
```

This works for two languages and will hit a wall at the fourth: Polish has different forms,
Arabic has six of them. The right answer for that case is ICU MessageFormat, where
plural rules live as data next to the translation, not as code. While there are two languages,
it isn't worth the effort.

## The reserved type name is closed in both languages

`WorkTypes.ReservedNames` lists «Без типа», "No type" and a dash — **both
languages at once, always**, even though each build knows only its own. This is needed so that a database
started in the English version opens in the Russian one without a collision: a type named
"No type" must not merge with the bucket for entries without a type.

The names of work types themselves are **never translated**: they are user data.
«Созвон» ("call") stays «Созвон» in the English version too.

## What the tests check

`StringsTests` — 24 checks:

- no label in either language is empty (via reflection over all properties);
- every period has its own, non-repeating label;
- pomodoro plurals follow the Russian and English rules;
- each language's "no type" label is among the forbidden names;
- **every `{loc:Text X}` key from the markup exists in `Strings`.**

The last one is the most important. A key in markup remains a string, and the compiler does not
catch a typo in it; the test reads all `Views/*.xaml` files and checks the keys against the list
of properties. It also checks that more than forty keys were found — otherwise a test
that failed to find the markup would silently "pass" forever.

The tests **don't know which language the build has chosen**: where on-screen text is checked,
the expectation is taken from `Loc.S`, not written as a literal. That is why the same suite
passes for both the Russian and the English build.

## Adding a language

1. A new value in `AppLanguage`.
2. A new `Strings` subclass — the compiler will list everything that needs translating.
3. An `#elif` branch in `Loc.cs` and a condition in the `AddLanguageConstant` target.
4. A publish profile with its own `AppLanguage` and `PublishDir`.
