# Equip Best Item

A Mount & Blade II: Bannerlord mod that equips your characters with the best
items in one click.

## Features

- **Equip best** button on every equipment slot; **equip-all** for the current
  character or for every party hero — planned up front and executed as one
  transfer batch, so items freed from one hero stay available to the next.
- Per-slot **search filters** (per character, per battle/civilian/stealth set).
  A filter holds your stat preferences, a pinned **weapon class** (short bow,
  long bow, one-handed axe, large shield, …) with its own sensible defaults, a
  **culture** restriction and a **weight limit**. **Set as default** saves the
  current filter as the default for every hero you have not customized;
  **Clear** resets only the stat preferences (class, culture and weight limit
  stay); **Lock** excludes the slot from searching.
- Three ways to score items, chosen in the settings:
  - **Parameter weights** (default) — every stat is scored as its percentile
    within the item's class across the whole catalog (modded items included)
    with diminishing returns, so one inflated stat cannot outweigh the rest.
    A swap is only suggested when the item is genuinely better. Weights run
    −1…+1 and show each parameter's signed share of the total.
  - **Stat priority** — a strict order: the top stat decides, ties fall
    through to the next. In the slot filter the stats become draggable chips —
    drop between rows to reorder, or onto a row to link two stats as equal rank.
  - **Game Effectiveness** — the game's own single quality score, nothing to
    set up.
- **"Why this?"** button in a slot's filter explains the pick in the message
  log — which stats decided it, where the item falls short, and what to tweak.
  Computed by the mod itself, no AI involved.
- Configurable — search method, slot button color, panel toggles — via
  [MCM](https://www.nexusmods.com/mountandblade2bannerlord/mods/612) when
  installed, or `settings.json` otherwise.
- Fast: single-pass search with cached item stat vectors.
- Extra, entirely optional: an AI button that fills in the slot filters from a
  plain-language request and answers questions about the mod's picks (see
  below). Everything above works without it.

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
Press it, describe the gear you want in plain language, and the reply lands
in the message log; you can also ask it questions ("why this helmet?", "why
not the Nord Great Sword in slot 1?") and it answers from the mod's real
numbers. Three ways to connect, easiest first:

**Local model.** Run [Ollama](https://ollama.com) (`ollama pull qwen3:4b`),
LM Studio or Player2 with its server enabled, then in MCM → Equip Best Item
→ AI assistant set **Endpoint** to the server address (e.g.
`http://localhost:1234` — the chat-completions path is appended
automatically) and press **Connection test**: it verifies the server and
fills the model in for you. No keys, nothing leaves your PC. Without MCM,
set `ai.endpoint` in `settings.json` instead.

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
strings. Only the mod's own messages (button hints, "Why this?" explanations, AI
replies, MCM option names) are translated, and they live in
`Bannerlord.EquipBestItem/ModuleData/Languages/<CODE>/std_module_strings_xml.xml`.

Each language lives in its own subfolder under
`Bannerlord.EquipBestItem/ModuleData/Languages/<CODE>/`, holding the strings
file plus a `language_data.xml` that registers it with the game. To fix or
add one:

1. Copy an existing language folder (or the English base
   `Languages/std_module_strings_xml.xml` and a `language_data.xml`) into a
   new subfolder — the folder name is conventional.
2. In `language_data.xml`, set the `id` to the game's name for the language
   **exactly as the game spells it** — e.g. `Deutsch`, `Türkçe`, `简体中文`
   (see `Modules/Native/ModuleData/Languages` for the list) — and point
   `xml_path` at your strings file. Without this file the game never loads
   the strings.
3. Translate the `text` attributes. Never change the `id` attributes, and
   keep placeholders like `{COUNT}` intact.
4. Save as UTF-8. Picked up on the next game start; any missing string falls
   back to English, so partial translations are safe. (The "Why this?"
   explanation lines are the mod's longest strings; all thirteen shipped
   languages carry them.)

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
