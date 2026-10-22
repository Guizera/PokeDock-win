using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PokeDock.Windows;

internal sealed class WidgetHubWindow : Window
{
    private enum ArcadeGameKind { GameBoyColor, SuperNintendo, Snake, Memory }
    private enum SnakeDirection { Up, Down, Left, Right }
    private readonly record struct SnakeCell(int X, int Y);

    private readonly WidgetStateStore _store = WidgetStateStore.Current;
    private readonly RetroRomStore _romStore = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _snakeTimer = new() { Interval = TimeSpan.FromMilliseconds(145) };
    private readonly List<Button> _memoryButtons = [];
    private readonly HashSet<int> _memoryMatched = [];
    private List<string> _memoryValues = [];
    private DockWidgetKind _kind;
    private TextBlock? _focusTime;
    private int? _memoryFirst;
    private bool _memoryLocked;
    private RetroConsole? _runningConsole;
    private WebView2? _arcadeWebView;
    private ArcadeGameKind _selectedArcadeGame = ArcadeGameKind.GameBoyColor;
    private readonly List<SnakeCell> _snake = [];
    private SnakeCell _snakeFood;
    private SnakeDirection _snakeDirection = SnakeDirection.Right;
    private Grid? _snakeBoard;
    private TextBlock? _snakeStatus;
    private TextBlock? _snakeScoreText;
    private TextBlock? _memoryStatus;
    private TextBlock? _memoryMovesText;
    private bool _snakeRunning;
    private bool _snakeGameOver;
    private int _snakeScore;
    private int _memoryMoves;
    private int _memoryBoardVersion;

    public WidgetHubWindow(DockWidgetKind kind)
    {
        _kind = kind;
        _store.Changed += Store_Changed;
        _clock.Tick += Clock_Tick;
        _snakeTimer.Tick += SnakeTimer_Tick;
        _clock.Start();
        ResetSnake();
        Closed += WidgetHubWindow_Closed;
        Rebuild();
    }

    public void Show(DockWidgetKind kind)
    {
        _kind = kind;
        if (kind != DockWidgetKind.Arcade)
        {
            _snakeRunning = false;
            _snakeTimer.Stop();
        }
        Rebuild();
        Activate();
    }

    private void Rebuild()
    {
        _arcadeWebView?.Close();
        _arcadeWebView = null;
        Title = _kind switch
        {
            DockWidgetKind.Budget => Localization.Text("FinanceTitle"),
            DockWidgetKind.Arcade => "Retro Arcade",
            _ => Localization.Text("ProductivityTitle")
        };

        var root = new Grid { Background = _kind == DockWidgetKind.Arcade ? ArcadeShellBrush() : ResourceBrush("GameBoyShellBrush") };
        root.Loaded += (_, _) =>
        {
            NativeWindow.ResizeAndCenter(
                this,
                _kind == DockWidgetKind.Arcade ? 560 : 650,
                _kind == DockWidgetKind.Arcade ? 660 : 680);
            if (_kind == DockWidgetKind.Arcade) ApplyArcadeTitleBar(); else ResetTitleBar();
        };
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = _kind == DockWidgetKind.Arcade
                ? ScrollBarVisibility.Hidden
                : ScrollBarVisibility.Auto
        };
        var page = new StackPanel
        {
            Padding = _kind == DockWidgetKind.Arcade ? new Thickness(20) : new Thickness(24),
            Spacing = _kind == DockWidgetKind.Arcade ? 14 : 16
        };
        page.Children.Add(CreateHeader());

        if (_kind == DockWidgetKind.Budget)
        {
            BuildFinance(page);
        }
        else if (_kind == DockWidgetKind.Arcade)
        {
            BuildArcade(page);
        }
        else
        {
            BuildProductivity(page);
        }

