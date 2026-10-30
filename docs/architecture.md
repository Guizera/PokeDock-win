# Arquitetura

## Camadas

- `PokeDock.Core`: regras puras do jogo e leitura do catálogo. Não referencia APIs do Windows.
- `PokeDock.Application`: casos de uso e persistência independente da interface.
- `PokeDock.Windows`: WinUI 3, composição visual, MSIX e integrações Win32/WinRT.
- `PokeDock.Core.Tests`: testes determinísticos do domínio.

## Princípios

- Relógio e aleatoriedade são dependências injetáveis.
- Estado durável usa um schema versionado, escrita atômica, backup local e cópia no perfil roaming. Na recuperação, o progresso é mesclado pelo maior contador e pela data mais recente para nunca regredir uma captura.
- Catálogo e sprites são dados compartilháveis, não código Swift ou C# duplicado manualmente.
- Integrações do Windows devem ficar atrás de interfaces para permitir testes sem Explorer/taskbar reais.
- A barra não deve encerrar processos à força; o fechamento normal usa mensagens de janela.
- A substituição visual da taskbar deve preservar o Explorer, registrar a área de trabalho original e restaurar ambos ao sair.
- Um watchdog nativo, compilado para x64 e ARM64 e incluído no MSIX, aguarda o processo principal e torna as taskbars visíveis novamente caso haja encerramento inesperado. O aplicativo não inicia PowerShell, não executa scripts codificados e não compila código em tempo de execução.
- Aplicativos abertos são obtidos a partir de janelas Win32 elegíveis para a taskbar; o clique ativa a janela sem iniciar outra instância.
- Janelas são agrupadas pelo executável; cada janela continua selecionável individualmente no menu de contexto.

## Dados locais

O primeiro schema mantém o comportamento local do macOS. A coleção pode ser transportada em um envelope `.pokedock` versionado; a importação é cumulativa e nunca reduz contadores nem substitui uma data mais recente. Preferências visuais continuam locais ao sistema operacional.

Atualizações usam a identidade do pacote MSIX. A consulta só encontra uma versão quando a instalação veio de um feed App Installer configurado e assinado; execuções de desenvolvimento por `winapp` não possuem esse canal.

