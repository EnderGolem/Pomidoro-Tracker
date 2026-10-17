# Recipes

Step-by-step walkthroughs of typical changes. Each recipe shows which files to touch
and in what order — and also serves as a check: if a change requires edits
in unexpected places, something is wrong with the layers.

The general rule: **start from the bottom**. First the domain and a test for it, then the store,
then the view model, and the markup last.

---

## Add a setting

Say you need a checkbox "don't start rest automatically".

**1. `Storage/AppSettings.cs`** — key, field, property:

```csharp
private const string AutoRestKey = "AutoRest";

private bool _autoRest;

public bool AutoRest
{
    get => _autoRest;
    set => Save(AutoRestKey, ref _autoRest, value);
}
```

`Save` stores an `int`, so a boolean value needs either an overload
or storing `0`/`1`. The latter is simpler and does not change the schema.

In the constructor — reading: `_autoRest = store.GetSetting(AutoRestKey, 1) != 0;`

**2. Domain** — if the setting changes the timer's behavior, it comes in as a parameter
to `Start` rather than being read inside. The domain does not know about `AppSettings`.

**3. `ViewModels/SettingsViewModel.cs`** — a property to bind to:

```csharp
public bool AutoRest
{
    get => _settings.AutoRest;
    set { _settings.AutoRest = value; Raise(); }
}
```

Saving happens by itself, and `AppSettings.Changed` makes the open screen
reload.

**4. `Views/SettingsView.xaml`** — the control:

```xml
<CheckBox Content="Отдых начинать вручную"
          IsChecked="{Binding AutoRest}"
          Margin="0,10,0,0" />
```

There is no style for `CheckBox` in the theme yet — the built-in one will look foreign in a dark window.
Either add a style to `Themes/Inputs.xaml`, or do without the checkbox.

**5. Test** — in `SqliteSessionStoreTests` it is enough to check that the value
survives reopening the database.

---

## Add a statistics period

For example, "last 30 days".

**1. `Domain/StatsPeriod.cs`** — a new value. Add it **at the end**: the order
of values sets the order of items in the list.

```csharp
internal enum StatsPeriod
{
    CurrentWeek,
    PreviousWeek,
    CurrentMonth,
    PreviousMonth,
    LastThirtyDays
}
```

**2. `Domain/StatsPeriods.cs`** — two branches in the `switch`:

```csharp
StatsPeriod.LastThirtyDays => new DayRange(today.AddDays(-29), today),
...
StatsPeriod.LastThirtyDays => "Последние 30 дней",
```

**3. Test** in `StatsPeriodsTests`:

```csharp
[Fact]
public void Last_thirty_days_ends_today()
{
    DayRange range = StatsPeriods.Resolve(StatsPeriod.LastThirtyDays, Friday);

    Assert.Equal(30, range.Days);
    Assert.Equal(Friday, range.To);
}
```

**That's it.** Neither the view model nor the markup needs to be touched: the period list
is built from `StatsPeriods.All`, and the chart picks the bar width
for the new number of days by itself.

This is the layer check in action: a change that looks like "a UI thing"
fits entirely into the domain.

---

## Add a column to the entry list

For example, the start time (right now only the end time is shown).

**1. `Domain/WorkSession.cs`** — a computed property, if the data is already there:

```csharp
public DateTime StartLocal => EndLocal - Duration;
```

If the data isn't there, you'll need a database column and a migration (see the next recipe).

**2. `ViewModels/SessionRowViewModel.cs`** — a display-ready string:

```csharp
StartTime = Format.TimeOfDay(session.StartLocal);
...
public string StartTime { get; }
```

**3. `Views/SessionListView.xaml`** — the column goes **in two places**: in the header
and in the row template. This is the only place where you have to write it twice,
but the width is set once — via `SharedSizeGroup`:

```xml
<ColumnDefinition Width="Auto" SharedSizeGroup="StartColumn" />
```

```xml
<TextBlock Grid.Column="1" Text="НАЧАЛ" Style="{StaticResource CaptionText}" Margin="0,0,12,0" />
```

```xml
<TextBlock Grid.Column="1" Text="{Binding StartTime}" Style="{StaticResource ValueText}"
           FontSize="13" VerticalAlignment="Center" Margin="0,0,12,0" />
```

Don't forget to shift `Grid.Column` for all the columns to the right.

**4. Test** in `SessionRowViewModelTests` — that the start time is computed as the end
minus the duration.

