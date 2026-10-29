# Releases do PokeDock para Windows

O canal de distribuição usa um bundle MSIX assinado e um arquivo `PokeDock.appinstaller` publicados no GitHub Releases. A primeira instalação é feita abrindo o `.appinstaller`; depois disso, o Windows consulta a mesma URL, encontra versões superiores e instala a atualização sem exigir que a pessoa baixe outro instalador manualmente.

## Antes do primeiro release

1. Escolha uma assinatura de produção confiável pelo Windows. Um certificado autoassinado serve apenas para testes porque teria de ser instalado manualmente em cada computador.
2. Configure a identidade do publicador como variável do repositório:
   - `WINDOWS_SIGNING_MODE`: `artifact`, `esigner` ou `pfx`.
   - `WINDOWS_PUBLISHER`: assunto exato do certificado, incluindo ordem e campos, por exemplo `CN=Empresa, O=Empresa, C=BR`. Esta variável não é necessária no modo `esigner`, pois o workflow lê o publicador diretamente do certificado carregado.
3. Crie o environment `windows-release` no GitHub e proteja-o com aprovação manual.

### Opção A: SSL.com eSigner

Use `WINDOWS_SIGNING_MODE=esigner` e crie estes secrets dentro do environment `windows-release`:

- `ESIGNER_USERNAME`: usuário da conta SSL.com.
- `ESIGNER_PASSWORD`: senha da conta SSL.com.
- `ESIGNER_TOTP_SECRET`: código secreto exibido junto do QR Code do eSigner. Este é o segredo fixo que gera os códigos de seis dígitos; não use o PIN de quatro dígitos nem um código temporário do aplicativo autenticador.

O workflow instala uma versão fixa e verificada do eSigner CKA, carrega o certificado no repositório de certificados do runner e assina o bundle MSIX com `SignTool`. A chave privada permanece no HSM da SSL.com. Se a conta tiver mais de um certificado de assinatura, configure também a variável opcional `ESIGNER_CERTIFICATE_THUMBPRINT` com a impressão digital do certificado correto.

Nunca envie essas credenciais por mensagem, não as coloque em variáveis comuns e não as versione. O PIN de quatro dígitos não é usado pelo GitHub Actions.

### Opção B: Azure Artifact Signing

Configure uma credencial federada OIDC para o GitHub e atribua a ela a função **Artifact Signing Certificate Profile Signer**. Depois, crie:

- Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` e `AZURE_SUBSCRIPTION_ID`.
- Variables: `AZURE_ARTIFACT_SIGNING_ENDPOINT`, `AZURE_ARTIFACT_SIGNING_ACCOUNT` e `AZURE_ARTIFACT_SIGNING_PROFILE`.

Use `WINDOWS_SIGNING_MODE=artifact`. A disponibilidade de perfis de confiança pública depende do país e do tipo de identidade validada pela Microsoft.

### Opção C: certificado PFX confiável

Converta o PFX para Base64 sem quebras de linha e salve o conteúdo no secret `WINDOWS_CERTIFICATE_BASE64`. Salve a senha em `WINDOWS_CERTIFICATE_PASSWORD` e use `WINDOWS_SIGNING_MODE=pfx`. Nunca versione o PFX, a senha ou o Base64.

## Publicar

Crie e envie uma tag estável no formato `vX.Y.Z`:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

O workflow `Release Windows` executa testes de empacotamento, produz um bundle único para x64 e ARM64, assina e valida o bundle, gera o feed e publica estes assets:

- `PokeDock-Windows.msixbundle`
- `PokeDock.appinstaller`

O workflow também pode ser iniciado manualmente pela aba Actions. Por segurança, uma execução manual não publica um GitHub Release por padrão: ela apenas mantém o bundle assinado e o `.appinstaller` como artefatos privados da execução para validação. Marque `publish_release` somente quando quiser publicar. Se o release da tag já existir, os dois assets Windows são adicionados ou substituídos sem remover outros assets.

Se macOS e Windows compartilharem o mesmo repositório, cada release estável que se tornar `latest` precisa conter os assets das duas plataformas. Um release estável apenas de macOS faria a URL `releases/latest/download/PokeDock.appinstaller` deixar de encontrar o feed Windows. Como alternativa, mantenha o app Windows em um repositório próprio.

## Primeira instalação e atualizações

Divulgue sempre este link:

```text
https://github.com/OWNER/REPOSITORY/releases/latest/download/PokeDock.appinstaller
```

O usuário baixa e abre esse arquivo uma única vez. O feed verifica atualizações ao abrir o PokeDock, no máximo a cada quatro horas, e também em segundo plano. O botão **Verificar atualizações** consulta o feed e inicia a atualização quando ela existe.

Os seguintes valores precisam permanecer estáveis em todos os releases:

- `Identity Name`: `PokeDock.Windows`.
- `Publisher`: exatamente o mesmo assunto do certificado.
- nome dos assets publicados.

Cada nova versão deve ser numericamente maior. A tag `v1.4.2` vira a versão MSIX `1.4.2.0`. Os dados do jogo permanecem fora da pasta de instalação, portanto a atualização substitui o aplicativo sem apagar a coleção.

## Checklist de validação

1. Instale `PokeDock.appinstaller` em um Windows 11 limpo, sem certificados de desenvolvimento.
2. Confirme x64 e ARM64.
3. Publique uma versão maior e abra a versão anterior.
4. Confirme o aviso do App Installer e o fluxo pelo botão do app.
5. Confirme que coleção, preferências e ROMs continuam presentes.
6. Verifique a assinatura com `Get-AuthenticodeSignature` e execute o Windows App Certification Kit antes do lançamento público.
