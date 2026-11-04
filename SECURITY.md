# Security Policy

## Reporting a vulnerability

This policy covers Voice Translate (`UshMask/Voice-Translate`), a Windows fork of Voica.
Do not send fork-specific reports to the upstream maintainer.

Use this repository's [private vulnerability reporting page](https://github.com/UshMask/Voice-Translate/security/advisories/new) when private reporting is enabled. If that page is unavailable, request a private reporting channel in an issue without vulnerability details, keys, dictations, audio or logs. Private reporting must be configured before public distribution. Do not publish sensitive reproduction material.

Include the affected version, reproduction steps using synthetic data, and impact. Do not attach personal recordings or API credentials.

## System and data boundaries

- A local Windows tray application; no application backend or telemetry service.
- Settings, history, optional audio and encrypted keys live under `%APPDATA%\VoiceTranslate`. Compatible model files can also be used from `Documents\VoiceTranslate\models`.
- Groq keys use `credentials.dat`. Translation/editing keys use per-stage/provider files; `openrouter.dat` is a legacy fallback. Keys use Windows DPAPI with CurrentUser scope. `GROQ_API_KEY` is a development fallback, not an application-managed secret.
- Local GigaAM recognition keeps audio on the device. Enabled cloud recognition sends audio to `api.groq.com`; AI term correction sends text there. Translation/editing sends text, instructions and relevant vocabulary to the selected service: `openrouter.ai`, `routerai.ru`, or `api.tsarrouter.ru`.
- Application update checks use GitHub's API for this fork. Model downloads use the pinned upstream GitHub model release and GitHub's asset delivery infrastructure. Proxy routing follows the application's proxy configuration.
- History contains recognized and processed text. Audio storage and retention are configurable. Delete all data resets settings and removes saved keys, history and audio; local model deletion is a separate action. Environment variables are not removed by the application.

## Security properties and review focus

Protect saved and in-memory credentials, history, audio and vocabulary from unintended disclosure. Model downloads/imports must retain their pinned integrity checks. Updates must identify this fork, and the installer must not replace upstream Voica. Untrusted dictated text and provider responses must not become application commands or cause unintended file access. The clipboard and synthesized paste are deliberate local integration surfaces and require application-level testing.

Treat dependency/native-parser handling, stage/provider routing, data deletion and credential handling as in scope. This policy introduces no finding exclusions or accepted-risk exceptions. The user's Windows account and chosen providers are trust boundaries; DPAPI is not protection against malicious code already running as that user.

## Distribution and current limitations

Public release preparation is in progress. Only artifacts published by this fork should be described as its releases. The current build pipeline has no code-signing step; do not claim signed binaries or a configured signing service. Installation, upgrade, uninstall and live recognition/provider tests require their own validation. A passing self-test is not proof of those workflows or of security compliance.
