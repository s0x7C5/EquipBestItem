# Changelog

## 3.2.0 — 2026-07-14

- **"Why this?"** button in the slot filter: the mod explains its pick in the
  message log — which stats decided, by how much, and what to tweak if you
  disagree. Fully deterministic, no AI required.
- **The AI assistant answers questions** — "why this helmet?", "why not the
  Nord Great Sword in slot 1?" — grounded in that same deterministic
  breakdown, so it never invents numbers. Ask about any item by name and it
  reports the exact reason: loses on stats, wrong culture, over the weight
  cap, wrong weapon class for the slot, and so on.
- The AI's replies now go to the message log instead of a status line.
- **New AI input dialog**: a wide in-inventory prompt with autofocus in place
  of the cramped native text popup; typing in it never triggers inventory
  hotkeys.
- **Clear** button in the slot filter: resets only the weights (or the
  priority order) to the slot's defaults, keeping weapon class, culture and
  the weight limit. Actions now sit in two rows; "Make default" is renamed
  to "Set as default".
- The filter popup is denser, so the tallest (weapon-slot) variant fits a
  1920×1080 screen.
- **Custom icons** for equip-current-character and the panel locks; the locks
  are brighter and step up further on hover. The plaques now slide in with
  the inventory side panels instead of popping into place.

## 3.1.0 — 2026-07-12

Two new mechanics ship as **experimental** — they work, but the details may
still change based on feedback.

- **Priority-based search** (experimental): a third search method — items are
  ranked by a per-slot stat order instead of weights: the top stat decides,
  ties fall through to the next one. In the slot filter the stats become
  draggable chips: drop between rows to reorder, drop onto a row to link
  stats as equal-rank (equals are judged by their combined value); an
  insertion line and a row glow preview exactly what a drop will do. Orders
  follow Default / Make default / Lock like weights do.
- **Reworked weighted scoring** (experimental): a stat now scores as its
  percentile within the item's class across the whole item catalog (modded
  items included) with diminishing returns — balanced items beat
  one-huge-stat ones, and no hand-tuned scales are involved. A swap is only
  suggested when the item is clearly better: no downside on any weighted
  stat, or a noticeably higher score — no more sidegrade churn.
- The search method is now a single dropdown in MCM, with a hint explaining
  what each method gives you.

## 3.0.2 — 2026-07-12

- Fixed item ranking: every parameter is now scored on a common scale, so a
  single large stat (a mount's or shield's hit points) no longer drowns out the
  rest. Better mounts and shields are picked correctly instead of just the
  highest-HP one.
- Bows are split into **Short Bow** and **Long Bow**: the mod no longer replaces
  a horseback-usable short bow with a long bow that cannot be fired mounted, and
  each can be pinned separately per weapon slot.
- Shields, bows and crossbows now tune **Swing Speed** — the "Speed" the game
  shows on them — instead of a hidden thrust-speed value. Shield defaults match
  the game's tooltip (hit points and swing speed); thrust speed and length stay
  available as optional sliders.
- Marked compatible with Bannerlord v1.4.7.

## 3.0.1 — 2026-07-12

- Fixed: the character model in the inventory now updates immediately when
  the mod equips an item (single slot and equip-all alike).

## 3.0.0 — 2026-07-11

Complete ground-up rework for Bannerlord v1.4.6.

### Search & filters
- Single-pass best-item search with cached stat vectors — previews recompute
  instantly on every inventory change, no background-update races.
- Per-slot parameter weights (−1…+1) for every hero and equipment set
  (battle / civilian / stealth); labels show each parameter's signed
  influence share in percent.
- Weapon class pinning per weapon slot; every class brings its own default
  weights (bow scores missile stats, shield scores hit points/armor, …).
- Hard constraints per slot: culture restriction and a max item weight cap.
- Player-editable defaults: **Make default** saves the current filter as the
  default for every hero without their own settings; a marker in the popup
  shows when a hero follows the defaults. **Lock** excludes a slot from
  searching.
- Two search methods: parameter weights (default) or the game's built-in
  Effectiveness score.

### UI
- A small button on every equipment slot previews the found item through the
  game's native comparison tooltip; left click equips exactly the previewed
  item, right click opens the slot's filter, holding Alt reveals hidden
  buttons.
- Action plaques at the top of the center panel (legacy layout): equip
  current hero, equip all heroes, and per-panel search locks (the merchant
  panel is locked by default so nothing is bought silently).
- Reworked weights popup: centered rows, aligned labels and values, culture
  selector, weight-limit slider, Default / Make default / Lock buttons.
- Configurable slot button color.

### Party
- **Equip all heroes** plans every transfer up front and executes one batch:
  no UI freezes on big inventories, no two heroes claiming the same item,
  and gear displaced from one hero stays available to the next.

### AI assistant (optional)
- Describe the gear you want in plain language, in any language — the AI
  rewrites the slot filters (it never equips by itself), previews recompute
  and the status line reports what changed per slot.
- Requests can target the current hero, a named companion, everyone, or
  everyone except the main hero.
- Works with any OpenAI-compatible endpoint (LM Studio, Ollama, Player2,
  OpenRouter, …) or the Anthropic API. The endpoint is explicit; the MCM
  **connection test** verifies it and fills the model in automatically.
- Prompt tuned for small local models, with a game-language glossary so
  requests work in every language the game ships; backend quirks (rejected
  JSON response format, dropped system role) are detected and worked around.

### Settings, localization, dependencies
- Optional MCM settings page (search method, slot button color, panel
  toggles, the whole AI connection); the same settings always live in
  `Documents/Mount and Blade II Bannerlord/Configs/EquipBestItem/settings.json`.
- Translated into every language the game supports.
- Framework mods are installed separately: Harmony and UIExtenderEx are
  required, MCM is optional; no third-party DLLs ship inside the module.
- The mod stores nothing in savegames — safe to add or remove mid-campaign.
