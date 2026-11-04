# Contributing to Voice Translate

Thanks for your interest! This document covers how the project is structured and the conventions to
follow. Read [AGENTS.md](AGENTS.md) for the working agreements and approval boundaries.

## Fork and upstream boundaries

This is `UshMask/Voice-Translate`, a Windows fork of `Inhum/voica-win`.
The vendored [CORE-SPEC](docs/CORE-SPEC.md) describes upstream Voica and is kept intact.
Fork-specific behavior, providers, data paths and distribution are documented in
[README.md](README.md), [README.ru.md](README.ru.md) and
[README-Voice-Translate.md](README-Voice-Translate.md).
Changes intended for shared upstream behavior still follow the canonical-spec process.

The UI uses Voice Translate. The source namespace, project and development executable remain
`Voica` for compatibility. Existing `%APPDATA%\VoiceTranslate` data and the fork's mutex stay
unchanged. Application updates target `UshMask/Voice-Translate`; GigaAM assets remain pinned
to a dedicated `Inhum/voica-win` model release through separate model-repository constants.

## Build, run, test

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download). The target framework is
`net8.0-windows10.0.17763.0` (Windows 10 1809 minimum). The NuGet source is pinned in
[nuget.config](nuget.config).

The SDK is not always on PATH. For the current PowerShell session, prepend
`C:\Program Files\dotnet` as shown below, or invoke `C:\Program Files\dotnet\dotnet.exe` directly.
PowerShell state does not persist between separate tool calls.

```powershell
$env:Path = "C:\Program Files\dotnet;$env:Path"

dotnet build Voica.sln -c Debug

# Use synthetic data, never the user's saved settings/keys/history.
$env:GROQ_API_KEY = $null
$env:VOICETRANSLATE_TEST_DATA = Join-Path $PWD ".test-data\VoiceTranslate"
New-Item -ItemType Directory -Force $env:VOICETRANSLATE_TEST_DATA | Out-Null

# Self-test: pure logic, no GUI/network. Exit 0/1. It's a WinExe:
Start-Process -FilePath "src\Voica\bin\Debug\net8.0-windows10.0.17763.0\Voica.exe" `
  -ArgumentList "--test-all" -Wait -PassThru -NoNewWindow

# Run the tray app (no main window)
& "src\Voica\bin\Debug\net8.0-windows10.0.17763.0\Voica.exe"

# Local single-file, self-contained publish
dotnet publish src\Voica\Voica.csproj -c Release -r win-x64 -p:PublishSingleFile=true
```

A running `Voica.exe` locks the output executable. If rebuilding requires stopping it, identify the
specific process and agree on stopping it before doing so; do not kill every Voica process by default.

## The self-test is the test suite

There is no separate unit-test project. [SelfTest.cs](src/Voica/SelfTest.cs) is run via
`--test-all`; each check is a named `Check(...)` printing `[+]`/`[-]`. **Add a check for every new
piece of pure logic.** The suite must stay green. CI runs it on `windows-latest`. The suite also initializes the native ONNX
runtime to catch missing Visual C++ DLLs without a model, microphone or network.

The pinned app-local runtime lives in `vendor/vc-runtime/`; `scripts/verify-vc-runtime.ps1`
checks its x64 architecture, versions and hashes against the manifest and license inventory.
The project copies these DLLs to normal build output and embeds them alongside ONNX in
single-file publishes. Validate runtime updates on a clean Windows VM without a global
Visual C++ runtime; keep the Microsoft license documents and inventory in sync.

Tagged release candidates are built as draft GitHub pre-releases. After a local candidate passes
clean-Windows checks, tag its exact source commit, download the CI-built draft assets and verify
those exact packages from a clean VM snapshot before publishing the pre-release. Publication
still requires the explicit permission described in AGENTS.md; candidates must not be latest.
The Release workflow also accepts an existing source tag through manual dispatch, so build-tool
infrastructure can be repaired without moving an immutable tag. Record the workflow revision
separately from the tagged application-source revision when using that retry path.

Tests that mutate state (settings, key file, DB) must snapshot and restore it. Access to personal
runtime data requires the separate agreement described in AGENTS.md; use agreed test data for
isolated checks. The suite must not touch the person's clipboard. There is no per-test filter;
`--test-all` runs the suite.

## Diagnostics

Run only the diagnostics agreed for the task. Some open UI, use personal data, send network
requests, or overwrite the clipboard; the examples below document their use, not permission to run
them. Do not change system network settings for a test.

### Text normalization

`--normalize-corpus <file>` runs the §6.2 term rules over a file of past dictations (one per line)
with the current vocabulary and prints only the lines they changed. Agree on the corpus and
vocabulary access before using personal data. Spec §6.2 requires this measurement whenever a
threshold moves: thresholds come from data, every change over the corpus must be justified, and
the result is then frozen as a self-test fixture.

### Proxy and network

Proxy behavior (§9.5) is checked with the [stub](scripts/fake-proxy.ps1), not just by reading code.
`Deny` answers every request with 407; `Allow` tunnels real traffic. `VOICA_PROXY` overrides the
system proxy for this process only.

```powershell
# Terminal 1
.\scripts\fake-proxy.ps1 -Mode Deny

