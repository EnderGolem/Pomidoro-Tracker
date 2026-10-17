using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using Pomidoro.Audio;
using Pomidoro.Localization;
using Pomidoro.Mvvm;
using Pomidoro.Storage;
using Pomidoro.Tray;
using Pomidoro.ViewModels;
using Pomidoro.Views;

namespace Pomidoro;

// The whole app is assembled here: database, settings, view models, windows and tray.
// This is the only place where they know about each other.
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The database and the tray icon are released in OnExit: Application has no Dispose")]
public partial class App : Application
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);

    private SqliteSessionStore? _store;
    private TrayIcon? _tray;
    private MainViewModel? _viewModel;
    private MainWindow? _window;
    private SqlWindow? _sqlWindow;

    // The close button hides the window to the tray; the app really exits only via "Exit"
    // or when Windows ends the session.
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        if (TryOpenStore() is not SqliteSessionStore store)
        {
            Shutdown(1);
            return;
        }

        _store = store;

        AppSettings settings = new(store);

        _viewModel = new MainViewModel(store, settings, TimeProvider.System, new DispatcherTicker(TickInterval));
        _viewModel.Timer.PhaseFinished += (_, _) => Chime.Play();
        _viewModel.SqlConsoleRequested += (_, _) => ShowSqlConsole(store);

        _tray = new TrayIcon();
        _tray.OpenRequested += (_, _) => RestoreFromTray();
        _tray.ExitRequested += (_, _) => ExitApplication();

        _window = new MainWindow
        {
            DataContext = _viewModel,
            Icon = TomatoIcon.ForWindow
        };

        _window.Closing += OnMainWindowClosing;

        MainWindow = _window;
        _window.Show();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // Windows is ending the session: the window must really close, not go to the tray.
        _exiting = true;
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _store?.Dispose();

        base.OnExit(e);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The user cares about why the database failed to open, not about the exception type")]
    private static SqliteSessionStore? TryOpenStore()
    {
        try
        {
            return new SqliteSessionStore(SqliteSessionStore.DefaultDatabasePath);
        }
        catch (Exception exception)
        {
            // The database isn't open yet, so the language can't be read from it; the default stays.
            MessageBox.Show(
                Loc.S.DatabaseOpenFailed(SqliteSessionStore.DefaultDatabasePath, exception.Message),
                Loc.S.AppTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return null;
        }
    }

    private void ExitApplication()
    {
        _exiting = true;
        Shutdown();
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting || _window is null || _viewModel is null)
        {
            return;
        }

        e.Cancel = true;

        _viewModel.OnHiddenToTray();
        _window.ShowInTaskbar = false;
        _window.Hide();
    }

    private void RestoreFromTray()
    {
        if (_window is null || _viewModel is null)
        {
            return;
        }

        _window.ShowInTaskbar = true;
        _window.Show();

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();

        // The window could have sat in the tray until the next day, and then "today" is another day.
        _viewModel.ReloadVisibleScreen();
    }

    private void ShowSqlConsole(ISqlConsole console)
    {
        if (_sqlWindow is not null)
        {
            _sqlWindow.Activate();
            return;
        }

        SqlConsoleViewModel viewModel = new(console);
        viewModel.Executed += (_, _) => RefreshAfterSql(viewModel);

        _sqlWindow = new SqlWindow
        {
            DataContext = viewModel,
            Owner = _window,
            Icon = TomatoIcon.ForWindow
        };

        _sqlWindow.Closed += (_, _) => _sqlWindow = null;
        _sqlWindow.Show();
    }

    // The query could have dropped the tables the screen is built from. The error is shown
    // in the console itself: crashing the app over a user's query would be too much.
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Arbitrary SQL can break anything, and that has to stay in the console window")]
    private void RefreshAfterSql(SqlConsoleViewModel console)
    {
        try
        {
            _viewModel?.ReloadAfterSql();
        }
        catch (Exception exception)
        {
            console.ReportRefreshFailure(exception);
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            Loc.S.UnexpectedError(e.Exception.GetType().Name, e.Exception.Message),
            Loc.S.AppTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        ExitApplication();
    }
}