---

## Add a field to a history entry

The most expensive recipe: it touches every layer. Say you need a note on a pomodoro.

**1. Schema, `Storage/SqliteSchema.cs`.** A column is added to `CREATE TABLE` —
but that is not enough: for those already using the application the table exists,
and `CREATE TABLE IF NOT EXISTS` won't touch it. A migration is needed, as for the `Type` column:

```csharp
if (!HasColumn(connection, "Note"))
{
    migrate.CommandText = "ALTER TABLE WorkSessions ADD COLUMN Note TEXT NOT NULL DEFAULT '';";
}
```

Right now `HasTypeColumn` is hard-wired to a single column. If there are two migrations,
it is better to introduce `PRAGMA user_version` and a list of steps rather than multiply checks.

**2. `Domain/WorkSession.cs`** — a property:

```csharp
public string Note { get; init; } = string.Empty;
```

**3. `Storage/SqliteSessionStore.cs`** — three places: `INSERT`, `SELECT` in `GetRange`
(don't forget the column indexes in `reader.GetString(n)`) and, if the note can be edited,
a new method like `UpdateNote`, plus a line in `ISessionStore` to go with it.

**4. `Fakes/FakeSessionStore.cs`** — the same operation for the tests.

**5. Row model and markup** — as in the previous recipe.

**6. Tests** — in `SqliteSessionStoreTests`, necessarily: that the field is saved,
read back and survives reopening the database.

---

## Add a label to the screen

Any new text goes through `Strings`; there must be no literals in markup or models.

**1. `Localization/Strings.cs`** — the declaration:

```csharp
public abstract string ExportButton { get; }
```

From this moment the project **does not build** until both languages are translated. That is
the whole point of the abstract class.

**2. `RussianStrings.cs` and `EnglishStrings.cs`** — the values:

```csharp
public override string ExportButton => "Выгрузить";
public override string ExportButton => "Export";
```

**3. Markup** — through its own markup extension:

```xml
<Button Content="{loc:Text ExportButton}" />
```

The file must declare the namespace:
`xmlns:loc="clr-namespace:Pomidoro.Localization"`.

**4. Or code** — through `Loc.S`:

```csharp
StatusText = Loc.S.ExportButton;
```

If the text is assembled into a model property, the model must subscribe to
`Loc.Current.LanguageChanged` and rebuild it — otherwise the label stays in
the previous language until the screen is reloaded.

**Text with a number** is declared as a method, not a property:

```csharp
public abstract string ExportedRows(int count);
```

The number is formatted in the invariant culture via `string.Create(Culture, $"...")`.

No test needs to be written: `StringsTests` checks by itself that the label is not empty in either
language and that the key from the markup exists.

---

## Change the look

**Color** — only in `Themes/Colors.xaml`. If the shade you need is missing, add a token
with a role-based name (`WarningBrush`), not `#FF8800` in the markup.

**Style of a button, field, list** — in the corresponding dictionary in `Themes/`.
A new style is best made via `BasedOn`:

```xml
<Style x:Key="DangerButton" TargetType="Button" BasedOn="{StaticResource FlatButton}">
    <Setter Property="Background" Value="{StaticResource AccentBrush}" />
</Style>
```

**Window size** — in `Views/MainWindow.xaml`. The chart adapts by itself:
its geometry is computed from the actual size of the panel.

If you edit a dictionary that uses colors, check that `Colors.xaml` is merged
at the top of the file — otherwise `StaticResource` won't find the token even though
it exists at the application level.

---

## Turn off warnings-as-errors

If a rule is in the way right now and there is no time to fix it:

```xml
<TreatWarningsAsErrors>false</TreatWarningsAsErrors>
```

A so-so solution. Better to be targeted: in `.editorconfig`, if the rule doesn't fit the project
as a whole, or `[SuppressMessage]` with an explanation, if it is a one-off case:

```csharp
[SuppressMessage("Design", "CA1031:Do not catch general exception types",
    Justification = "Why this is the right thing here")]
```

An empty `Justification` is worse than no suppression at all: in half a year nobody
will remember what was meant.

---

## See what is in the database

Gear → "Developer SQL". Or any SQLite client at the path
shown in the top line of the console window.

Everyday queries are in [03-storage.md](03-storage.md#useful-queries).

If the application is running, keep in mind: there is one connection and it lives in the process. A third-party
client may not see the latest changes until they are committed, and a long transaction
in the client will block writes from the application.
