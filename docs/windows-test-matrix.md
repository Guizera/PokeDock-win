# Matriz de validação Windows

## Automática

- Domínio em Linux/macOS e Windows.
- Build x64 e ARM64.
- Serialização e restauração de estado.
- Regras de tempo com relógio falso.
- UI automation dos fluxos críticos com Appium Windows Driver.

## Manual na VM

- Escala de tela: 100%, 125%, 150% e 200%.
- Um monitor e múltiplos monitores.
- Ativar “mostrar em todos os monitores”, conectar/desconectar um monitor e confirmar uma Dock centralizada por tela.
- Taskbar visível e com auto-ocultar.
- Encerrar o processo pelo Gerenciador de Tarefas e confirmar que o watchdog torna a taskbar nativa visível novamente.
- Reinício do Explorer com o PokeDock aberto.
- Suspender e retomar a VM.
- Abrir, ativar e fechar apps Win32 e empacotados.
- Spotify/Apple Music e sessões de mídia sem player ativo.
- Testar anterior, reproduzir/pausar, próxima faixa e abrir player no menu do widget de mídia.
- Abrir duas janelas do mesmo aplicativo, alternar entre elas pelo menu e usar “fechar todas as janelas”.
- Autorizar e negar o acesso às notificações; conferir badges nos aplicativos compatíveis.
- Habilitar inicialização com o Windows, reiniciar a sessão e validar também o estado gerenciado nas Configurações do Windows.
- Instalação limpa, atualização e desinstalação sem perder dados.
- Capturar um Pokémon, atualizar o MSIX sobre a instalação existente e confirmar contadores, último encontro, Pokébolas, frequência e pausa.
- Corromper apenas a cópia principal de `game-state.json` e confirmar a recuperação automática pelo backup.
- Internet indisponível e cache da cotação.
- Importação de ROM válida e rejeição de arquivo inválido.
- Abrir o detalhe de um Pokémon capturado e conferir tipos, raridade, estatísticas e data do último encontro.
- Alternar entre as frequências Tranquila, Normal e Animada; reiniciar o app e confirmar a persistência.
- Pausar as aparições com e sem um encontro ativo; reiniciar o app e confirmar que nenhum novo encontro é agendado.
- Windows em português: Dock, Pokédex, encontro, configurações, menus, energia, widgets e Arcade em português.
- Windows em inglês: os mesmos fluxos em inglês, incluindo textos dinâmicos, erros e tooltips.
- Idioma diferente de português: confirmar o fallback para inglês.
- Exportar a coleção, importar em uma instalação limpa e confirmar que capturas, encontros e último encontro foram preservados.
- Importar uma coleção mais antiga sobre progresso mais novo e confirmar que nenhum contador ou data regride.
- Instalar por um feed `.appinstaller` assinado e validar os estados sem atualização, disponível e obrigatória.

## Hardware físico antes da publicação

- Pelo menos um PC Windows 11 x64.
- Pelo menos um dispositivo Windows 11 ARM64 ou beta externo equivalente.
- Usuário padrão e cenário com aplicativo elevado para confirmar as limitações de integridade do Windows.