        scroll.Content = page;
        root.Children.Add(scroll);
        Content = root;
    }

    private UIElement CreateHeader()
    {
        if (_kind == DockWidgetKind.Arcade)
        {
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var labels = new StackPanel { Spacing = 2 };
            labels.Children.Add(new TextBlock
            {
                Text = "POKÉDOCK",
                FontFamily = GameBoyFont(),
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.Black,
                Foreground = White(0.72),
                CharacterSpacing = 180
            });
            labels.Children.Add(new TextBlock
            {
                Text = "RETRO ARCADE",
                FontFamily = GameBoyFont(),
                FontSize = 24,
                FontWeight = Microsoft.UI.Text.FontWeights.Black,
                Foreground = White(1),
                CharacterSpacing = 70
            });
            header.Children.Add(labels);
            var power = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            power.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = new SolidColorBrush(global::Windows.UI.Color.FromArgb(220, 238, 65, 72))
            });
            power.Children.Add(new TextBlock
            {
                Text = "POWER",
                FontFamily = GameBoyFont(),
                FontSize = 8,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = White(0.62)
            });
            Grid.SetColumn(power, 1);
            header.Children.Add(power);
            return header;
        }

        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(new TextBlock
        {
            Text = "POKÉDOCK",
            FontFamily = GameBoyFont(),
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Black,
            Foreground = ResourceBrush("GameBoyMidBrush"),
            CharacterSpacing = 180
        });
        panel.Children.Add(new TextBlock
        {
            Text = _kind switch
            {
                DockWidgetKind.Budget => Localization.Text("Finance"),
                DockWidgetKind.Arcade => "RETRO ARCADE",
                _ => Localization.Text("Productivity")
            },
            FontFamily = GameBoyFont(),
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.Black,
            Foreground = ResourceBrush("GameBoyDarkBrush")
        });
        return panel;
    }

    private void BuildProductivity(Panel page)
    {
        var focus = new StackPanel { Spacing = 10 };
        _focusTime = new TextBlock
        {
            Text = FormatFocus(),
            FontFamily = GameBoyFont(),
            FontSize = 34,
            FontWeight = Microsoft.UI.Text.FontWeights.Black,
            Foreground = ResourceBrush("GameBoyDarkBrush")
        };
        focus.Children.Add(_focusTime);
        var focusActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        focusActions.Children.Add(ActionButton(
            _store.FocusEndsAt is null ? Localization.Text("Start") : Localization.Text("Pause"),
            _store.ToggleFocus));
        focusActions.Children.Add(ActionButton("25 MIN", () => _store.ResetFocus(25)));
        focusActions.Children.Add(ActionButton("50 MIN", () => _store.ResetFocus(50)));
        focus.Children.Add(focusActions);
        page.Children.Add(Section(Localization.Text("WidgetFocusTitle"), focus));

        var tasks = new StackPanel { Spacing = 8 };
        var taskInput = new TextBox { PlaceholderText = Localization.Text("NewTask") };
        var taskEntry = new Grid { ColumnSpacing = 8 };
        taskEntry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        taskEntry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        taskEntry.Children.Add(taskInput);
        var addTask = ActionButton(Localization.Text("Add"), () =>
        {
            _store.AddTask(taskInput.Text);
            taskInput.Text = string.Empty;
        });
        Grid.SetColumn(addTask, 1);
        taskEntry.Children.Add(addTask);
        tasks.Children.Add(taskEntry);
        foreach (var task in _store.Tasks)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var check = new CheckBox
            {
                Content = task.Title,
                IsChecked = task.IsDone,
                FontFamily = GameBoyFont(),
                FontSize = 11
            };
            var taskId = task.Id;
            check.Click += (_, _) => _store.ToggleTask(taskId);
            row.Children.Add(check);
            var remove = IconButton("\uE74D", Localization.Text("Remove"), () => _store.RemoveTask(taskId));
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);
            tasks.Children.Add(row);
        }
        page.Children.Add(Section(Localization.Text("WidgetTasksTitle"), tasks));

        var notes = new StackPanel { Spacing = 8 };
        var note = new TextBox
        {
            Text = _store.Note,
            PlaceholderText = Localization.Text("WriteQuickNote"),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120
        };
        notes.Children.Add(note);
        notes.Children.Add(ActionButton(Localization.Text("SaveNote"), () => _store.SetNote(note.Text)));
        page.Children.Add(Section(Localization.Text("WidgetNotesTitle"), notes));
    }

    private void BuildFinance(Panel page)
    {
        var summary = new Grid { ColumnSpacing = 8 };
        for (var index = 0; index < 3; index++)
        {
            summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        var values = new[]
        {
            (Localization.Text("Limit"), WidgetStateStore.Money(_store.MonthlyBudgetCents)),
            (Localization.Text("SpentThisMonth"), WidgetStateStore.Money(_store.SpentThisMonthCents)),
            (Localization.Text("Remaining"), WidgetStateStore.Money(_store.RemainingBudgetCents))
        };
        for (var index = 0; index < values.Length; index++)
        {
            var card = Summary(values[index].Item1, values[index].Item2);
            Grid.SetColumn(card, index);
            summary.Children.Add(card);
        }
        page.Children.Add(summary);

        var budget = new StackPanel { Spacing = 8 };
        var budgetInput = new TextBox { PlaceholderText = Localization.Text("LimitCurrency") };
        budget.Children.Add(budgetInput);
        budget.Children.Add(ActionButton(Localization.Text("SaveLimit"), () => _store.SetBudget(budgetInput.Text)));
        page.Children.Add(Section(Localization.Text("MonthlyBudget"), budget));

        var expenses = new StackPanel { Spacing = 8 };
        var title = new TextBox { PlaceholderText = Localization.Text("Description") };
        var amount = new TextBox { PlaceholderText = Localization.Text("AmountCurrency") };
        var entry = new Grid { ColumnSpacing = 8 };
        entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        entry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        entry.Children.Add(title);
        Grid.SetColumn(amount, 1);
        entry.Children.Add(amount);
        var add = ActionButton(Localization.Text("Add"), () =>
        {
            if (!_store.AddExpense(title.Text, amount.Text)) return;
            title.Text = string.Empty;
            amount.Text = string.Empty;
        });
        Grid.SetColumn(add, 2);
        entry.Children.Add(add);
        expenses.Children.Add(entry);
        foreach (var expense in _store.Expenses)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = expense.Title, FontFamily = GameBoyFont(), VerticalAlignment = VerticalAlignment.Center });
            var value = new TextBlock { Text = WidgetStateStore.Money(expense.AmountCents), FontWeight = Microsoft.UI.Text.FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(value, 1);
            row.Children.Add(value);
            var expenseId = expense.Id;
            var remove = IconButton("\uE74D", Localization.Text("Remove"), () => _store.RemoveExpense(expenseId));
            Grid.SetColumn(remove, 2);
            row.Children.Add(remove);
            expenses.Children.Add(row);
        }
        page.Children.Add(Section(Localization.Text("Expenses"), expenses));
    }

    private void BuildArcade(Panel page)
    {
        var cabinet = new StackPanel { Spacing = 12 };
        var tabs = new Grid
        {
            Padding = new Thickness(3),
            ColumnSpacing = 4,
            Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(80, 0, 0, 0))
        };
        for (var index = 0; index < 4; index++)
        {
            tabs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var games = new[]
        {
            (ArcadeGameKind.GameBoyColor, "GAME BOY COLOR"),
            (ArcadeGameKind.SuperNintendo, "SUPER NINTENDO"),
            (ArcadeGameKind.Snake, "SNAKE"),
            (ArcadeGameKind.Memory, "MEMORY")
        };
        for (var index = 0; index < games.Length; index++)
        {
            var game = games[index];
            var selected = _selectedArcadeGame == game.Item1;
            var tab = new Button
            {
                Style = ArcadeButtonStyle(),
                Content = game.Item2,
                Height = 34,
                Padding = new Thickness(5, 0, 5, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = selected ? ResourceBrush("GameBoyActionBrush") : new SolidColorBrush(global::Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                CornerRadius = new CornerRadius(7),
                Foreground = White(selected ? 1 : 0.72),
                FontFamily = GameBoyFont(),
                FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.Black
            };
            tab.Click += (_, _) => SelectArcadeGame(game.Item1);
            Grid.SetColumn(tab, index);
            tabs.Children.Add(tab);
        }
        cabinet.Children.Add(new Border { CornerRadius = new CornerRadius(9), Child = tabs });

        cabinet.Children.Add(_selectedArcadeGame switch
        {
            ArcadeGameKind.GameBoyColor => BuildEmulatorContent(RetroConsole.GameBoyColor),
            ArcadeGameKind.SuperNintendo => BuildEmulatorContent(RetroConsole.SuperNintendo),
            ArcadeGameKind.Snake => BuildSnakeContent(),
            ArcadeGameKind.Memory => BuildMemoryContent(),
            _ => new Grid()
        });

        page.Children.Add(new Border
        {
            Padding = new Thickness(14),
            Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 25, 30, 31)),
            BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(220, 0, 0, 0)),
            BorderThickness = new Thickness(4),
            CornerRadius = new CornerRadius(20),
            Child = cabinet
        });

        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = _selectedArcadeGame is ArcadeGameKind.GameBoyColor or ArcadeGameKind.SuperNintendo
                ? Localization.Text("EmulatorFooter")
                : Localization.Text("OriginalGamesFooter"),
            FontFamily = GameBoyFont(),
            FontSize = 8,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = White(0.58),
            VerticalAlignment = VerticalAlignment.Center
        });
        var vent = new Border
        {
            Width = 70,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(90, 0, 0, 0))
        };
        Grid.SetColumn(vent, 1);
        footer.Children.Add(vent);
        page.Children.Add(footer);
    }

    private Border BuildEmulatorContent(RetroConsole console)
    {
        var definition = RetroRomStore.Definition(console);
        var romPath = _romStore.RomPath(console);
        var isRunning = _runningConsole == console && romPath is not null;
        var content = new StackPanel { Spacing = 10 };

        if (isRunning)
        {
            var status = new TextBlock
            {
                Text = Localization.Text("LoadingEmulator"),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = GameBoyFont(),
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = ResourceBrush("GameBoyMidBrush")
            };
            var webView = new WebView2
            {
                Height = 320,
                DefaultBackgroundColor = global::Windows.UI.Color.FromArgb(255, 17, 17, 17)
            };
            _arcadeWebView = webView;
            webView.Loaded += async (_, _) => await StartEmulatorAsync(webView, console, status);
            var screen = new Grid { Height = 320 };
            screen.Children.Add(webView);
            screen.Children.Add(status);
            content.Children.Add(new Border
            {
                BorderBrush = ResourceBrush("GameBoyDarkBrush"),
                BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(6),
                Child = screen
            });
        }
        else
        {
            var placeholder = new StackPanel
            {
                Height = 314,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };
            placeholder.Children.Add(new FontIcon
            {
                Glyph = "\uE7FC",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 46,
                Foreground = ResourceBrush("GameBoyDarkBrush"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            placeholder.Children.Add(new TextBlock
            {
                Text = definition.Title,
                TextAlignment = TextAlignment.Center,
                FontFamily = GameBoyFont(),
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Black,
                Foreground = ResourceBrush("GameBoyDarkBrush")
            });
            placeholder.Children.Add(new TextBlock
            {
                Text = "NINTENDO",
                TextAlignment = TextAlignment.Center,
                FontFamily = GameBoyFont(),
                FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = ResourceBrush("GameBoyMidBrush")
            });
            placeholder.Children.Add(new TextBlock
            {
                Text = romPath is null
                    ? Localization.Format("ImportRomPrompt", string.Join(" / ", definition.Extensions))
                    : $"●  {Path.GetFileName(romPath)}",
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = GameBoyFont(),
                FontSize = 8,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = ResourceBrush(romPath is null ? "GameBoyMidBrush" : "GameBoyDarkBrush")
            });
            content.Children.Add(new Border
            {
                Padding = new Thickness(18),
                Background = ResourceBrush("GameBoyScreenBrush"),
                BorderBrush = ResourceBrush("GameBoyDarkBrush"),
                BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(6),
                Child = placeholder
            });
        }

        var actions = new Grid { ColumnSpacing = 9 };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var import = ArcadeActionButton(romPath is null ? Localization.Text("ImportRom") : Localization.Text("ReplaceRom"));
        import.Click += async (_, _) =>
        {
            _runningConsole = null;
            var error = await _romStore.ImportAsync(this, console);
            if (error is not null) await ShowMessageAsync(Localization.Text("ImportRomFailed"), error);
            Rebuild();
        };
        actions.Children.Add(import);
        if (romPath is not null)
        {
            var play = ArcadeActionButton(isRunning ? Localization.Text("Back") : Localization.Text("Play"));
            play.Click += (_, _) =>
            {
                _runningConsole = isRunning ? null : console;
                Rebuild();
            };
            Grid.SetColumn(play, 1);
            actions.Children.Add(play);
        }
        var note = new TextBlock
        {
            Text = Localization.Text("EmulatorInternet"),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = GameBoyFont(),
            FontSize = 7,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = ResourceBrush("GameBoyMidBrush")
        };
        Grid.SetColumn(note, 3);
        actions.Children.Add(note);
        content.Children.Add(actions);
        return new Border
        {
            Padding = new Thickness(12),
            Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 184, 184, 166)),
            CornerRadius = new CornerRadius(10),
            Child = content
        };
    }

    private void SelectArcadeGame(ArcadeGameKind game)
    {
        if (_selectedArcadeGame == game) return;
        _selectedArcadeGame = game;
        _runningConsole = null;
        if (game != ArcadeGameKind.Snake)
        {
            _snakeRunning = false;
            _snakeTimer.Stop();
        }
        if (game != ArcadeGameKind.Memory)
        {
            _memoryBoardVersion++;
            _memoryLocked = false;
            _memoryFirst = null;
        }
        Rebuild();
    }

    private Border BuildSnakeContent()
    {
        var content = new StackPanel { Spacing = 10 };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _snakeStatus = new TextBlock
        {
            Text = _snakeGameOver ? Localization.Text("GameOver") : _snakeRunning ? Localization.Text("InGame") : Localization.Text("Ready"),
            FontFamily = GameBoyFont(),
            FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.Black,
            Foreground = ResourceBrush("GameBoyDarkBrush")
        };
        header.Children.Add(_snakeStatus);
        _snakeScoreText = new TextBlock
        {
            Text = Localization.Format("Points", _snakeScore),
            FontFamily = GameBoyFont(),
            FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.Black,
            Foreground = ResourceBrush("GameBoyDarkBrush")
        };
        Grid.SetColumn(_snakeScoreText, 1);
        header.Children.Add(_snakeScoreText);
        content.Children.Add(header);

        _snakeBoard = new Grid
        {
            Width = 432,
            Height = 300,
            Background = ResourceBrush("GameBoyScreenBrush")
        };
        for (var column = 0; column < 16; column++)
        {
            _snakeBoard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        for (var row = 0; row < 12; row++)
        {
            _snakeBoard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }
        DrawSnake();
        content.Children.Add(new Border
        {
            BorderBrush = ResourceBrush("GameBoyDarkBrush"),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(6),
            Child = _snakeBoard
        });

        var controls = new Grid { ColumnSpacing = 8 };
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var start = ArcadeActionButton(_snakeRunning ? Localization.Text("Pause") : Localization.Text("Start"));
        start.Click += (_, _) =>
        {
            if (_snakeGameOver) ResetSnake();
            _snakeRunning = !_snakeRunning;
            if (_snakeRunning) _snakeTimer.Start(); else _snakeTimer.Stop();
            Rebuild();
        };
        controls.Children.Add(start);
        var reset = ArcadeActionButton(Localization.Text("NewGame"));
        reset.Click += (_, _) =>
        {
            ResetSnake();
            Rebuild();
        };
        Grid.SetColumn(reset, 1);
        controls.Children.Add(reset);

        var directions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        directions.Children.Add(DirectionButton("←", SnakeDirection.Left));
        directions.Children.Add(DirectionButton("↑", SnakeDirection.Up));
        directions.Children.Add(DirectionButton("↓", SnakeDirection.Down));
        directions.Children.Add(DirectionButton("→", SnakeDirection.Right));
        Grid.SetColumn(directions, 3);
        controls.Children.Add(directions);
        content.Children.Add(controls);

        return ArcadeGameSurface(content);
    }

    private Border BuildMemoryContent()
    {
        if (_memoryValues.Count == 0) ResetMemory();
        _memoryLocked = false;
        _memoryFirst = null;
        var boardVersion = ++_memoryBoardVersion;
        var content = new StackPanel { Spacing = 10 };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _memoryStatus = new TextBlock
        {
            Text = _memoryMatched.Count == 16 ? Localization.Text("YouWon") : Localization.Text("FindPairs"),
            FontFamily = GameBoyFont(),
            FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.Black,
            Foreground = ResourceBrush("GameBoyDarkBrush")
        };
        header.Children.Add(_memoryStatus);
        _memoryMovesText = new TextBlock
        {
            Text = Localization.Format("Moves", _memoryMoves),
            FontFamily = GameBoyFont(),
            FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.Black,
            Foreground = ResourceBrush("GameBoyDarkBrush")
        };
        Grid.SetColumn(_memoryMovesText, 1);
        header.Children.Add(_memoryMovesText);
        content.Children.Add(header);

        var board = new Grid
        {
            Padding = new Thickness(10),
            RowSpacing = 7,
            ColumnSpacing = 7,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = ResourceBrush("GameBoyScreenBrush")
        };
        for (var index = 0; index < 4; index++)
        {
            board.RowDefinitions.Add(new RowDefinition { Height = new GridLength(66) });
            board.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        _memoryButtons.Clear();
        for (var index = 0; index < 16; index++)
        {
            var cardIndex = index;
            var faceUp = _memoryMatched.Contains(index);
            var button = new Button
            {
                Style = ArcadeButtonStyle(),
                Height = 62,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = faceUp ? _memoryValues[index] : "?",
                FontFamily = GameBoyFont(),
                FontSize = 24,
                FontWeight = Microsoft.UI.Text.FontWeights.Black,
                Foreground = faceUp ? ResourceBrush("GameBoyActionBrush") : ResourceBrush("GameBoyScreenBrush"),
                Background = faceUp ? ResourceBrush("GameBoyLightBrush") : ResourceBrush("GameBoyDarkBrush"),
                BorderBrush = ResourceBrush("GameBoyDarkBrush"),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(5),
                IsEnabled = !faceUp
            };
            button.Click += (_, _) => RevealMemoryCard(cardIndex, boardVersion);
            Grid.SetRow(button, index / 4);
            Grid.SetColumn(button, index % 4);
            board.Children.Add(button);
            _memoryButtons.Add(button);
        }
        content.Children.Add(new Border
        {
            BorderBrush = ResourceBrush("GameBoyDarkBrush"),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(6),
            Child = board
        });
        var actions = new Grid();
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.Children.Add(new TextBlock
        {
            Text = Localization.Text("ClickCards"),
            FontFamily = GameBoyFont(),
            FontSize = 8,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = ResourceBrush("GameBoyMidBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        var reset = ArcadeActionButton(Localization.Text("NewGame"));
        reset.Click += (_, _) =>
        {
            ResetMemory();
            Rebuild();
        };
        Grid.SetColumn(reset, 1);
        actions.Children.Add(reset);
        content.Children.Add(actions);
        return ArcadeGameSurface(content);
    }

    private static Border ArcadeGameSurface(UIElement content) => new()
    {
        Padding = new Thickness(12),
        Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 184, 184, 166)),
        CornerRadius = new CornerRadius(10),
        Child = content
    };

    private Button DirectionButton(string symbol, SnakeDirection direction)
    {
        var button = new Button
        {
            Style = ArcadeButtonStyle(),
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            Content = symbol,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.Black,
            Background = ResourceBrush("GameBoyLightBrush"),
            Foreground = ResourceBrush("GameBoyDarkBrush")
        };
        button.Click += (_, _) => SetSnakeDirection(direction);
        return button;
    }

    private void SnakeTimer_Tick(object? sender, object e)
    {
        if (!_snakeRunning || _selectedArcadeGame != ArcadeGameKind.Snake) return;
        var head = _snake[0];
        var next = _snakeDirection switch
        {
            SnakeDirection.Up => new SnakeCell(head.X, head.Y - 1),
            SnakeDirection.Down => new SnakeCell(head.X, head.Y + 1),
            SnakeDirection.Left => new SnakeCell(head.X - 1, head.Y),
            _ => new SnakeCell(head.X + 1, head.Y)
        };
        if (next.X < 0 || next.X >= 16 || next.Y < 0 || next.Y >= 12 || _snake.Contains(next))
        {
            _snakeRunning = false;
            _snakeGameOver = true;
            _snakeTimer.Stop();
            if (_snakeStatus is not null) _snakeStatus.Text = Localization.Text("GameOver");
            return;
        }

        _snake.Insert(0, next);
        if (next == _snakeFood)
        {
            _snakeScore++;
            PlaceSnakeFood();
            if (_snakeScoreText is not null) _snakeScoreText.Text = Localization.Format("Points", _snakeScore);
        }
        else
        {
            _snake.RemoveAt(_snake.Count - 1);
        }
        DrawSnake();
    }

    private void DrawSnake()
    {
        if (_snakeBoard is null) return;
        _snakeBoard.Children.Clear();
        for (var index = 0; index < _snake.Count; index++)
        {
            var cell = _snake[index];
            var segment = new Border
            {
                Margin = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Background = index == 0 ? ResourceBrush("GameBoyDarkBrush") : ResourceBrush("GameBoyMidBrush")
            };
            Grid.SetColumn(segment, cell.X);
            Grid.SetRow(segment, cell.Y);
            _snakeBoard.Children.Add(segment);
        }
        var food = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Margin = new Thickness(4),
            Fill = ResourceBrush("GameBoyActionBrush")
        };
        Grid.SetColumn(food, _snakeFood.X);
        Grid.SetRow(food, _snakeFood.Y);
        _snakeBoard.Children.Add(food);
    }

    private void SetSnakeDirection(SnakeDirection direction)
    {
        var opposite = (_snakeDirection == SnakeDirection.Up && direction == SnakeDirection.Down)
            || (_snakeDirection == SnakeDirection.Down && direction == SnakeDirection.Up)
            || (_snakeDirection == SnakeDirection.Left && direction == SnakeDirection.Right)
            || (_snakeDirection == SnakeDirection.Right && direction == SnakeDirection.Left);
        if (!opposite) _snakeDirection = direction;
    }

    private void ResetSnake()
    {
        _snakeTimer.Stop();
        _snake.Clear();
        _snake.AddRange([new SnakeCell(7, 6), new SnakeCell(6, 6), new SnakeCell(5, 6)]);
        _snakeDirection = SnakeDirection.Right;
        _snakeScore = 0;
        _snakeRunning = false;
        _snakeGameOver = false;
        _snakeFood = new SnakeCell(12, 6);
    }

    private void PlaceSnakeFood()
    {
        var free = new List<SnakeCell>();
        for (var y = 0; y < 12; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var cell = new SnakeCell(x, y);
                if (!_snake.Contains(cell)) free.Add(cell);
            }
        }
        if (free.Count > 0) _snakeFood = free[Random.Shared.Next(free.Count)];
    }

    private async Task StartEmulatorAsync(WebView2 webView, RetroConsole console, TextBlock status)
    {
        try
        {
            var page = await _romStore.CreatePlayerPageAsync(console);
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "pokedock.local",
                _romStore.DirectoryPath,
                CoreWebView2HostResourceAccessKind.Allow);
            webView.Source = page;
            status.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            status.Text = Localization.Format("CouldNotStart", exception.Message);
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var root = Content as FrameworkElement;
        if (root?.XamlRoot is null) return;
        var dialog = new ContentDialog
        {
            XamlRoot = root.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    private void RevealMemoryCard(int index, int boardVersion)
    {
        if (boardVersion != _memoryBoardVersion || _memoryLocked || _memoryMatched.Contains(index) || _memoryFirst == index) return;
        _memoryButtons[index].Content = _memoryValues[index];
        _memoryButtons[index].Background = ResourceBrush("GameBoyLightBrush");
        _memoryButtons[index].Foreground = ResourceBrush("GameBoyActionBrush");
        if (_memoryFirst is null)
        {
            _memoryFirst = index;
            return;
        }

        var first = _memoryFirst.Value;
        _memoryFirst = null;
        _memoryMoves++;
        if (_memoryMovesText is not null) _memoryMovesText.Text = Localization.Format("Moves", _memoryMoves);
        if (_memoryValues[first] == _memoryValues[index])
        {
            _memoryMatched.Add(first);
            _memoryMatched.Add(index);
            _memoryButtons[first].IsEnabled = false;
            _memoryButtons[index].IsEnabled = false;
            if (_memoryMatched.Count == _memoryValues.Count && _memoryStatus is not null)
            {
                _memoryStatus.Text = Localization.Text("YouWon");
            }
            return;
        }

        _memoryLocked = true;
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(650);
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            if (boardVersion != _memoryBoardVersion)
            {
                _memoryLocked = false;
                return;
            }
            _memoryButtons[first].Content = "?";
            _memoryButtons[index].Content = "?";
            _memoryButtons[first].Background = ResourceBrush("GameBoyDarkBrush");
            _memoryButtons[index].Background = ResourceBrush("GameBoyDarkBrush");
            _memoryButtons[first].Foreground = ResourceBrush("GameBoyScreenBrush");
            _memoryButtons[index].Foreground = ResourceBrush("GameBoyScreenBrush");
            _memoryLocked = false;
        };
        timer.Start();
    }

    private void ResetMemory()
    {
        var symbols = new[] { "●", "◆", "▲", "■", "★", "♥", "☀", "☂" };
        _memoryValues = symbols.Concat(symbols).OrderBy(_ => Random.Shared.Next()).ToList();
        _memoryMatched.Clear();
        _memoryFirst = null;
        _memoryLocked = false;
        _memoryMoves = 0;
    }

    private void Store_Changed(object? sender, EventArgs e)
    {
        if (_kind != DockWidgetKind.Arcade) _ = DispatcherQueue.TryEnqueue(Rebuild);
    }

    private void Clock_Tick(object? sender, object e)
    {
        if (_focusTime is not null) _focusTime.Text = FormatFocus();
    }

    private string FormatFocus()
    {
        var seconds = _store.FocusRemainingSeconds;
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private void WidgetHubWindow_Closed(object sender, WindowEventArgs args)
    {
        _arcadeWebView?.Close();
        _snakeTimer.Stop();
        _clock.Stop();
        _store.Changed -= Store_Changed;
    }

    private static Border Section(string title, UIElement content)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = GameBoyFont(),
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.Black,
            Foreground = ResourceBrush("GameBoyDarkBrush")
        });
        panel.Children.Add(content);
        return new Border
        {
            Padding = new Thickness(16),
            Background = ResourceBrush("GameBoyScreenBrush"),
            BorderBrush = ResourceBrush("GameBoyDarkBrush"),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(10),
            Child = panel
        };
    }

    private static Border Summary(string title, string value)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = title, FontFamily = GameBoyFont(), FontSize = 8, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = value, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Black });
        return new Border { Padding = new Thickness(10), Background = ResourceBrush("GameBoyLightBrush"), CornerRadius = new CornerRadius(6), Child = panel };
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = GameBoyFont(),
            FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.Black
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button ArcadeActionButton(string text) => new()
    {
        Style = ArcadeButtonStyle(),
        Content = text,
        Height = 34,
        Padding = new Thickness(14, 0, 14, 0),
        FontFamily = GameBoyFont(),
        FontSize = 9,
        FontWeight = Microsoft.UI.Text.FontWeights.Black,
        Foreground = White(1),
        Background = ResourceBrush("GameBoyActionBrush"),
        BorderBrush = ResourceBrush("GameBoyDarkBrush"),
        BorderThickness = new Thickness(2),
        CornerRadius = new CornerRadius(17)
    };

    private static Style ArcadeButtonStyle() =>
        (Style)Microsoft.UI.Xaml.Application.Current.Resources["ArcadeButtonStyle"];

    private static Button IconButton(string glyph, string tooltip, Action action)
    {
        var button = new Button
        {
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            Content = new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 13 }
        };
        button.Click += (_, _) => action();
        button.SetValue(ToolTipService.ToolTipProperty, tooltip);
        return button;
    }

    private static Brush ResourceBrush(string key) =>
        (Brush)Microsoft.UI.Xaml.Application.Current.Resources[key];

    private static SolidColorBrush White(double opacity) => new(
        global::Windows.UI.Color.FromArgb(
            (byte)Math.Clamp(Math.Round(opacity * 255), 0, 255),
            255,
            255,
            255));

    private static LinearGradientBrush ArcadeShellBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new global::Windows.Foundation.Point(0, 0),
            EndPoint = new global::Windows.Foundation.Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop
        {
            Color = global::Windows.UI.Color.FromArgb(255, 74, 77, 71),
            Offset = 0
        });
        brush.GradientStops.Add(new GradientStop
        {
            Color = global::Windows.UI.Color.FromArgb(255, 40, 43, 43),
            Offset = 1
        });
        return brush;
    }

    private void ApplyArcadeTitleBar()
    {
        var titleBar = NativeWindow.GetAppWindow(this).TitleBar;
        var background = global::Windows.UI.Color.FromArgb(255, 43, 46, 46);
        var hover = global::Windows.UI.Color.FromArgb(255, 62, 65, 63);
        var white = global::Windows.UI.Color.FromArgb(255, 255, 255, 255);
        titleBar.BackgroundColor = background;
        titleBar.ForegroundColor = white;
        titleBar.InactiveBackgroundColor = background;
        titleBar.InactiveForegroundColor = global::Windows.UI.Color.FromArgb(170, 255, 255, 255);
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonForegroundColor = white;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonHoverForegroundColor = white;
        titleBar.ButtonPressedBackgroundColor = ResourceColor("GameBoyActionBrush");
        titleBar.ButtonPressedForegroundColor = white;
    }

    private void ResetTitleBar()
    {
        var titleBar = NativeWindow.GetAppWindow(this).TitleBar;
        titleBar.BackgroundColor = null;
        titleBar.ForegroundColor = null;
        titleBar.InactiveBackgroundColor = null;
        titleBar.InactiveForegroundColor = null;
        titleBar.ButtonBackgroundColor = null;
        titleBar.ButtonForegroundColor = null;
        titleBar.ButtonHoverBackgroundColor = null;
        titleBar.ButtonHoverForegroundColor = null;
        titleBar.ButtonPressedBackgroundColor = null;
        titleBar.ButtonPressedForegroundColor = null;
    }

    private static global::Windows.UI.Color ResourceColor(string key) =>
        ((SolidColorBrush)Microsoft.UI.Xaml.Application.Current.Resources[key]).Color;

    private static FontFamily GameBoyFont() =>
        (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["GameBoyFont"];
}
