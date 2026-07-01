# Lexverse Progress

## Current Slice

- Created a .NET/WPF solution for the Windows desktop app.
- Added `LexVerse.Core` service contracts for OCR, translation, settings, selections, token links, and annotations.
- Added `LexVerse.Infrastructure` implementations for settings, screen capture, Bedrock popup translation, optional Google Vision OCR, and Google Translate.
- Built the first main window inspired by `media/demo.png`, using the provided Lexverse logo assets.
- Implemented the popup workflow:
  - Press `F6` or click `Select area (F6)`.
  - Drag a screen region.
  - The selected screen area is captured as an image.
  - AWS Bedrock receives the captured image directly.
  - Bedrock reads visible text, translates it, and returns mappings/annotations for the popup.
  - Popup appears near the selection.
  - Hovering source or target tokens highlights the corresponding translation pair.
- Added a project-specific Codex skill at `.codex/skills/lexverse-project`.
- Verified the solution with `dotnet build LexVerse.slnx`.
- Fixed WPF asset packaging by embedding Lexverse logo images as `Resource` items so `dotnet run` can resolve them at startup.
- Replaced focused-window F6 handling with a Windows global hotkey so selection works while Lexverse is running in the background.
- Redesigned the main window into a more original Lexverse control center inspired by `media/demo.png` rather than copying the sample layout.
- Replaced fake selected-area OCR text with real screen capture plus Bedrock multimodal popup translation.
- Updated the visual direction to Playfair Display typography, earthy/natural colors, atmospheric depth, and small hover/popup animations.
- Added a lightweight test runner at `tests/LexVerse.Tests` covering Google provider error formatting and translation token linking.
- Improved Google API error messages so disabled API responses show a short action and console link instead of raw JSON.
- Switched popup translation from Google Translate/Google Vision OCR chain to AWS Bedrock Converse multimodal so popup results can include recognized source text, translated text, source-target mappings, and keyword annotations.
- Added Bedrock structured payload parsing tests.
- Added Bedrock inference-profile normalization for base Anthropic model IDs such as `anthropic.*` in `us-*` and `eu-*` regions.
- Stopped guessing APAC inference profile IDs because `apac.<model-id>` can be invalid depending on the specific model/account/region; APAC should use an explicit inference profile ID or ARN in `LLM_MODEL`.
- Improved Bedrock on-demand throughput errors so they explain that `LLM_MODEL` must be an inference profile ID or ARN when required.
- Improved Bedrock invalid-model errors so they show the current model/profile and region.
- Adjusted Bedrock Converse inference config to send `temperature` only, because some models reject requests that include both `temperature` and `topP`.
- Changed the translation popup from per-word boxes to full-sentence source/translation text. Only detected keywords or important mapped phrases are highlighted inline; hovering a highlighted phrase shows details below.
- Updated the Bedrock prompt to translate naturally as complete sentences and avoid word-by-word mappings.
- Minimized the main app window during screen capture and popup display so selecting an area leaves the translation popup visible while Lexverse still stays accessible from the taskbar.
- Optimized Bedrock image input by resizing large captures and sending JPEG instead of full PNG to reduce latency.
- Changed selected-area translation so the popup appears immediately in a loading state, then updates in place when Bedrock returns. Provider failures now render inside the popup instead of opening an extra Windows message box.
- Reduced Bedrock popup latency further by sending a smaller optimized JPEG and requesting fewer output tokens.
- Narrowed popup keyword extraction to academic terms, acronyms, technical/domain terms, named concepts, and hard-to-translate phrases; descriptions now ask the model to explain the term in context.
- Added click-away behavior by closing translation popups on real outside mouse clicks instead of focus loss, avoiding premature closes during loading/result updates.
- Hardened Bedrock popup JSON parsing so truncated/missing closing braces are repaired when possible, with a plain-text fallback instead of surfacing raw parser errors.
- Added K/W/T segment mapping for popup mode: K is the captured region, W are source text fragments inside K, and T are translated fragments. The popup now displays translated T segments only.
- Added a transparent source-highlight overlay over K so hovering a translated T segment highlights the corresponding W rectangle directly on the screen.
- Improved popup ergonomics: selection cancellation now listens for Esc even if the overlay misses keyboard focus, popups prefer positions outside the captured K region, and outside-click close uses native cursor/mouse state.
- Limited popup keyword descriptions to model annotations only, with the prompt narrowed to abbreviations/acronyms that remain meaningful in translated text. Normal translated phrases and token mappings no longer open the keyword description panel.
- Repositioned popups after translated content is rendered so the final popup avoids K, added a draggable popup header, and tightened source-bound prompting/overlay drawing for W highlights.
- Hardened source-bound parsing for highlights: Bedrock responses using percent values, pixel values, or `left/top/right/bottom` style boxes are normalized back to K-relative `x/y/width/height`.
- Restored guaranteed hover highlighting: translated segments now highlight their W bounds when present, fall back to a light full-K hint when bounds are missing, and re-assert the transparent overlay as topmost on hover.
- Added the first Windows OCR-based K/W/T pipeline: selected K is read locally into indexed W fragments with OCR bounds, Bedrock translates those indexes into T, and parser prefers OCR bounds over model-estimated sourceBounds for reliable highlighting.
- Refined Windows OCR segmentation so W fragments split on large visual gaps, punctuation, and long OCR lines instead of sending whole OCR lines as one merged W. Bedrock OCR-mode prompt now requires exactly one T segment per W index.
- Made translated T content more compact in the popup and added vertical scrolling for long translation lists.
- Reworked the main app window from mostly descriptive panels into a practical control center with language controls, behavior toggles, realtime tuning sliders, runtime status, popup actions, and realtime actions.
- Added the first usable realtime translation slice: select a screen region, read it with Windows OCR on a configurable loop, translate changed text through Google Translate text-only, and render a click-through overlay near the region.
- Added realtime settings for scan interval, overlay opacity, font size, and max OCR fragments, with safe bounds and test coverage.

## Provider Notes

- `.env` is loaded for language defaults with `LEXVERSE_SOURCE_LANGUAGE` and `LEXVERSE_TARGET_LANGUAGE`.
- Bedrock popup translation, Google Translate realtime translation, and optional OCR providers are behind interfaces:
  - `ITranslationService`
  - `IOcrService`
- Selected-area translation now captures the selected screen region and sends the image directly to AWS Bedrock for richer popup output.
- Google Vision OCR remains available in infrastructure but is no longer required for the popup path.
- Google Translate remains useful for realtime text-only translation where structured popup metadata is less important.
- If Google APIs are not enabled for the service account, Lexverse reports the provider error instead of returning a fake sample sentence.
- Run tests with `dotnet run --project tests\LexVerse.Tests\LexVerse.Tests.csproj`.

## Next Steps

1. Improve Bedrock prompt/schema handling after testing real model outputs from `LLM_MODEL`.
2. Add a local OCR fallback for offline use or when Google Vision is not enabled.
3. Add tray/minimize-to-background behavior so closing the window can keep Lexverse available for global hotkeys.
4. Improve real-time overlay mode with per-fragment placement, scroll/content tracking, and stronger collision avoidance so translated Vietnamese text never overlaps already translated text.
5. Persist settings and app profiles to local JSON.
6. Expand tests for ignored terms, settings loading, provider fallback behavior, and OCR response parsing.
