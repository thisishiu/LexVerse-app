---
name: lexverse-project
description: Build and maintain the Lexverse Windows desktop translation app in this repository. Use when continuing product implementation, updating the WPF UI, wiring OCR or Google Translate providers, maintaining project progress notes, or making architecture decisions for Lexverse popup and real-time overlay translation.
---

# Lexverse Project

## Workflow

- Read `initial.md` and `docs/PROGRESS.md` before substantial work.
- Treat this as a Windows desktop product, not a landing page.
- Keep the app runnable after each slice with `dotnet build LexVerse.slnx`.
- Prefer small vertical slices: UI, service contract, provider implementation, then validation.
- Keep `.env` and `lexverse-42d205709f8e.json` private; never print secrets.

## Architecture

- `src/LexVerse.App`: WPF UI, selection overlay, translation popup, future tray/global hotkey behavior.
- `src/LexVerse.Core`: provider-independent models and service interfaces.
- `src/LexVerse.Infrastructure`: OCR, translation, settings, credentials, local persistence, and external integrations.

## Product Priorities

- Popup translation is the first priority.
- The popup should preserve word-level interaction: hovering a source word highlights the related translated token.
- Technical terms may remain unchanged and should receive annotations.
- Real-time mode must avoid translated text overlapping existing translated text.
- UI should stay compact, polished, readable, and close to the dark control surface shown in `media/demo.png`.

## Validation

- Run `dotnet build LexVerse.slnx` after code changes.
- Update `docs/PROGRESS.md` whenever a meaningful slice is completed or a major decision changes.
