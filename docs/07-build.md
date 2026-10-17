# Build and release

## Requirements

- .NET SDK 9
- Windows (the project targets `net9.0-windows` and uses WPF)

Visual Studio is optional: everything builds and runs from the console.

## Configurations

| Configuration | Purpose |
|---|---|
| `Debug` | regular debug build |
| `Release` | release, optimization enabled |

```
dotnet build Pomidoro.sln -c Debug
bin\Debug\net9.0-windows\Pomidoro.exe
```

## What is enabled in `Pomidoro.csproj`

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisLevel>latest-recommended</AnalysisLevel>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

- **Nullable is on.** A reference that may be `null` is marked with `?`, and the compiler
  keeps track of the checks.
- **Analyzers at the `latest-recommended` level.** That is noticeably more rules than
  the default: formatting culture, disposing of resources, suspicious SQL,
  P/Invoke safety.
- **Warnings are errors.** "It built" means "it built cleanly". If it gets in the way,
  the line can be removed, but it is better to fix the cause.

Exactly three rules are turned off, each in `.editorconfig` with an explanation:

| Rule | Why it is off |
|---|---|
| `CA1303` | the UI strings are hard-coded Russian, there are no resources for translation and none are planned |
| `CA1062` | `null` checks on arguments are not needed: all types are `internal`, there are no calls from outside the assembly |
| `CA1707` | only for `tests/**`: test names are phrases joined with underscores |

Where a rule is broken deliberately and in a single place, there is a `[SuppressMessage]`
with an explanation right in the code. There are four such places: two `catch (Exception)` in the console
and when opening the database, one in the sound player and one on `App` (it owns
disposable fields but disposes of them in `OnExit`).

### Bringing in WinForms

```xml
<UseWPF>true</UseWPF>
<UseWindowsForms>true</UseWindowsForms>

<ItemGroup>
    <Using Remove="System.Windows.Forms" />
    <Using Remove="System.Drawing" />
</ItemGroup>
```

WinForms is needed only for `NotifyIcon`: WPF has no tray icon of its own.
The implicit `using`s must be removed — otherwise `Button`, `TextBox` and `MessageBox`
from WinForms start clashing with the WPF types of the same name all over the project.

Where types from both libraries meet in one file (`Tray/TomatoIcon.cs`),
an alias is used:

```csharp
using ImageSource = System.Windows.Media.ImageSource;
```

### Tests inside the project folder

```xml
<DefaultItemExcludes>$(DefaultItemExcludes);tests\**</DefaultItemExcludes>
<InternalsVisibleTo Include="Pomidoro.Tests" />
```

The test project lives in `tests/Pomidoro.Tests/`, that is, inside the main project's folder.
SDK-style projects pick up **all** `.cs` files recursively by default, so the folder is excluded explicitly.
`InternalsVisibleTo` opens internal types to the tests — without it everything would have
to be made public just for the sake of testing.

### Packages

| Package | Version | Why |
|---|---|---|
| `Microsoft.Data.Sqlite` | 9.0.11 | working with the database |
| `SQLitePCLRaw.bundle_e_sqlite3` | 3.0.5 | bundled native SQLite library |

Versions are pinned exactly. The second package is there so as not to depend on the SQLite installed
on the system — and it is precisely because of it that single-file publishing has
`IncludeNativeLibrariesForSelfExtract` enabled.

## Tests

```
dotnet test tests\Pomidoro.Tests\Pomidoro.Tests.csproj -c Debug
```

The test project has analyzers too, but no `TreatWarningsAsErrors`. More details
in [06-testing.md](06-testing.md).

## Publishing

The Russian and English versions are **two different files**: the language is baked in at build time,
and there is no switch in the UI. Releasing both:

```
dotnet publish -p:PublishProfile=SingleFile-Ru
dotnet publish -p:PublishProfile=SingleFile-En
```

| Profile | Where | Language | Size | Needs .NET |
|---|---|---|---|---|
| `SingleFile-Ru` | `publish\ru\` | Russian | ~72 MB | no |
| `SingleFile-En` | `publish\en\` | English | ~72 MB | no |
| `SingleFile-Framework` | `publish\framework\` | Russian | ~2.4 MB | yes, .NET 9 Desktop Runtime |
| `FolderProfile` | `bin\Release\net9.0-windows\publish\win-x64\` | Russian | a folder of files | no |

The first three produce exactly one `Pomidoro.exe`. The fourth lays out
a self-contained build as a regular folder and is left over from the Visual Studio template.

The English version is turned on by the `AppLanguage=en` property; it works with any profile
and with a regular build:

```
dotnet build Pomidoro.sln -c Debug -p:AppLanguage=en
```

**Both versions put their intermediate files into the same `bin\Release`.** MSBuild
rebuilds them because the compiler arguments change, but if the release matters,
wipe `bin\Release` before it and check the window title of the finished `.exe`. That is the
cheapest way to check the language is the right one.

Key settings of the self-contained profile:

```xml
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
<PublishReadyToRun>false</PublishReadyToRun>
<DebugType>none</DebugType>
```

`IncludeNativeLibrariesForSelfExtract` is needed because of SQLite: without it the native
`e_sqlite3.dll` would stay next to the `.exe` as a separate file. The database is still
created next to the `.exe`, not in the temporary extraction folder — that is
what `AppContext.BaseDirectory` takes care of.

It can't be made any smaller: `PublishTrimmed` is not supported with WPF, and compression
is already enabled in the profile (without it the result would be around 160 MB).

## Continuous integration

`.github/workflows/build.yml` — on every push and pull request to `main`:

```yaml
runs-on: windows-latest
steps:
  - dotnet restore Pomidoro.sln
  - dotnet build Pomidoro.sln -c Release --no-restore
  - dotnet test tests/Pomidoro.Tests/Pomidoro.Tests.csproj -c Release --no-build
```

`windows-latest` is mandatory: WPF does not build on Linux.

## Encodings and line endings

- Sources: **UTF-8 without BOM**, line endings **CRLF**.
- The rules are pinned in `.editorconfig` and `.gitattributes`.

Mixed encodings in a project with Cyrillic are a source of trouble: some files
get read as ANSI, and comments turn into mojibake. If you edit files
with anything other than Visual Studio, keep an eye on this.

A separate trap, not related to the project but found nearby: Windows PowerShell 5.1 reads
`.ps1` scripts in UTF-8 **without** a BOM as ANSI and breaks on Cyrillic.
Scripts need the BOM.

## What git ignores

```
bin/  obj/  publish/  TestResults/
.vs/  *.user  *.suo
pomodoro.db  pomodoro.db-shm  pomodoro.db-wal
```

The database is excluded on purpose: it is personal data and has no place in the repository.
