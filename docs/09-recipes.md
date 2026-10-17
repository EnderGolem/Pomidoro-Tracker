# Рецепты

Пошаговые разборы типичных изменений. Каждый рецепт показывает, какие файлы трогать
и в каком порядке, — а заодно служит проверкой: если изменение требует правок
в неожиданных местах, значит, что-то не так со слоями.

Общее правило: **начинай снизу**. Сначала домен и тест к нему, потом хранилище,
потом модель представления, в последнюю очередь разметка.

---

## Добавить настройку

Допустим, нужна галочка «не начинать отдых автоматически».

**1. `Storage/AppSettings.cs`** — ключ, поле, свойство:

```csharp
private const string AutoRestKey = "AutoRest";

private bool _autoRest;

public bool AutoRest
{
    get => _autoRest;
    set => Save(AutoRestKey, ref _autoRest, value);
}
```

`Save` хранит `int`, поэтому для булева значения понадобится либо перегрузка,
либо хранение `0`/`1`. Второе проще и не меняет схему.

В конструкторе — чтение: `_autoRest = store.GetSetting(AutoRestKey, 1) != 0;`

**2. Домен** — если настройка меняет поведение таймера, она приходит параметром
в `Start`, а не читается внутри. Домен не знает про `AppSettings`.

**3. `ViewModels/SettingsViewModel.cs`** — свойство для привязки:

```csharp
public bool AutoRest
{
    get => _settings.AutoRest;
    set { _settings.AutoRest = value; Raise(); }
}
```

Сохранение произойдёт само, а `AppSettings.Changed` заставит открытый экран
перечитаться.

**4. `Views/SettingsView.xaml`** — элемент управления:

```xml
<CheckBox Content="Отдых начинать вручную"
          IsChecked="{Binding AutoRest}"
          Margin="0,10,0,0" />
```

Стиля для `CheckBox` в теме пока нет — встроенный в тёмном окне будет выглядеть чужим.
Либо добавить стиль в `Themes/Inputs.xaml`, либо обойтись без флажка.

**5. Тест** — в `SqliteSessionStoreTests` достаточно проверить, что значение
переживает переоткрытие базы.

---

## Добавить период в статистику

Например, «последние 30 дней».

**1. `Domain/StatsPeriod.cs`** — новое значение. Добавлять **в конец**: порядок
значений задаёт порядок пунктов в списке.

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

**2. `Domain/StatsPeriods.cs`** — две ветки в `switch`:

```csharp
StatsPeriod.LastThirtyDays => new DayRange(today.AddDays(-29), today),
...
StatsPeriod.LastThirtyDays => "Последние 30 дней",
```

**3. Тест** в `StatsPeriodsTests`:

```csharp
[Fact]
public void Last_thirty_days_ends_today()
{
    DayRange range = StatsPeriods.Resolve(StatsPeriod.LastThirtyDays, Friday);

    Assert.Equal(30, range.Days);
    Assert.Equal(Friday, range.To);
}
```

**Всё.** Ни модель представления, ни разметку трогать не нужно: список периодов
строится из `StatsPeriods.All`, а диаграмма сама подберёт ширину столбиков
под новое количество дней.

Это и есть проверка слоёв: изменение, которое выглядит как «про интерфейс»,
полностью умещается в домене.

---

## Добавить колонку в список записей

Например, время начала (сейчас показывается только окончание).

**1. `Domain/WorkSession.cs`** — вычисляемое свойство, если данные уже есть:

```csharp
public DateTime StartLocal => EndLocal - Duration;
```

Если данных нет — понадобится колонка в базе и миграция (см. следующий рецепт).

**2. `ViewModels/SessionRowViewModel.cs`** — готовая к показу строка:

```csharp
StartTime = Format.TimeOfDay(session.StartLocal);
...
public string StartTime { get; }
```

**3. `Views/SessionListView.xaml`** — колонка **в двух местах**: в заголовке
и в шаблоне строки. Это единственное место, где приходится писать дважды,
зато ширина задаётся один раз — через `SharedSizeGroup`:

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

Не забудь сдвинуть `Grid.Column` у всех колонок правее.

**4. Тест** в `SessionRowViewModelTests` — что время начала считается от окончания
минус длительность.

---

## Добавить поле в запись истории

Самый дорогой рецепт: он затрагивает все слои. Допустим, нужна заметка к помидорке.

**1. Схема, `Storage/SqliteSchema.cs`.** В `CREATE TABLE` добавляется колонка —
но этого мало: у тех, кто уже пользуется приложением, таблица существует,
и `CREATE TABLE IF NOT EXISTS` её не тронет. Нужна миграция, как у колонки `Type`:

