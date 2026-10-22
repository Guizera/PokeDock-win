using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PokeDock.Windows;

public sealed class Localization : DependencyObject
{
    private sealed record Translation(string Portuguese, string English);

    private static readonly IReadOnlyDictionary<string, Translation> Strings =
        new Dictionary<string, Translation>(StringComparer.Ordinal)
        {
            ["Yes"] = new("SIM", "YES"),
            ["No"] = new("NÃO", "NO"),
            ["Cancel"] = new("CANCELAR", "CANCEL"),
            ["Open"] = new("ABRIR", "OPEN"),
            ["Play"] = new("JOGAR", "PLAY"),
            ["Paused"] = new("PAUSADO", "PAUSED"),
            ["Now"] = new("AGORA!", "NOW!"),
            ["InMinutes"] = new("EM {0:0} MIN", "IN {0:0} MIN"),
            ["InHours"] = new("EM {0:0} H", "IN {0:0} H"),
            ["Pokeballs"] = new("{0}/{1} POKÉBOLAS", "{0}/{1} POKÉ BALLS"),

            ["OpenPokedex"] = new("Abrir Pokédex", "Open Pokédex"),
            ["FileExplorer"] = new("Explorador de Arquivos", "File Explorer"),
            ["OpenControls"] = new("Abrir controles do PokeDock", "Open PokeDock controls"),
            ["WindowsPowerOptions"] = new("Opções de energia do Windows", "Windows power options"),
            ["ExitPokeDock"] = new("Encerrar PokeDock e restaurar a barra do Windows", "Exit PokeDock and restore the Windows taskbar"),
            ["Lock"] = new("Bloquear", "Lock"),
            ["Sleep"] = new("Suspender", "Sleep"),
            ["SignOut"] = new("Sair da conta", "Sign out"),
            ["Restart"] = new("Reiniciar", "Restart"),
            ["ShutDown"] = new("Desligar", "Shut down"),
            ["SleepTitle"] = new("SUSPENDER", "SLEEP"),
            ["SleepMessage"] = new("O Windows entrará em suspensão.", "Windows will go to sleep."),
            ["SignOutTitle"] = new("SAIR DA CONTA", "SIGN OUT"),
            ["SignOutMessage"] = new("Todos os aplicativos serão fechados.", "All applications will be closed."),
            ["RestartTitle"] = new("REINICIAR", "RESTART"),
            ["RestartMessage"] = new("O Windows será reiniciado e os aplicativos serão fechados.", "Windows will restart and all applications will be closed."),
            ["ShutDownTitle"] = new("DESLIGAR", "SHUT DOWN"),
            ["ShutDownMessage"] = new("O Windows será desligado e os aplicativos serão fechados.", "Windows will shut down and all applications will be closed."),
            ["ExploringKanto"] = new("Explorando a região de Kanto", "Exploring the Kanto region"),
            ["CatchPokemon"] = new("Capturar {0}", "Catch {0}"),
            ["PinToTaskbar"] = new("FIXAR NA BARRA", "PIN TO TASKBAR"),
            ["UnpinFromTaskbar"] = new("DESAFIXAR DA BARRA", "UNPIN FROM TASKBAR"),
            ["OpenFileLocation"] = new("ABRIR LOCAL DO ARQUIVO", "OPEN FILE LOCATION"),
            ["CloseApp"] = new("FECHAR APP", "CLOSE APP"),
            ["ShowWindow"] = new("MOSTRAR JANELA", "SHOW WINDOW"),
            ["WindowCount"] = new("{0} · {1} JANELAS", "{0} · {1} WINDOWS"),
            ["CloseAllWindows"] = new("FECHAR TODAS AS JANELAS", "CLOSE ALL WINDOWS"),
            ["SearchWindows"] = new("Pesquisar aplicativos e arquivos", "Search apps and files"),
            ["SearchPlaceholder"] = new("Pesquisar", "Search"),
            ["ClearSearch"] = new("Limpar e fechar pesquisa", "Clear and close search"),
            ["SystemStatus"] = new("Status do Windows", "Windows status"),
            ["QuickSettings"] = new("CONFIGURAÇÕES RÁPIDAS", "QUICK SETTINGS"),
            ["NotificationsCalendar"] = new("NOTIFICAÇÕES E CALENDÁRIO", "NOTIFICATIONS AND CALENDAR"),
            ["TaskView"] = new("VISÃO DE TAREFAS", "TASK VIEW"),
            ["SoundSettings"] = new("CONFIGURAÇÕES DE SOM", "SOUND SETTINGS"),
            ["NetworkSettings"] = new("CONFIGURAÇÕES DE REDE", "NETWORK SETTINGS"),

            ["CustomizeDockTitle"] = new("Personalizar Dock", "Customize Dock"),
            ["CustomizeDock"] = new("PERSONALIZAR DOCK", "CUSTOMIZE DOCK"),
            ["ThemesAppsWidgets"] = new("TEMAS • APPS • WIDGETS", "THEMES • APPS • WIDGETS"),
            ["Preview"] = new("◉  PRÉVIA", "◉  PREVIEW"),
            ["Themes"] = new("▦  TEMAS", "▦  THEMES"),
            ["AppearanceBehavior"] = new("☷  APARÊNCIA E COMPORTAMENTO", "☷  APPEARANCE AND BEHAVIOR"),
            ["AnimateTheme"] = new("ANIMAR TEMA", "ANIMATE THEME"),
            ["ShowRunningApps"] = new("MOSTRAR APPS EM EXECUÇÃO", "SHOW RUNNING APPS"),
            ["ShowOnAllDisplays"] = new("MOSTRAR EM TODOS OS MONITORES", "SHOW ON ALL DISPLAYS"),
            ["StartWithWindows"] = new("INICIAR COM O WINDOWS", "START WITH WINDOWS"),
            ["StartupUnavailable"] = new("Disponível após instalar o pacote MSIX.", "Available after installing the MSIX package."),
            ["StartupManaged"] = new("Gerenciado pelas configurações de inicialização do Windows.", "Managed by Windows Startup settings."),
            ["EncounterNotifications"] = new("NOTIFICAR NOVOS ENCONTROS", "NOTIFY NEW ENCOUNTERS"),
            ["NotificationBadges"] = new("MOSTRAR CONTADORES DE NOTIFICAÇÕES", "SHOW NOTIFICATION BADGES"),
            ["NotificationAccessDenied"] = new("O Windows não concedeu acesso às notificações.", "Windows did not grant notification access."),
            ["TaskbarRestoreHint"] = new("A PokeDock restaura a barra nativa do Windows ao encerrar.", "PokeDock restores the native Windows taskbar when it exits."),
            ["DataUpdates"] = new("☷  DADOS E ATUALIZAÇÕES", "☷  DATA AND UPDATES"),
            ["ExportCollection"] = new("EXPORTAR COLEÇÃO", "EXPORT COLLECTION"),
            ["ImportCollection"] = new("IMPORTAR COLEÇÃO", "IMPORT COLLECTION"),
            ["CollectionExported"] = new("Coleção exportada com segurança.", "Collection exported safely."),
            ["CollectionImported"] = new("Coleção importada. Novos capturados: {0}.", "Collection imported. Newly caught: {0}."),
            ["CollectionTransferFailed"] = new("Não foi possível transferir a coleção: {0}", "Could not transfer collection: {0}"),
            ["CheckUpdates"] = new("VERIFICAR ATUALIZAÇÕES", "CHECK FOR UPDATES"),
            ["Version"] = new("VERSÃO {0}", "VERSION {0}"),
            ["CheckingUpdates"] = new("Verificando atualizações…", "Checking for updates…"),
            ["InstallingUpdate"] = new("Atualização encontrada. O PokeDock será reiniciado para instalar.", "Update found. PokeDock will restart to install it."),
            ["UpdateAvailable"] = new("Há uma atualização disponível pelo instalador do Windows.", "An update is available through Windows App Installer."),
            ["UpdateRequired"] = new("Uma atualização obrigatória está disponível.", "A required update is available."),
            ["NoUpdates"] = new("Você já está usando a versão mais recente.", "You are already using the latest version."),
            ["UpdateUnavailable"] = new("A verificação automática requer instalação por arquivo .appinstaller.", "Automatic checks require installation through an .appinstaller file."),
            ["UpdateFailed"] = new("Não foi possível instalar a atualização: {0}", "Could not install the update: {0}"),
            ["BarItems"] = new("▦  ITENS DA BARRA", "▦  BAR ITEMS"),
            ["AddApp"] = new("⊞  ADICIONAR APP", "⊞  ADD APP"),
            ["AddWidget"] = new("⊞  ADICIONAR WIDGET", "⊞  ADD WIDGET"),
            ["BarItemsHint"] = new("Use as setas para definir a ordem. Pokédex, Explorer, Pokémon, energia e controles permanecem fixos nas extremidades.", "Use the arrows to set the order. Pokédex, Explorer, Pokémon, power and controls remain fixed at the ends."),
            ["FixedApplication"] = new("Aplicativo fixo", "Fixed application"),
            ["MoveLeft"] = new("Mover para a esquerda", "Move left"),
            ["MoveRight"] = new("Mover para a direita", "Move right"),
            ["RemoveApp"] = new("Remover aplicativo", "Remove application"),
            ["RemoveWidget"] = new("Remover widget", "Remove widget"),
            ["Widget"] = new("Widget", "Widget"),
            ["Power"] = new("ENERGIA", "POWER"),
            ["PowerDescription"] = new("Bloquear, suspender, sair, reiniciar ou desligar", "Lock, sleep, sign out, restart or shut down"),
            ["Fixed"] = new("FIXO", "FIXED"),
            ["EncounterSettings"] = new("◉  ENCONTROS", "◉  ENCOUNTERS"),
            ["Frequency"] = new("FREQUÊNCIA", "FREQUENCY"),
            ["FrequencyCalm"] = new("TRANQUILA", "CALM"),
            ["FrequencyNormal"] = new("NORMAL", "NORMAL"),
            ["FrequencyLively"] = new("ANIMADA", "LIVELY"),
            ["FrequencyCalmDescription"] = new("Próximo encontro em cerca de 45 a 70 minutos.", "Next encounter in about 45 to 70 minutes."),
            ["FrequencyNormalDescription"] = new("Próximo encontro em cerca de 20 a 35 minutos.", "Next encounter in about 20 to 35 minutes."),
            ["FrequencyLivelyDescription"] = new("Próximo encontro em cerca de 10 a 20 minutos.", "Next encounter in about 10 to 20 minutes."),
            ["PauseAppearances"] = new("PAUSAR APARIÇÕES", "PAUSE APPEARANCES"),
            ["PauseAppearancesHint"] = new("Use a pausa durante trabalho, apresentações ou quando quiser silêncio. Os encontros voltam quando você reativar.", "Pause while working, presenting or whenever you want some quiet. Encounters return when you resume."),

            ["ThemeMeadowName"] = new("PRADARIA", "MEADOW"),
            ["ThemeMeadowSubtitle"] = new("Folhas ao vento", "Leaves in the breeze"),
            ["ThemeElectricName"] = new("ELÉTRICO", "ELECTRIC"),
            ["ThemeElectricSubtitle"] = new("Raios e faíscas", "Lightning and sparks"),
            ["ThemeNightName"] = new("NOTURNO", "NIGHTFALL"),
            ["ThemeNightSubtitle"] = new("Névoa e estrelas", "Mist and stars"),
            ["ThemeOceanName"] = new("OCEANO", "OCEAN"),
            ["ThemeOceanSubtitle"] = new("Ondas suaves", "Gentle waves"),
            ["ThemeVolcanicName"] = new("VULCÂNICO", "VOLCANIC"),
            ["ThemeVolcanicSubtitle"] = new("Brasas flutuantes", "Floating embers"),
            ["ThemePsychicName"] = new("PSÍQUICO", "PSYCHIC"),
            ["ThemePsychicSubtitle"] = new("Orbes luminosas", "Glowing orbs"),
            ["WidgetClockTitle"] = new("RELÓGIO", "CLOCK"),
            ["WidgetClockSubtitle"] = new("Hora local", "Local time"),
            ["WidgetPokedexSubtitle"] = new("Capturados em Kanto", "Caught in Kanto"),
            ["WidgetEncounterTitle"] = new("PRÓXIMO ENCONTRO", "NEXT ENCOUNTER"),
            ["WidgetEncounterSubtitle"] = new("Tempo até a aparição", "Time until appearance"),
            ["WidgetSpotifySubtitle"] = new("Música e controles", "Music and controls"),
            ["WidgetMusicTitle"] = new("MÚSICA", "MUSIC"),
            ["WidgetMusicSubtitle"] = new("Sessão de mídia do Windows", "Windows media session"),
            ["WidgetDollarSubtitle"] = new("Cotação de referência", "Reference exchange rate"),
            ["WidgetBudgetTitle"] = new("FINANÇAS · MÊS", "FINANCE · MONTH"),
            ["WidgetBudgetSubtitle"] = new("Orçamento mensal", "Monthly budget"),
            ["WidgetFocusTitle"] = new("FOCO", "FOCUS"),
            ["WidgetFocusSubtitle"] = new("Temporizador de produtividade", "Productivity timer"),
            ["WidgetTasksTitle"] = new("TAREFAS", "TASKS"),
            ["WidgetTasksSubtitle"] = new("Lista de tarefas rápidas", "Quick task list"),
            ["WidgetNotesTitle"] = new("NOTAS RÁPIDAS", "QUICK NOTES"),
            ["WidgetNotesSubtitle"] = new("Bloco de notas local", "Local notepad"),
            ["WidgetArcadeSubtitle"] = new("Jogos retrô", "Retro games"),

            ["PokedexSubtitle"] = new("151 ESPÉCIES PARA DESCOBRIR", "151 SPECIES TO DISCOVER"),
            ["TestEncounter"] = new("TESTAR ENCONTRO", "TEST ENCOUNTER"),
            ["Exploring"] = new("EXPLORANDO", "EXPLORING"),
            ["EncounterAvailable"] = new("ENCONTRO DISPONÍVEL", "ENCOUNTER AVAILABLE"),
            ["PokemonOnDock"] = new("UM POKÉMON ESTÁ NA DOCK", "A POKÉMON IS ON THE DOCK"),
            ["Caught"] = new("CAPTURADOS", "CAUGHT"),
            ["Seen"] = new("VISTOS", "SEEN"),
            ["Duplicates"] = new("REPETIDOS", "DUPLICATES"),
            ["All"] = new("TODOS", "ALL"),
            ["Unknown"] = new("DESCONHECIDOS", "UNKNOWN"),
            ["SearchPokemon"] = new("BUSCAR NOME OU Nº", "SEARCH NAME OR NO."),
            ["EncounterSystem"] = new("POKÉDOCK  •  SISTEMA DE ENCONTROS", "POKÉDOCK  •  ENCOUNTER SYSTEM"),
            ["IndependentEncounters"] = new("DOCK  •  ENCONTROS INDEPENDENTES", "DOCK  •  INDEPENDENT ENCOUNTERS"),
            ["SelectAction"] = new("A  •  SELECIONAR", "A  •  SELECT"),
            ["NoEncounterScheduled"] = new("SEM ENCONTRO AGENDADO", "NO ENCOUNTER SCHEDULED"),
            ["EncounterAnyMoment"] = new("PRÓXIMO A QUALQUER MOMENTO", "NEXT AT ANY MOMENT"),
            ["NextInMinutes"] = new("PRÓXIMO EM {0:0} MIN", "NEXT IN {0:0} MIN"),
            ["NextInHours"] = new("PRÓXIMO EM {0:0} H", "NEXT IN {0:0} H"),
            ["CaughtStatus"] = new("CAPTURADO", "CAUGHT"),
            ["SeenStatus"] = new("VISTO", "SEEN"),
            ["UnknownStatus"] = new("DESCONHECIDO", "UNKNOWN"),
            ["ViewPokemonDetails"] = new("Ver detalhes de {0}", "View {0} details"),
            ["BaseCaptureChance"] = new("CHANCE BASE DE CAPTURA: {0}%", "BASE CAPTURE CHANCE: {0}%"),
            ["Catches"] = new("CAPTURAS", "CATCHES"),
            ["Encounters"] = new("ENCONTROS", "ENCOUNTERS"),
            ["LastEncounter"] = new("ÚLTIMO ENCONTRO", "LAST ENCOUNTER"),
            ["KantoRecord"] = new("REGISTRO DE KANTO • DADOS SALVOS NESTE PC", "KANTO RECORD • DATA SAVED ON THIS PC"),
            ["Close"] = new("FECHAR", "CLOSE"),
            ["TypeNormal"] = new("NORMAL", "NORMAL"),
            ["TypeFire"] = new("FOGO", "FIRE"),
            ["TypeWater"] = new("ÁGUA", "WATER"),
            ["TypeElectric"] = new("ELÉTRICO", "ELECTRIC"),
            ["TypeGrass"] = new("PLANTA", "GRASS"),
            ["TypeIce"] = new("GELO", "ICE"),
            ["TypeFighting"] = new("LUTADOR", "FIGHTING"),
            ["TypePoison"] = new("VENENO", "POISON"),
            ["TypeGround"] = new("TERRA", "GROUND"),
            ["TypeFlying"] = new("VOADOR", "FLYING"),
            ["TypePsychic"] = new("PSÍQUICO", "PSYCHIC"),
            ["TypeBug"] = new("INSETO", "BUG"),
            ["TypeRock"] = new("PEDRA", "ROCK"),
            ["TypeGhost"] = new("FANTASMA", "GHOST"),
            ["TypeDragon"] = new("DRAGÃO", "DRAGON"),
            ["TypeSteel"] = new("AÇO", "STEEL"),
            ["TypeFairy"] = new("FADA", "FAIRY"),

            ["EncounterWindowTitle"] = new("Encontro Pokémon", "Pokémon Encounter"),
            ["WildEncounter"] = new("ENCONTRO SELVAGEM", "WILD ENCOUNTER"),
            ["Capture"] = new("CAPTURA", "CATCH RATE"),
            ["CatchAction"] = new("◉  CAPTURAR", "◉  CATCH"),
            ["Run"] = new("FUGIR", "RUN"),
            ["Continue"] = new("CONTINUAR", "CONTINUE"),
            ["WildPokemonAppeared"] = new("Um Pokémon selvagem apareceu!", "A wild Pokémon appeared!"),
            ["ThrewPokeball"] = new("Você lançou uma Pokébola…", "You threw a Poké Ball…"),
            ["BrokeFree"] = new("Quase! O Pokémon escapou da Pokébola.", "So close! The Pokémon broke free."),
            ["NoPokeballs"] = new("Suas Pokébolas acabaram.", "You are out of Poké Balls."),
            ["CaughtMessage"] = new("Conseguiu! O Pokémon foi capturado.", "Got it! The Pokémon was caught."),
            ["FledMessage"] = new("Oh não! O Pokémon fugiu.", "Oh no! The Pokémon fled."),
            ["EncounterUnavailable"] = new("O encontro não está mais disponível.", "The encounter is no longer available."),
            ["RanSafely"] = new("Você fugiu em segurança. Até o próximo encontro!", "You got away safely. Until the next encounter!"),
            ["PokemonDetails"] = new("Nº {0:000}  •  {1}", "NO. {0:000}  •  {1}"),
            ["FirstFailureSafe"] = new("★  1ª FALHA: SEM FUGA", "★  1ST MISS: NO ESCAPE"),
            ["Attempt"] = new("★  TENTATIVA {0}", "★  ATTEMPT {0}"),
            ["RegisteredPokedex"] = new("REGISTRADO NA POKÉDEX", "REGISTERED IN POKÉDEX"),
            ["EncounterEnded"] = new("O ENCONTRO TERMINOU", "THE ENCOUNTER ENDED"),
            ["NoPokeballsHint"] = new("Sem Pokébolas! Espere a próxima recarga ou fuja.", "No Poké Balls! Wait for the next refill or run."),
            ["Refill"] = new("RECARGA {0}", "REFILL {0}"),
            ["FullStock"] = new("ESTOQUE CHEIO", "FULL STOCK"),
            ["WillFlee"] = new("FUGIRÁ {0}", "WILL FLEE {0}"),
            ["CaughtNamed"] = new("Conseguiu! {0} foi capturado.", "Got it! {0} was caught."),
            ["FledNamed"] = new("Oh não! {0} fugiu após a tentativa.", "Oh no! {0} fled after the attempt."),
            ["TimeExpired"] = new("O tempo acabou. O Pokémon foi embora.", "Time ran out. The Pokémon left."),
            ["EncounterFinished"] = new("O encontro terminou.", "The encounter ended."),
            ["RarityCommon"] = new("COMUM", "COMMON"),
            ["RarityUncommon"] = new("INCOMUM", "UNCOMMON"),
            ["RarityRare"] = new("RARO", "RARE"),
            ["RaritySpecial"] = new("ESPECIAL", "SPECIAL"),

            ["Loading"] = new("CARREGANDO…", "LOADING…"),
            ["SetLimit"] = new("DEFINIR LIMITE", "SET LIMIT"),
            ["OnePending"] = new("1 PENDENTE", "1 PENDING"),
            ["PendingCount"] = new("{0} PENDENTES", "{0} PENDING"),
            ["OpenApp"] = new("ABRIR APP", "OPEN APP"),
            ["NothingPlaying"] = new("NADA TOCANDO", "NOTHING PLAYING"),
            ["Playing"] = new("REPRODUZINDO", "PLAYING"),
            ["PreviousTrack"] = new("FAIXA ANTERIOR", "PREVIOUS TRACK"),
            ["PlayPause"] = new("REPRODUZIR / PAUSAR", "PLAY / PAUSE"),
            ["NextTrack"] = new("PRÓXIMA FAIXA", "NEXT TRACK"),
            ["OpenPlayer"] = new("ABRIR PLAYER", "OPEN PLAYER"),
            ["WriteNote"] = new("ESCREVER NOTA", "WRITE NOTE"),
            ["OpenNote"] = new("ABRIR NOTA", "OPEN NOTE"),
            ["Update"] = new("ATUALIZAR", "UPDATE"),
            ["Media"] = new("MÍDIA", "MEDIA"),
            ["ComingSoon"] = new("EM BREVE", "SOON"),

            ["FinanceTitle"] = new("PokeDock · Finanças", "PokeDock · Finance"),
            ["ProductivityTitle"] = new("PokeDock · Produtividade", "PokeDock · Productivity"),
            ["Finance"] = new("FINANÇAS", "FINANCE"),
            ["Productivity"] = new("PRODUTIVIDADE", "PRODUCTIVITY"),
            ["Start"] = new("INICIAR", "START"),
            ["Pause"] = new("PAUSAR", "PAUSE"),
            ["NewTask"] = new("Nova tarefa", "New task"),
            ["Add"] = new("ADICIONAR", "ADD"),
            ["Remove"] = new("Remover", "Remove"),
            ["WriteQuickNote"] = new("Escreva uma nota rápida…", "Write a quick note…"),
            ["SaveNote"] = new("SALVAR NOTA", "SAVE NOTE"),
            ["Limit"] = new("LIMITE", "LIMIT"),
            ["SpentThisMonth"] = new("GASTO NO MÊS", "SPENT THIS MONTH"),
            ["Remaining"] = new("RESTANTE", "REMAINING"),
            ["LimitCurrency"] = new("Limite em R$", "Limit in R$"),
            ["SaveLimit"] = new("SALVAR LIMITE", "SAVE LIMIT"),
            ["MonthlyBudget"] = new("ORÇAMENTO MENSAL", "MONTHLY BUDGET"),
            ["Description"] = new("Descrição", "Description"),
            ["AmountCurrency"] = new("Valor em R$", "Amount in R$"),
            ["Expenses"] = new("LANÇAMENTOS", "EXPENSES"),
            ["EmulatorFooter"] = new("Emulador disponível • ROM fornecida pelo usuário", "Emulator available • user-supplied ROM"),
            ["OriginalGamesFooter"] = new("Jogos originais • nenhuma ROM necessária", "Original games • no ROM required"),
            ["LoadingEmulator"] = new("CARREGANDO EMULADOR…", "LOADING EMULATOR…"),
            ["ImportRomPrompt"] = new("IMPORTE UMA ROM {0}", "IMPORT A ROM {0}"),
            ["ImportRom"] = new("IMPORTAR ROM", "IMPORT ROM"),
            ["ReplaceRom"] = new("TROCAR ROM", "REPLACE ROM"),
            ["ImportRomFailed"] = new("NÃO FOI POSSÍVEL IMPORTAR", "COULD NOT IMPORT"),
            ["Back"] = new("VOLTAR", "BACK"),
            ["EmulatorInternet"] = new("EmulatorJS • internet necessária na primeira carga", "EmulatorJS • internet required on first load"),
            ["GameOver"] = new("GAME OVER", "GAME OVER"),
            ["InGame"] = new("EM JOGO", "IN GAME"),
            ["Ready"] = new("PRONTO", "READY"),
            ["Points"] = new("PONTOS {0}", "POINTS {0}"),
            ["NewGame"] = new("NOVO JOGO", "NEW GAME"),
            ["YouWon"] = new("VOCÊ VENCEU!", "YOU WON!"),
            ["FindPairs"] = new("ENCONTRE OS PARES", "FIND THE PAIRS"),
            ["Moves"] = new("JOGADAS {0}", "MOVES {0}"),
            ["ClickCards"] = new("CLIQUE NAS CARTAS", "CLICK THE CARDS"),
            ["CouldNotStart"] = new("NÃO FOI POSSÍVEL INICIAR: {0}", "COULD NOT START: {0}"),
            ["SelectRom"] = new("Selecione uma ROM {0}.", "Select a {0} ROM."),
            ["IncompatibleRom"] = new("O arquivo não parece ser uma ROM compatível com {0}.", "The file does not appear to be a ROM compatible with {0}."),
            ["RomImportError"] = new("Não foi possível importar a ROM: {0}", "Could not import the ROM: {0}"),
            ["RomNotImported"] = new("A ROM ainda não foi importada.", "The ROM has not been imported yet."),
            ["NoConnection"] = new("SEM CONEXÃO", "NO CONNECTION"),
            ["LastQuote"] = new("ÚLTIMA COTAÇÃO", "LAST QUOTE")
        };

