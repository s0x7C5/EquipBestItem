# Equip Best Item

A Mount & Blade II: Bannerlord mod that equips your characters with the best
items in one click — and, if you want, understands plain-language requests
in any language, like *"dress me in the lightest imperial armor"*, via an LLM.

## Features

- **Equip best** button on every equipment slot; **equip-all** for the current
  character or for every party hero — planned up front and executed as one
  transfer batch, so items freed from one hero stay available to the next.
- Per-slot **search weights** (per character, per battle/civilian/stealth set)
  with negative weights supported (`Weight: -1` = prefer light gear). Zero out
  a slot's weights (the Lock button) to exclude it from searching. Weapon
  slots can pin a **weapon class** (bow, one-handed axe, shield, …) — each
  class brings its own sensible default weights.
- Choice of search method in `settings.json`: parameter weights (default) or
  the game's built-in Effectiveness score.
- **AI assistant**: the AI button next to the equip buttons opens the game's
  native text-input dialog — describe what you want in your own words and
  the mod rewrites the affected slot filters, recomputes the previews and
  reports exactly what changed per slot. Works with the Anthropic API or any
  OpenAI-compatible endpoint, including fully local ones.
- Fast: single-pass search with cached item stat vectors.
- Self-contained: no dependency on other mods (UIExtenderEx and Harmony ship
  inside the module).

## Installation

1. Copy `Bannerlord.EquipBestItem` into your game's `Modules` folder
   (or download from [NexusMods](https://www.nexusmods.com/mountandblade2bannerlord/mods/369)).
2. Enable it in the launcher.

### Enabling the AI assistant (optional)

The AI button appears in the inventory as soon as a backend is available.
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
