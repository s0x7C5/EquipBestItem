# Equip Best Item

A Mount & Blade II: Bannerlord mod that equips your characters with the best
items in one click — and, if you want, understands plain-language requests
like *"одень меня в самую лёгкую броню империи"* via an LLM.

## Features

- **Equip best** button on every equipment slot; **equip-all** for the current
  character.
- Per-slot **search weights** (per character, per battle/civilian/stealth set)
  with negative weights supported (`Weight: -1` = prefer light gear).
- **AI request box**: free text → structured search plan → preview → apply.
  Works with the Anthropic API or any OpenAI-compatible endpoint.
- Fast: single-pass search with cached item stat vectors.
- Self-contained: no dependency on other mods (UIExtenderEx and Harmony ship
  inside the module).

## Installation

1. Copy `Bannerlord.EquipBestItem` into your game's `Modules` folder
   (or download from [NexusMods](https://www.nexusmods.com/mountandblade2bannerlord/mods/369)).
2. Enable it in the launcher.

### Enabling the AI assistant (optional)

1. Set an environment variable `EBI_AI_API_KEY` with your API key.
2. Optionally edit
   `Documents/Mount and Blade II Bannerlord/Configs/EquipBestItem/settings.json`
   (provider `anthropic` or `openai`, endpoint, model).
3. The request box appears in the inventory when a key is configured.

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
