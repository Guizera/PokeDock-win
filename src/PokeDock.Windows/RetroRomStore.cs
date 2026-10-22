using System.Text.Json;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PokeDock.Windows;

internal enum RetroConsole
{
    GameBoyColor,
    SuperNintendo
}

internal sealed record RetroConsoleDefinition(
    RetroConsole Console,
    string Title,
    string Core,
    string StorageName,
    string[] Extensions);

internal sealed class RetroRomStore
{
    public static RetroConsoleDefinition Definition(RetroConsole console) => console switch
    {
        RetroConsole.GameBoyColor => new(console, "GAME BOY COLOR", "gb", "game-boy-color", [".gb", ".gbc"]),
        RetroConsole.SuperNintendo => new(console, "SUPER NINTENDO", "snes", "super-nintendo", [".sfc", ".smc"]),
        _ => throw new ArgumentOutOfRangeException(nameof(console))
    };

    public string? RomPath(RetroConsole console)
    {
        var definition = Definition(console);
        return definition.Extensions
            .Select(extension => Path.Combine(RomDirectory, definition.StorageName + extension))
            .FirstOrDefault(File.Exists);
    }

    public async Task<string?> ImportAsync(Microsoft.UI.Xaml.Window owner, RetroConsole console)
    {
        var definition = Definition(console);
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads
        };
        foreach (var extension in definition.Extensions) picker.FileTypeFilter.Add(extension);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;

        try
        {
            var extension = Path.GetExtension(file.Path).ToLowerInvariant();
            if (!definition.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return Localization.Format("SelectRom", string.Join(Localization.IsPortuguese ? " ou " : " or ", definition.Extensions));
            }

            var data = await File.ReadAllBytesAsync(file.Path);
            if (!Matches(console, data))
            {
                return Localization.Format("IncompatibleRom", definition.Title);
            }

            Directory.CreateDirectory(RomDirectory);
            var destination = Path.Combine(RomDirectory, definition.StorageName + extension);
            await File.WriteAllBytesAsync(destination, data);
            foreach (var oldExtension in definition.Extensions.Where(item => item != extension))
            {
                var oldPath = Path.Combine(RomDirectory, definition.StorageName + oldExtension);
                if (File.Exists(oldPath)) File.Delete(oldPath);
            }
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Localization.Format("RomImportError", exception.Message);
        }
    }

    public async Task<Uri> CreatePlayerPageAsync(RetroConsole console)
    {
        var romPath = RomPath(console) ?? throw new FileNotFoundException(Localization.Text("RomNotImported"));
        var definition = Definition(console);
        Directory.CreateDirectory(RomDirectory);
        var romFileName = Path.GetFileName(romPath);
        var gameName = Path.GetFileNameWithoutExtension(romPath);
        var html = $$"""
            <!doctype html>
            <html lang="{{(Localization.IsPortuguese ? "pt-BR" : "en-US")}}">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <style>
                html, body, #game { width: 100%; height: 100%; margin: 0; overflow: hidden; background: #111; }
                .ejs_menu_bar, .ejs_context_menu { display: none !important; visibility: hidden !important; }
              </style>
            </head>
            <body>
              <div id="game"></div>
              <script>
                window.EJS_player = '#game';
                window.EJS_core = {{JsonSerializer.Serialize(definition.Core)}};
                window.EJS_gameName = {{JsonSerializer.Serialize(gameName)}};
                window.EJS_gameUrl = {{JsonSerializer.Serialize("https://pokedock.local/" + Uri.EscapeDataString(romFileName))}};
                window.EJS_pathtodata = 'https://cdn.emulatorjs.org/stable/data/';
                window.EJS_startOnLoaded = true;
                window.EJS_language = '{{(Localization.IsPortuguese ? "pt-BR" : "en-US")}}';
                window.EJS_volume = 0.7;
                window.EJS_color = '#8e245c';
                window.EJS_backgroundColor = '#111111';
                window.EJS_askBeforeExit = false;
                window.EJS_Buttons = {
                  playPause: false,
                  restart: false,
                  mute: false,
                  settings: false,
                  fullscreen: false,
                  saveState: false,
                  loadState: false,
                  screenRecord: false,
                  gamepad: false,
                  cheat: false,
                  volume: false,
                  saveSavFiles: false,
                  loadSavFiles: false,
                  quickSave: false,
                  quickLoad: false,
                  screenshot: false,
                  cacheManager: false,
                  exitEmulation: false
                };
                window.addEventListener('contextmenu', event => event.preventDefault());
              </script>
              <script src="https://cdn.emulatorjs.org/stable/data/loader.js"></script>
            </body>
            </html>
            """;
        var playerPath = Path.Combine(RomDirectory, "player.html");
        await File.WriteAllTextAsync(playerPath, html);
        return new Uri("https://pokedock.local/player.html");
    }

    public string DirectoryPath => RomDirectory;

    private static string RomDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PokeDock",
        "ROMs");

    private static bool Matches(RetroConsole console, byte[] data) => console switch
    {
        RetroConsole.GameBoyColor => MatchesGameBoy(data),
        RetroConsole.SuperNintendo => MatchesSuperNintendo(data),
        _ => false
    };

    private static bool MatchesGameBoy(byte[] data)
    {
        if (data.Length <= 0x14D) return false;
        byte checksum = 0;
        for (var index = 0x134; index <= 0x14C; index++)
        {
            checksum = unchecked((byte)(checksum - data[index] - 1));
        }
        return checksum == data[0x14D];
    }

    private static bool MatchesSuperNintendo(byte[] data)
    {
        if (data.Length < 32_768) return false;
        int[] headerOffsets = [0x7FC0, 0xFFC0, 0x81C0, 0x101C0];
        return headerOffsets.Any(offset =>
        {
            if (offset + 0x20 > data.Length) return false;
            var printableTitle = true;
            for (var index = offset; index < offset + 21; index++)
            {
                var value = data[index];
                if (value != 0 && value != 0x20 && (value < 0x21 || value > 0x7E))
                {
                    printableTitle = false;
                    break;
                }
            }
            var mapMode = data[offset + 0x15] & 0x0F;
            return printableTitle && mapMode <= 5;
        });
    }
}
