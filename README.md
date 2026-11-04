# Voice Translate for Windows

[Русский](README.ru.md) · [Source](https://github.com/UshMask/Voice-Translate) · [Releases](https://github.com/UshMask/Voice-Translate/releases)

Speech recognition, text processing and translation in a Windows tray application.
Voice Translate is a fork of [Voica for Windows](https://github.com/Inhum/voica-win),
originally created by Ivan Ushakov. This repository prepares an independent public fork;
it is not an official Voica distribution.

## Capabilities

- Recognize Russian speech locally with GigaAM v3, or use Groq Whisper for cloud recognition.
- Insert the result into the focused field or show an editable result window.
- Optionally translate Russian speech into English and edit text with a selected API model.
  Choose the order of translation and editing.
- Configure OpenRouter, RouterAI or Tsar Router separately for translation and editing.
- Keep a vocabulary, apply correction rules, and inspect processing stages in local history.
- Use a persistent draggable microphone island and an optional local live speech preview.

Windows 10 1809+ / Windows 11, x64. The interface supports English and Russian.
Local recognition is Russian-only; the live preview is an approximate draft and uses extra RAM/CPU.

## Start from source

Install the .NET 8 SDK with Windows Desktop support, then run `build-local.bat` and
`ЗАПУСК.bat`. Development binaries retain the internal name `Voica.exe`.

1. Open Settings → Recognition. Download the compatible GigaAM model (about 215 MB),
   or choose an existing compatible model folder. Files are checked before import.
2. New settings use local recognition with translation and editing off. To add either stage,
   open Translation & editing. Existing settings keep their choices.
3. To use translation or editing, select a provider, save its API key for the relevant stage,
   enter a supported model ID and save the stage settings. Provider usage may be billed.
4. Focus a text field. Press Right Alt to record, then press it again to stop.

If the selected recognition engine needs a model or key, Settings opens on Recognition.
Missing requirements for enabled translation/editing open that section instead. A short hint
explains the next step; downloading models and checking API services require your action.

The release pipeline produces a self-contained `VoiceTranslate.exe`, a per-user
`VoiceTranslate-Setup-<version>.exe` installer, and `VoiceTranslate-fx.exe` for computers
with the .NET 8 Desktop Runtime. A local build is not a published release.
See [detailed instructions](README-Voice-Translate.md).

## Data and network

Settings, DPAPI-encrypted keys, history and optional recordings live in
`%APPDATA%\VoiceTranslate`. Model files can also be used from
`Documents\VoiceTranslate\models`. Existing data paths are retained across updates.
The fork uses a separate installation identity and directory from original Voica.

Local recognition keeps audio on the device. Enabled Groq recognition sends audio to Groq;
AI term correction sends text to Groq. Enabled translation/editing sends text and relevant
instructions to its selected provider. Local recognition alone does not make those later
stages offline. The application has no telemetry service or application backend.

Application updates are checked against this repository. The pinned GigaAM download remains
in an upstream model release, independently of application updates. Model files are not bundled
in the installer. Proxy settings affect application network requests.

## Development and attribution

See [CONTRIBUTING.md](CONTRIBUTING.md) for isolated tests and release preparation.
The upstream [CORE-SPEC](docs/CORE-SPEC.md) is retained as a reference; fork-specific behavior
is described in this repository's README files. Do not independently edit that vendored spec.

Code is distributed under the [MIT license](LICENSE), retaining the original copyright notice.
Voica's author, assets and upstream project remain credited. The optional support link in the
application goes to the original Voica author. API-provider fees are separate.
Third-party dependencies and the separately downloaded model retain their respective terms.
