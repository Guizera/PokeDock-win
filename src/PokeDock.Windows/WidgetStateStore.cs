using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeDock.Windows;

public sealed record QuickTask(Guid Id, string Title, bool IsDone = false);

public sealed record BudgetExpense(Guid Id, string Title, int AmountCents, DateTimeOffset Date);

public sealed class WidgetStateStore
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PokeDock",
        "Widgets",
        "widget-state.json");

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static WidgetStateStore Current { get; } = Load();

    private readonly List<QuickTask> _tasks = [];
    private readonly List<BudgetExpense> _expenses = [];
    private bool _loadingDollar;

    public event EventHandler? Changed;

    public IReadOnlyList<QuickTask> Tasks => _tasks;
    public IReadOnlyList<BudgetExpense> Expenses => _expenses;
    public string Note { get; private set; } = string.Empty;
    public int FocusMinutes { get; private set; } = 25;
    public int PausedFocusSeconds { get; private set; } = 25 * 60;
    public DateTimeOffset? FocusEndsAt { get; private set; }
    public int MonthlyBudgetCents { get; private set; }
    public double? DollarRate { get; private set; }
    public string? DollarDate { get; private set; }
    public string? DollarError { get; private set; }

    public int OpenTaskCount => _tasks.Count(task => !task.IsDone);

    public int SpentThisMonthCents
    {
        get
        {
            var now = DateTimeOffset.Now;
            return _expenses
                .Where(expense => expense.Date.Year == now.Year && expense.Date.Month == now.Month)
                .Sum(expense => expense.AmountCents);
        }
    }

    public int RemainingBudgetCents => MonthlyBudgetCents - SpentThisMonthCents;

    public int FocusRemainingSeconds
    {
        get
        {
            if (FocusEndsAt is not { } end) return Math.Max(0, PausedFocusSeconds);
            return Math.Max(0, (int)Math.Ceiling((end - DateTimeOffset.Now).TotalSeconds));
        }
    }

    public void ToggleFocus()
    {
        if (FocusEndsAt is { } end)
        {
            PausedFocusSeconds = Math.Max(0, (int)Math.Ceiling((end - DateTimeOffset.Now).TotalSeconds));
            FocusEndsAt = null;
        }
        else
        {
            if (PausedFocusSeconds <= 0) PausedFocusSeconds = FocusMinutes * 60;
            FocusEndsAt = DateTimeOffset.Now.AddSeconds(PausedFocusSeconds);
        }
        PersistAndNotify();
    }

    public void ResetFocus(int minutes)
    {
        FocusMinutes = minutes is 50 ? 50 : 25;
        FocusEndsAt = null;
        PausedFocusSeconds = FocusMinutes * 60;
        PersistAndNotify();
    }

    public void SetNote(string value)
    {
        Note = value.Trim();
        PersistAndNotify();
    }

    public void AddTask(string title)
    {
        var value = title.Trim();
        if (value.Length == 0) return;
        _tasks.Add(new QuickTask(Guid.NewGuid(), value));
        PersistAndNotify();
    }

    public void ToggleTask(Guid id)
    {
        var index = _tasks.FindIndex(task => task.Id == id);
        if (index < 0) return;
        _tasks[index] = _tasks[index] with { IsDone = !_tasks[index].IsDone };
        PersistAndNotify();
    }

    public void RemoveTask(Guid id)
    {
        if (_tasks.RemoveAll(task => task.Id == id) == 0) return;
        PersistAndNotify();
    }

    public bool SetBudget(string value)
    {
        if (!TryParseCents(value, allowZero: true, out var cents)) return false;
        MonthlyBudgetCents = cents;
        PersistAndNotify();
        return true;
    }

    public bool AddExpense(string title, string value)
    {
        var cleanTitle = title.Trim();
        if (cleanTitle.Length == 0 || !TryParseCents(value, allowZero: false, out var cents)) return false;
        _expenses.Insert(0, new BudgetExpense(Guid.NewGuid(), cleanTitle, cents, DateTimeOffset.Now));
        PersistAndNotify();
        return true;
    }

    public void RemoveExpense(Guid id)
    {
        if (_expenses.RemoveAll(expense => expense.Id == id) == 0) return;
        PersistAndNotify();
    }

    public async Task RefreshDollarAsync(bool force = false)
    {
        if (_loadingDollar || (!force && DollarRate is not null)) return;
        _loadingDollar = true;
        try
        {
            var response = await Http.GetFromJsonAsync<DollarResponse>(
                "https://api.frankfurter.dev/v2/rate/usd/brl");
            if (response is null || !double.IsFinite(response.Rate) || response.Rate <= 0)
            {
                throw new JsonException("Invalid USD/BRL quote.");
            }
            DollarRate = response.Rate;
            DollarDate = response.Date;
            DollarError = null;
            Save();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            DollarError = DollarRate is null ? Localization.Text("NoConnection") : Localization.Text("LastQuote");
        }
        finally
        {
            _loadingDollar = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public static string Money(int cents)
    {
        var numberFormat = (NumberFormatInfo)Localization.Culture.NumberFormat.Clone();
        numberFormat.CurrencySymbol = "R$";
        return (cents / 100m).ToString("C2", numberFormat);
    }

    private void PersistAndNotify()
    {
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(StatePath)!;
        Directory.CreateDirectory(directory);
        var state = new StoredState(
            Note,
            FocusMinutes,
            PausedFocusSeconds,
            FocusEndsAt,
            MonthlyBudgetCents,
            _tasks,
            _expenses,
            DollarRate,
            DollarDate);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state));
    }

    private static WidgetStateStore Load()
    {
        var store = new WidgetStateStore();
        try
        {
            if (!File.Exists(StatePath)) return store;
            var state = JsonSerializer.Deserialize<StoredState>(File.ReadAllText(StatePath));
            if (state is null) return store;
            store.Note = state.Note ?? string.Empty;
            store.FocusMinutes = state.FocusMinutes is 50 ? 50 : 25;
            store.PausedFocusSeconds = Math.Max(0, state.PausedFocusSeconds);
            store.FocusEndsAt = state.FocusEndsAt;
            store.MonthlyBudgetCents = Math.Max(0, state.MonthlyBudgetCents);
            store._tasks.AddRange(state.Tasks ?? []);
            store._expenses.AddRange(state.Expenses ?? []);
            store.DollarRate = state.DollarRate is > 0 ? state.DollarRate : null;
            store.DollarDate = state.DollarDate;
        }
        catch (JsonException)
        {
            // Invalid state is replaced on the next successful edit.
        }
        catch (IOException)
        {
            // Widgets remain usable with defaults when local storage is unavailable.
        }
        return store;
    }

    private static bool TryParseCents(string value, bool allowZero, out int cents)
    {
        cents = 0;
        var styles = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
        var cultures = new[] { CultureInfo.CurrentCulture, CultureInfo.GetCultureInfo("pt-BR"), CultureInfo.InvariantCulture };
        foreach (var culture in cultures)
        {
            if (!decimal.TryParse(value, styles, culture, out var amount)) continue;
            var rounded = decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
            if (rounded < (allowZero ? 0 : 1) || rounded > int.MaxValue) continue;
            cents = (int)rounded;
            return true;
        }
        return false;
    }

    private sealed record StoredState(
        string? Note,
        int FocusMinutes,
        int PausedFocusSeconds,
        DateTimeOffset? FocusEndsAt,
        int MonthlyBudgetCents,
        List<QuickTask>? Tasks,
        List<BudgetExpense>? Expenses,
        double? DollarRate,
        string? DollarDate);

    private sealed record DollarResponse(
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("rate")] double Rate);
}