# Terminal 2
$env:VOICA_PROXY = "127.0.0.1:18899"
Start-Process -FilePath "src\Voica\bin\Debug\net8.0-windows10.0.17763.0\Voica.exe" `
  -ArgumentList "--probe-net" -Wait -NoNewWindow
```

**`--probe-net` sends real requests with the saved key.** It walks all five network surfaces:
key validation, chat model, a real silent dictation, update check, and model download. In Deny mode
all five must name the proxy; a raw error code indicates a §9.5 regression. Obtain separate
agreement before this diagnostic.

### Settings window

`--probe-settings [all|<tab>]` opens Settings on its own and prints its size: `all` cycles the
tabs; a number opens a specific tab, as the tray menu does. Verify §11.4 by reading back the window
height: it must grow and shrink per tab, including the first frame when opened at a chosen tab.
Pair the measurement with `PrintWindow` from PowerShell to capture the tab for visual inspection.
XAML inspection alone does not establish the window's actual size.

For the Voice Translate fork, the probe cycles all eight sidebar sections. Optional
`--probe-language en|ru` selects the UI language for that diagnostic process, and
`--probe-hold <seconds>` holds a chosen section for 1–300 seconds (default 20).
Set `VOICETRANSLATE_TEST_DATA` to an isolated folder with synthetic settings and remove
`GROQ_API_KEY` from the child environment when inspecting without personal data or credentials.

### Clipboard

**`--probe-clipboard` performs a full clipboard loan and return.** It lists the current contents,
what can be saved and how long that takes, then compares the restored data byte for byte.
[scripts/try-clipboard.ps1](scripts/try-clipboard.ps1) stages text, HTML, RTF, files, images, and a
frozen owner, and runs the probe on each: **it overwrites the clipboard**. These checks need separate
agreement and live applications; the self-test must not alter what the person has copied.

Real applications expose cases a logic test does not: an owner that is not pumping messages can
make each on-demand format wait 15 seconds; RichEdit can announce a format with no data; Word
treats even its own flushed clipboard data as coming from another program. In a COM test,
`Range.Paste()` ignores Word's default-paste options; `CommandBars.ExecuteMso('Paste')` follows the
Ctrl+V path.

## Architecture

Tray-only background app. Entry point is [Program.cs](src/Voica/Program.cs), rather than WPF's
default entry point: `App.xaml` is compiled as `<Page>` with `<StartupObject>Voica.Program`, so
`--test-all` short-circuits before any WPF initialization. `Program.Main` calls `AttachConsole`
so the self-test prints to the launching terminal. [App.xaml.cs](src/Voica/App.xaml.cs) enforces a
single instance with a named mutex, runs retention, and hosts the tray controller with
`ShutdownMode=OnExplicitShutdown`. Code splits into `Core/` (logic, spec-mapped) and `UI/` (WPF
windows and tray).

- [DictationController.cs](src/Voica/Core/DictationController.cs) orchestrates the state machine
  (`idle → recording → transcribing → idle`): hotkey → recorder → Groq → delivery, with
  `StateChanged`/`Error`/`Notice`/`ResultReady` events and persistence to Store. It lives on the
  WPF UI thread. Hotkey callbacks fire there, so async continuations capture and resume on that
  synchronization context.
- [Hotkey.cs](src/Voica/Core/Hotkey.cs) and [HotkeyBinding.cs](src/Voica/Core/HotkeyBinding.cs) use
  a global `WH_KEYBOARD_LL` hook for PTT holds and clean Toggle edges. A bare key (Right/Left Alt,
  CapsLock, ScrollLock, Pause, F13–F24) is swallowed entirely; for a combination such as
  Ctrl+Shift+Space, only the main key is swallowed while the modifiers are held. Swallowing bare
  Alt prevents menu activation from stealing focus and breaking insertion. Ctrl/Win are not
  offered as bare keys. Create and dispose the hook on the UI thread: it needs a message loop.
- [Recorder.cs](src/Voica/Core/Recorder.cs) uses NAudio to record WAV, 16 kHz mono PCM.
- [AutoInsert.cs](src/Voica/Core/AutoInsert.cs) places text on the clipboard and sends Ctrl+V via
  `SendInput` in insert mode (§5); the caller decides whether the text stays. **Native `INPUT`
  must marshal to 40 bytes on x64**, with the union sized to `MOUSEINPUT`. An undersized struct
  makes `SendInput` silently reject `cbSize` and inject nothing; a self-test guards this.