```csharp
if (!HasColumn(connection, "Note"))
{
    migrate.CommandText = "ALTER TABLE WorkSessions ADD COLUMN Note TEXT NOT NULL DEFAULT '';";
}
```

Сейчас `HasTypeColumn` зашит под одну колонку. Если миграций станет две,
правильнее завести `PRAGMA user_version` и список шагов, а не множить проверки.

**2. `Domain/WorkSession.cs`** — свойство:

```csharp
public string Note { get; init; } = string.Empty;
```

**3. `Storage/SqliteSessionStore.cs`** — три места: `INSERT`, `SELECT` в `GetRange`
(не забыть индексы колонок в `reader.GetString(n)`) и, если заметку можно править,
новый метод вроде `UpdateNote`, а заодно строка в `ISessionStore`.

**4. `Fakes/FakeSessionStore.cs`** — та же операция для тестов.

**5. Модель строки и разметка** — как в предыдущем рецепте.

**6. Тесты** — в `SqliteSessionStoreTests` обязательно: что поле сохраняется,
читается и переживает переоткрытие базы.

---

## Добавить надпись на экран

Любой новый текст идёт через `Strings`, литералов в разметке и моделях быть не должно.

**1. `Localization/Strings.cs`** — объявление:

```csharp
public abstract string ExportButton { get; }
```

С этой секунды проект **не собирается**, пока не переведены оба языка. Это и есть
смысл абстрактного класса.

**2. `RussianStrings.cs` и `EnglishStrings.cs`** — значения:

```csharp
public override string ExportButton => "Выгрузить";
public override string ExportButton => "Export";
```

**3. Разметка** — через свою запись:

```xml
<Button Content="{loc:Text ExportButton}" />
```

В файле должно быть объявлено пространство имён:
`xmlns:loc="clr-namespace:Pomidoro.Localization"`.

**4. Или код** — через `Loc.S`:

```csharp
StatusText = Loc.S.ExportButton;
```

Если текст складывается в свойство модели, модель обязана подписаться на
`Loc.Current.LanguageChanged` и пересобрать его — иначе надпись останется на
прежнем языке до перезагрузки экрана.

**Текст с числом** объявляется методом, а не свойством:

```csharp
public abstract string ExportedRows(int count);
```

Число форматируется в инвариантной культуре через `string.Create(Culture, $"...")`.

Тест писать не нужно: `StringsTests` сам проверит, что подпись не пустая ни в одном
языке и что ключ из разметки существует.

---

## Изменить внешний вид

**Цвет** — только в `Themes/Colors.xaml`. Если нужного оттенка нет, добавь токен
с ролевым именем (`WarningBrush`), а не `#FF8800` в разметке.

**Стиль кнопки, поля, списка** — в соответствующем словаре `Themes/`.
Новый стиль лучше делать через `BasedOn`:

```xml
<Style x:Key="DangerButton" TargetType="Button" BasedOn="{StaticResource FlatButton}">
    <Setter Property="Background" Value="{StaticResource AccentBrush}" />
</Style>
```

**Размер окна** — в `Views/MainWindow.xaml`. Диаграмма подстроится сама:
её геометрия считается от фактического размера панели.

Если правишь словарь, который пользуется цветами, проверь, что в начале файла
подключён `Colors.xaml` — иначе `StaticResource` не найдёт токен даже при том,
что он есть на уровне приложения.

---

## Отключить предупреждения-как-ошибки

Если правило мешает прямо сейчас и чинить его некогда:

```xml
<TreatWarningsAsErrors>false</TreatWarningsAsErrors>
```

Так себе решение. Лучше точечно: в `.editorconfig`, если правило не подходит проекту
целиком, или `[SuppressMessage]` с объяснением, если случай единичный:

```csharp
[SuppressMessage("Design", "CA1031:Do not catch general exception types",
    Justification = "Почему здесь это правильно")]
```

Пустое `Justification` хуже, чем отсутствие подавления: через полгода никто
не вспомнит, что имелось в виду.

---

## Посмотреть, что в базе

Шестерёнка → «SQL разработчика». Или любым клиентом SQLite по пути,
который показан в верхней строке окна консоли.

Запросы на каждый день — в [корневом README](../README.md#полезные-запросы).

Если приложение запущено, помни: соединение одно и живёт в процессе. Сторонний
клиент может не увидеть последних изменений до их фиксации, а длинная транзакция
в клиенте заблокирует запись из приложения.
