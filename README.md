# PokeDock para Windows

Cliente nativo do PokeDock para Windows 11, desenvolvido em C#/.NET e WinUI 3.

## Estado atual

- Motor de encontros, captura, fuga, Pokébolas e frequências portado para uma biblioteca multiplataforma.
- Persistência em JSON atômico fora da instalação: arquivo principal em `%LOCALAPPDATA%\PokeDock\Game\game-state.json`, backup local e cópia durável em `%APPDATA%\PokeDock\Game\game-state.json`. Na inicialização, as cópias válidas são mescladas sem regredir Pokémon vistos ou capturados.
- Catálogo completo da primeira geração e 151 sprites importados do projeto macOS.
- Shell WinUI com Dock, encontro independente, editor de temas e Pokédex.
- Substituição visual da taskbar com restauração segura.
- Itens fixos para Explorer e energia, aplicativos escolhidos pelo usuário e aplicativos abertos ativáveis pela Dock.
- Pesquisa expansível dentro da Dock, integrada ao índice do Windows, além de status de rede/bateria e atalhos para ajustes rápidos, notificações, Visão de Tarefas, som e rede.
- Agrupamento das várias janelas de um aplicativo em um único ícone, seleção pelo menu de contexto, fechamento de todas as janelas e indicadores opcionais de notificações.
- Widgets configuráveis de relógio, Pokédex, próximo encontro, Spotify, mídia do Windows, dólar, finanças, foco, tarefas, notas e Arcade, com ordem persistida.
- Controles de faixa anterior, reproduzir/pausar e próxima faixa para Spotify e sessões de mídia do Windows.
- Interface em português e inglês, selecionada automaticamente pelo idioma de exibição do Windows.
- Inicialização opcional com o Windows, Dock opcional em todos os monitores e notificações de novos encontros.
- Arcade com Memória e emulação de Game Boy/Game Boy Color e Super Nintendo via EmulatorJS; as ROMs são importadas pelo usuário e permanecem em `%LOCALAPPDATA%\PokeDock\ROMs`.
- Testes automatizados do domínio e CI para Windows x64 e ARM64.
- Gravações de progresso serializadas para que capturas simultâneas não sejam descartadas durante atualização, encerramento ou troca de versão.
- Exportação/importação da coleção em `.pokedock`, preservando os maiores contadores e a data mais recente de cada Pokémon.
- Verificação de atualização para instalações distribuídas por MSIX/App Installer.
- Watchdog nativo incluído no pacote que restaura a taskbar mesmo se o processo do PokeDock encerrar inesperadamente, sem iniciar PowerShell ou compilar código em tempo de execução.

As integrações de taskbar, aplicativos, janelas, energia, widgets e emuladores estão na fase de validação manual no Windows ARM64.

## Requisitos para o app Windows

- Windows 11.
- Visual Studio com os workloads **.NET desktop development** e **Windows application development**.
- SDK .NET 10.
- Windows App SDK estável configurado pelo NuGet.

Abra `PokeDock.sln`, selecione `PokeDock.Windows` como projeto de inicialização e execute em `x64` ou `ARM64`.

### Testar os idiomas

Por padrão, `pt-*` usa português e os demais idiomas do Windows usam inglês. Para validar os dois idiomas sem alterar as configurações da VM, defina `POKEDOCK_LANGUAGE` somente na sessão atual do PowerShell:

```powershell
$env:POKEDOCK_LANGUAGE = "en-US"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-windows.ps1

$env:POKEDOCK_LANGUAGE = "pt-BR"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-windows.ps1
```

Remova a variável para voltar a acompanhar o Windows:

```powershell
Remove-Item Env:POKEDOCK_LANGUAGE
```

## Testes do domínio

Os projetos `PokeDock.Core`, `PokeDock.Application` e seus testes também funcionam no macOS e Linux:

```bash
dotnet test tests/PokeDock.Core.Tests/PokeDock.Core.Tests.csproj
```

## Atualizar catálogo e sprites a partir do macOS

Com `pokeDock-win` e `pokeTab` como pastas irmãs:

```bash
node scripts/import-macos-assets.mjs
```

O script reconstrói `Assets/Data/pokemon.json` e copia os 151 sprites. Os arquivos gerados são versionados para que o repositório Windows funcione sozinho.

## Releases e atualização automática

A distribuição de produção usa um `.msixbundle` assinado para x64 e ARM64 e um feed `PokeDock.appinstaller` hospedado nos assets do GitHub Releases. A primeira instalação associa o app ao feed; as versões seguintes são encontradas pelo Windows ao abrir o PokeDock ou pelo botão **Verificar atualizações**, sem baixar manualmente outro instalador.

O workflow `.github/workflows/release-windows.yml` cria os dois assets ao publicar uma tag `vX.Y.Z`. A configuração do certificado, das variáveis do GitHub e o checklist de lançamento estão em [`docs/windows-release.md`](docs/windows-release.md).

## Próximos marcos

1. Validar substituição da taskbar, múltiplos monitores e escalas de 100% a 200% no Windows 11 ARM64 e x64.
2. Configurar a identidade e o certificado de assinatura de produção no environment `windows-release` do GitHub.
3. Refinar sessões globais de mídia com capa do álbum e controles visíveis diretamente na Dock.
4. Adicionar miniaturas das janelas ao seletor de aplicativos e concluir a validação dos emuladores com ROMs fornecidas pelo usuário.