- [ClipboardKeeper.cs](src/Voica/Core/ClipboardKeeper.cs) borrows and restores the clipboard (§5,
  opt-in via `Prefs.KeepTextInClipboard = false`). It saves readable formats, lends the clipboard
  to the paste, then restores after 0.5 seconds (5 seconds for a remote-desktop viewer), unless
  something new was copied. Over 64 MB, reads over 500 ms, virtual files, or an unresponsive owner
  fall back to keeping the dictation. The restoration rule is what the system itself leaves when
  the source application closes. Ownership cannot be restored: Excel pastes values and Word uses
  its "other programs" rule.
- [Store.cs](src/Voica/Core/Store.cs) serializes SQLite history access through one connection and
  a `lock` (the Windows equivalent of the macOS serial queue, §7). It owns the audio-file
  lifecycle and honors the "store audio" setting (§8).
- [GroqClient.cs](src/Voica/Core/GroqClient.cs) implements the §2 request contract, vocabulary
  preparation for `prompt` (trim, empty → null, keep the last 800 characters), and error-message
  mapping. It is static and stores no secrets.
- [KeyStore.cs](src/Voica/Core/KeyStore.cs) stores the Groq key in a DPAPI-encrypted file, with
  `GROQ_API_KEY` as a development fallback (§9).
- [Prefs.cs](src/Voica/Core/Prefs.cs) stores JSON settings; its property initializers are the spec
  defaults. [Paths.cs](src/Voica/Core/Paths.cs) centralizes runtime paths.
  [Loc.cs](src/Voica/Core/Loc.cs) / `S` provide en/ru localization.

Runtime data lives outside the executable in `%APPDATA%\VoiceTranslate\`: `history.sqlite`,
`audio\*.wav`, `credentials.dat`, `settings.json`, and `voica.log`. Recognized text and audio
never live beside the executable, so they survive updates. These paths describe storage; reading,
changing, or deleting personal runtime data is governed by AGENTS.md.

## Windows release mechanics

The fork's installer has its own permanent AppId and installs into
`%LOCALAPPDATA%\Programs\Voice Translate`, with `VoiceTranslate.exe` as the installed executable.
It must coexist with upstream Voica, not upgrade or uninstall it. Preserve LICENSE and original
copyright attribution. Self-contained and framework-dependent publishes still build internally
as `Voica.exe`; release staging gives the downloads fork-specific names.

After publishing to `out/self` and `out/fx`, run
`./scripts/package-release.ps1 -Version 0.10.0` in PowerShell 7. It validates the resolved
dependency versions and vendored license hashes, then creates self-contained and runtime-dependent
portable ZIPs in `out/assets`. Keep the complete ZIP contents together when redistributing.
The installer also includes `LICENSE`, `DISTRIBUTION-TERMS.txt`, `THIRD-PARTY-NOTICES.txt`
and `licenses/`. Its license page identifies the application MIT terms separately from the
Microsoft SDK redistributable terms. Inno Setup is pinned to 6.7.4 to match the inventory.

`licenses/components.json` records versions, sources and checksums of the full vendor texts.
Update that inventory and the corresponding original texts when dependencies, runtime packs or
the compiler engine change. Preserve upstream notice files without translation or shortening.
GigaAM license/provenance resources are embedded in the EXE and written next to model files after
download/import and verification, including the cached-verification path; no extra request is made.

Before an authorized candidate: build both published variants, run isolated self-tests, compile
the installer, and test install/update/uninstall on a clean Windows account or VM. Local
compilation does not prove installation, signing, model availability or public release readiness.
Private vulnerability reporting and dependency/model distribution terms must be checked before
changing repository visibility. No signing service is configured by this change.


Follow [CORE-SPEC §13](docs/CORE-SPEC.md#13-выпуск-кандидаты-в-релиз) and obtain the required
release approval. User-visible changes go out as a candidate first: `vX.Y.Z-rc.N`, published as a
GitHub pre-release. Pre-releases stay out of `/releases/latest`, the endpoint used by update checks
(§10), so candidates are not offered to ordinary users.

- `<Version>` in [Voica.csproj](src/Voica/Voica.csproj) stays at the base `X.Y.Z` throughout the
  candidate cycle. CI checks the tag against that base version.
- CI stamps the full tagged version into the binary using `-p:InformationalVersion`, so About
  identifies the candidate. Installer version fields (Inno accepts numbers only) and asset names
  use the base version; the release title carries the full tag.
- `Updater.IsNewer` compares the numeric core and treats a final release as newer than its own
  candidate, so a tester learns when the final ships.
- Candidates are tagged from `main`; no separate release branch is required. After the candidate
  checks in §13 pass and the final release is approved, `vX.Y.Z` is published as the final release
  and becomes `latest`.

The [release workflow](.github/workflows/release.yml) builds the release from its tag. Update
checks must target **`UshMask/Voice-Translate`**. Do not repoint the independent model source when changing update identity.

## Pull requests

- Keep changes focused; match the surrounding code style.
- Update the self-test for new logic, and keep the build warning-free.
- Update user-facing strings in both supported languages.
- Note any behavior change against the spec section it implements.
- Be kind — see the [Code of Conduct](CODE_OF_CONDUCT.md).