    private static readonly string ActiveLanguage = DetectLanguage();

    public static bool IsPortuguese => ActiveLanguage.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(IsPortuguese ? "pt-BR" : "en-US");

    public static string Text(string key)
    {
        if (!Strings.TryGetValue(key, out var value)) return key;
        return IsPortuguese ? value.Portuguese : value.English;
    }

    public static string Format(string key, params object?[] arguments) =>
        string.Format(Culture, Text(key), arguments);

    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(Localization), new PropertyMetadata(null, KeyChanged));

    public static readonly DependencyProperty PlaceholderKeyProperty = DependencyProperty.RegisterAttached(
        "PlaceholderKey", typeof(string), typeof(Localization), new PropertyMetadata(null, PlaceholderKeyChanged));

    public static readonly DependencyProperty ToolTipKeyProperty = DependencyProperty.RegisterAttached(
        "ToolTipKey", typeof(string), typeof(Localization), new PropertyMetadata(null, ToolTipKeyChanged));

    public static readonly DependencyProperty OnKeyProperty = DependencyProperty.RegisterAttached(
        "OnKey", typeof(string), typeof(Localization), new PropertyMetadata(null, ToggleContentKeyChanged));

    public static readonly DependencyProperty OffKeyProperty = DependencyProperty.RegisterAttached(
        "OffKey", typeof(string), typeof(Localization), new PropertyMetadata(null, ToggleContentKeyChanged));

    public static void SetKey(DependencyObject target, string value) => target.SetValue(KeyProperty, value);
    public static string GetKey(DependencyObject target) => (string)target.GetValue(KeyProperty);
    public static void SetPlaceholderKey(DependencyObject target, string value) => target.SetValue(PlaceholderKeyProperty, value);
    public static string GetPlaceholderKey(DependencyObject target) => (string)target.GetValue(PlaceholderKeyProperty);
    public static void SetToolTipKey(DependencyObject target, string value) => target.SetValue(ToolTipKeyProperty, value);
    public static string GetToolTipKey(DependencyObject target) => (string)target.GetValue(ToolTipKeyProperty);
    public static void SetOnKey(DependencyObject target, string value) => target.SetValue(OnKeyProperty, value);
    public static string GetOnKey(DependencyObject target) => (string)target.GetValue(OnKeyProperty);
    public static void SetOffKey(DependencyObject target, string value) => target.SetValue(OffKeyProperty, value);
    public static string GetOffKey(DependencyObject target) => (string)target.GetValue(OffKeyProperty);

    private static void KeyChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is not string key) return;
        var value = Text(key);
        switch (target)
        {
            case TextBlock textBlock:
                textBlock.Text = value;
                break;
            case MenuFlyoutItem menuItem:
                menuItem.Text = value;
                break;
            case ToggleSwitch toggle:
                toggle.Header = value;
                break;
            case ContentControl content:
                content.Content = value;
                break;
        }
    }

    private static void PlaceholderKeyChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is TextBox textBox && args.NewValue is string key) textBox.PlaceholderText = Text(key);
    }

    private static void ToolTipKeyChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is string key) ToolTipService.SetToolTip(target, Text(key));
    }

    private static void ToggleContentKeyChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not ToggleSwitch toggle) return;
        var onKey = GetOnKey(toggle);
        var offKey = GetOffKey(toggle);
        if (!string.IsNullOrWhiteSpace(onKey)) toggle.OnContent = Text(onKey);
        if (!string.IsNullOrWhiteSpace(offKey)) toggle.OffContent = Text(offKey);
    }

    private static string DetectLanguage()
    {
        var testOverride = Environment.GetEnvironmentVariable("POKEDOCK_LANGUAGE");
        return string.IsNullOrWhiteSpace(testOverride)
            ? CultureInfo.CurrentUICulture.Name
            : testOverride.Trim();
    }
}
