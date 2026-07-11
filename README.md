# Equip Best Item

A Mount & Blade II: Bannerlord mod that equips your characters with the best
items in one click.

## Features

- **Equip best** button on every equipment slot; **equip-all** for the current
  character or for every party hero — planned up front and executed as one
  transfer batch, so items freed from one hero stay available to the next.
- Per-slot **search weights** (per character, per battle/civilian/stealth set)
  with negative weights supported (`Weight: -1` = prefer light gear). Zero out
  a slot's weights (the Lock button) to exclude it from searching. Weapon
  slots can pin a **weapon class** (bow, one-handed axe, shield, …) — each
  class brings its own sensible default weights.
- Choice of search method: parameter weights (default) or the game's built-in
  Effectiveness score. Configurable — along with the slot button color and
  panel toggles — via
  [MCM](https://www.nexusmods.com/mountandblade2bannerlord/mods/612) when
  installed, or `settings.json` otherwise.
- Fast: single-pass search with cached item stat vectors.
- Extra, entirely optional: an AI button that fills in the slot filters from a
  plain-language request (see below). Everything above works without it.

## Installation

Requires two common framework mods (like most UI mods):
[Harmony](https://www.nexusmods.com/mountandblade2bannerlord/mods/2006) and
[UIExtenderEx](https://www.nexusmods.com/mountandblade2bannerlord/mods/2102).

1. Install Harmony and UIExtenderEx (NexusMods or Steam Workshop).
2. Copy `Bannerlord.EquipBestItem` into your game's `Modules` folder
   (or download from [NexusMods](https://www.nexusmods.com/mountandblade2bannerlord/mods/369)).
3. Enable all three in the launcher — the load order is enforced
   automatically (Harmony → UIExtenderEx → Equip Best Item).

Optionally install
[Mod Configuration Menu](https://www.nexusmods.com/mountandblade2bannerlord/mods/612)
to edit the mod's settings in-game; without it the same settings live in
`Documents/Mount and Blade II Bannerlord/Configs/EquipBestItem/settings.json`.

### Enabling the AI assistant (optional)

Skip this section unless you want the AI button — the mod does not need it.
The button appears in the inventory as soon as a backend is available.
Three ways, easiest first:

**Zero-config (local model).** Run [Ollama](https://ollama.com)
(`ollama pull qwen3:4b`), LM Studio or Player2 before starting the game —
the mod auto-detects a backend on its default port (11434 / 1234 / 4315)
and uses its first model. No keys, no files, nothing leaves your PC.

**OpenRouter (cloud, one key for many models).** Set the `EBI_AI_API_KEY`
environment variable and edit
`Documents/Mount and Blade II Bannerlord/Configs/EquipBestItem/settings.json`:

```json
"ai": { "provider": "openai",
        "endpoint": "https://openrouter.ai/api/v1/chat/completions",
        "model": "anthropic/claude-haiku-4.5" }
```

**Anthropic API.** Set `EBI_AI_API_KEY` and:

```json
"ai": { "provider": "anthropic", "model": "claude-haiku-4-5" }
```

Any other OpenAI-compatible server works the same way via `endpoint`.
Settings are read at game start. Backend quirks (a rejected JSON response
format, a chat template that drops the system role) are detected and worked
around automatically on the first request, then remembered for the session.

## Translations

The mod ships with translations for **every language the game supports**
(Brazilian Portuguese, both Chinese variants, English, French, German,
Italian, Japanese, Korean, Latin-American Spanish, Polish, Russian, Turkish).
It follows the game's language automatically — no configuration.

Most of what you see in the inventory needs no translation in the first
place: item parameter names, slot names and weapon classes are the game's own
strings. Only the mod's own messages (button hints, AI status lines, MCM
option names) are translated, and they live in
`Bannerlord.EquipBestItem/ModuleData/Languages/<CODE>/std_module_strings_xml.xml`.

To fix or add one:

1. Copy the English base
   (`Languages/std_module_strings_xml.xml`) into a language subfolder
   (existing ones, or a new one — the folder name is conventional).
2. Set the `<tag language="…"/>` value to the game's name for the language
   **exactly as the game spells it** — e.g. `Deutsch`, `Türkçe`, `简体中文`
   (see `Modules/Native/ModuleData/Languages` for the list).
3. Translate the `text` attributes. Never change the `id` attributes, and
   keep placeholders like `{COUNT}` intact.
4. Save as UTF-8. Picked up on the next game start; any missing string falls
   back to English, so partial translations are safe.

The AI assistant is language-independent regardless — it answers in whatever
language the request is written in.

## Building

```
dotnet build Bannerlord.EquipBestItem.sln
```

Reference assemblies come from NuGet — the game itself is not required to
compile. If the `BANNERLORD_GAME_DIR` environment variable is set, the built
module is deployed into the game's `Modules` folder automatically.

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## License

[MIT License](LICENSE.txt)
