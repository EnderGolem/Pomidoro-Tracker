# Сборка и выпуск

## Что нужно

- .NET SDK 9
- Windows (проект целится в `net9.0-windows` и использует WPF)

Visual Studio не обязательна: всё собирается и запускается из консоли.

## Конфигурации

| Конфигурация | Для чего |
|---|---|
| `Debug` | обычная отладочная |
| `DebugLocal` | отладочная с символом `DEBUG_LOCAL`, используется по умолчанию |
| `Release` | выпуск, оптимизация включена |

```
dotnet build Pomidoro.sln -c DebugLocal
bin\DebugLocal\net9.0-windows\Pomidoro.exe
```

`DebugLocal` отличается от `Debug` только дополнительным символом компиляции
и живёт здесь по привычке автора — ни одного `#if DEBUG_LOCAL` в коде сейчас нет.

## Что включено в `Pomidoro.csproj`

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisLevel>latest-recommended</AnalysisLevel>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

- **Nullable включён.** Ссылка, которая может быть `null`, помечена `?`, и компилятор
  следит за проверками.
- **Анализаторы на уровне `latest-recommended`.** Это заметно больше правил, чем
  по умолчанию: культура форматирования, освобождение ресурсов, подозрительный SQL,
  безопасность P/Invoke.
- **Предупреждения — ошибки.** «Собралось» означает «собралось начисто». Если мешает,
  строка убирается, но лучше чинить причину.

Отключено ровно три правила, каждое в `.editorconfig` с объяснением:

| Правило | Почему выключено |
|---|---|
| `CA1303` | строки интерфейса жёстко русские, ресурсов для перевода нет и не планируется |
| `CA1062` | проверки аргументов на `null` не нужны: все типы `internal`, вызовов извне сборки нет |
| `CA1707` | только для `tests/**`: имена тестов — фразы через подчёркивания |

Там, где правило нарушено осознанно и однократно, стоит `[SuppressMessage]`
с объяснением прямо в коде. Таких мест четыре: два `catch (Exception)` в консоли
и при открытии базы, один в проигрывателе звука и один на `App` (владеет
освобождаемыми полями, но освобождает их в `OnExit`).

### Подключение WinForms

```xml
<UseWPF>true</UseWPF>
<UseWindowsForms>true</UseWindowsForms>

<ItemGroup>
    <Using Remove="System.Windows.Forms" />
    <Using Remove="System.Drawing" />
</ItemGroup>
```

WinForms нужен только ради `NotifyIcon`: своего значка в трее у WPF нет.
Неявные `using` обязательно снимаются — иначе `Button`, `TextBox` и `MessageBox`
из WinForms начинают конфликтовать с одноимёнными типами WPF по всему проекту.

Там, где типы обеих библиотек встречаются в одном файле (`Tray/TomatoIcon.cs`),
используется псевдоним:

```csharp
using ImageSource = System.Windows.Media.ImageSource;
```

### Тесты внутри папки проекта

```xml
<DefaultItemExcludes>$(DefaultItemExcludes);tests\**</DefaultItemExcludes>
<InternalsVisibleTo Include="Pomidoro.Tests" />
```

Проект тестов лежит в `tests/Pomidoro.Tests/`, то есть внутри папки основного проекта.
SDK-проекты по умолчанию забирают **все** `.cs` рекурсивно, поэтому папка явно исключена.
`InternalsVisibleTo` открывает тестам внутренние типы — без него пришлось бы делать
всё публичным ради проверок.

### Пакеты

| Пакет | Версия | Зачем |
|---|---|---|
| `Microsoft.Data.Sqlite` | 9.0.11 | работа с базой |
| `SQLitePCLRaw.bundle_e_sqlite3` | 3.0.5 | нативная библиотека SQLite в комплекте |

Версии закреплены точно. Второй пакет нужен, чтобы не зависеть от SQLite, установленного
в системе, — и именно из-за него у публикации одним файлом включён
`IncludeNativeLibrariesForSelfExtract`.

## Тесты

```
dotnet test tests\Pomidoro.Tests\Pomidoro.Tests.csproj -c DebugLocal
```

Проект тестов тоже с анализаторами, но без `TreatWarningsAsErrors`. Подробнее —
в [06-testing.md](06-testing.md).

## Публикация

Русская и английская версии — **два разных файла**: язык зашивается при сборке,
переключателя в интерфейсе нет. Выпуск обеих:

```
dotnet publish -p:PublishProfile=SingleFile-Ru
dotnet publish -p:PublishProfile=SingleFile-En
```

| Профиль | Куда | Язык | Размер | Нужен .NET |
|---|---|---|---|---|
| `SingleFile-Ru` | `publish\ru\` | русский | ~72 МБ | нет |
| `SingleFile-En` | `publish\en\` | английский | ~72 МБ | нет |
| `SingleFile-Framework` | `publish\framework\` | русский | ~2.4 МБ | да, .NET 9 Desktop Runtime |
| `FolderProfile` | `bin\Release\net9.0-windows\publish\win-x64\` | русский | папка с файлами | нет |

Результат первых трёх — ровно один `Pomidoro.exe`. Четвёртый раскладывает
самодостаточную сборку обычной папкой и остался от шаблона Visual Studio.

Английскую версию включает свойство `AppLanguage=en`; оно работает с любым профилем
и с обычной сборкой:

```
dotnet build Pomidoro.sln -c DebugLocal -p:AppLanguage=en
```

**Обе версии кладут промежуточные файлы в один и тот же `bin\Release`.** MSBuild
пересобирает их, потому что меняются аргументы компилятора, но если выпуск важен —
снеси `bin\Release` перед ним и проверь заголовок окна у готового `.exe`. Это самая
дешёвая проверка, что язык тот.

Ключевые настройки самодостаточного профиля:

```xml
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
<PublishReadyToRun>false</PublishReadyToRun>
<DebugType>none</DebugType>
```

`IncludeNativeLibrariesForSelfExtract` нужен из-за SQLite: без него нативная
`e_sqlite3.dll` осталась бы лежать рядом с `.exe` отдельным файлом. База при этом
всё равно создаётся рядом с `.exe`, а не во временной папке распаковки, — за это
отвечает `AppContext.BaseDirectory`.

Сжать сильнее не выйдет: `PublishTrimmed` с WPF не поддерживается, а сжатие в профиле
уже включено (без него вышло бы около 160 МБ).

## Непрерывная сборка

`.github/workflows/build.yml` — на каждый push и pull request в `main`:

```yaml
runs-on: windows-latest
steps:
  - dotnet restore Pomidoro.sln
  - dotnet build Pomidoro.sln -c Release --no-restore
  - dotnet test tests/Pomidoro.Tests/Pomidoro.Tests.csproj -c Release --no-build
```

`windows-latest` обязателен: WPF на Linux не собирается.

## Кодировки и переводы строк

- Исходники: **UTF-8 без BOM**, переводы строк **CRLF**.
- Правила закреплены в `.editorconfig` и `.gitattributes`.

Смешанные кодировки в проекте с кириллицей — источник неприятностей: часть файлов
читается как ANSI, и комментарии превращаются в кракозябры. Если правишь файлы
чем-то кроме Visual Studio, следи за этим.

Отдельная ловушка, не связанная с проектом, но встречающаяся рядом: `.ps1`-скрипты
в UTF-8 **без** BOM Windows PowerShell 5.1 читает как ANSI и ломается на кириллице.
Для скриптов BOM нужен.

## Что игнорируется гитом

```
bin/  obj/  publish/  TestResults/
.vs/  *.user  *.suo
pomodoro.db  pomodoro.db-shm  pomodoro.db-wal
```

База исключена намеренно: это личные данные, им не место в репозитории.
