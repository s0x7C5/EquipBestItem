# Architecture

Equip Best Item v3 is a ground-up rework built around one idea:

> **Every way of asking for gear produces the same `ItemQuery`.**
> Manual weight sliders, built-in defaults and the AI interpreter are just
> different producers; the search and equip pipeline consumes queries without
> knowing where they came from.

```
                 ┌────────────────────┐
  slider popup ─►│                    │
  defaults     ─►│     ItemQuery      │──► BestItemFinder ──► InventoryGateway
  AI interpreter►│ (weights + limits) │    (filters+scorer)    (TransferCommand)
                 └────────────────────┘
```

## Layers

| Layer | Namespace | Depends on | Responsibility |
|---|---|---|---|
| Domain | `Domain.*` | TaleWorlds types only | Queries, filters, scorers, the finder. No UI, no IO. |
| Profiles | `Profiles.*` | Domain, Persistence | Per-character/per-set/per-slot weights with defaults. |
| AI | `Ai.*` | Domain, Settings | Free text → `InterpretedPlan` (list of slot + query). |
| Game adapter | `Inventory.*` | Domain, game VMs | The only code touching live inventory state and transfer commands. |
| UI | `UI.*` | everything above | UIExtenderEx mixin, view models, prefab patches. Thin. |
| Composition | `ModRuntime` | everything | The single static seam; builds the object graph once. |

### SOLID mapping

- **S** — each filter is one rule (`SlotFilter`, `WeaponMatchFilter`, …); each
  scorer is one strategy; `InventoryGateway` is the only class that knows how
  to issue a `TransferCommand`.
- **O** — new search rules are added by appending an `IItemFilter` to the list
  in `ModRuntime`; new scoring strategies implement `IItemScorer`; new request
  sources (e.g. a local model) implement `IRequestInterpreter`. Nothing else
  changes.
- **L** — all interfaces are behavioral contracts with no hidden preconditions.
- **I** — interfaces are single-method (`IItemFilter`, `IItemScorer`,
  `IRequestInterpreter`).
- **D** — the UI depends on abstractions (`IRequestInterpreter`), wiring
  happens only in the composition root. Two documented static exceptions:
  `ModRuntime` (UIExtenderEx creates the mixin reflectively) and the
  `MainThread` queue (drained from the engine's per-frame tick, which only
  exists as a `SubModule` override).

## Search performance

- One pass, no LINQ, no allocations per candidate (`BestItemFinder`).
- Stat vectors (`float[20]`, indexed by `ItemParam`) are extracted once per
  `(item, modifier)` pair and cached in `WeightedItemScorer`; the cache is
  invalidated on inventory refresh events, not per search.
- Scoring is a dot product over non-zero weights, normalized by `sum(|w|)`.
  Negative weights penalize (e.g. `Weight: -1` prefers light items).

### Search method selection

The game's built-in `ItemObject.Effectiveness` is a blunt aggregate — the
whole reason this mod exists is weight-based search. Both are available and
the player chooses in `settings.json` (`searchMethod`):

- `"weights"` (default) — score by the per-slot weight vectors; every slot
  ships with balanced defaults (all relevant params at 1.0, as in v2), and a
  pinned weapon class swaps in that class's own parameter set (a bow scores
  missile damage/speed/accuracy, a shield hit points/armor, …). A slot whose
  weights the player zeroes out is **excluded** from searching — this
  replaces the v2 per-slot "lock". In the stored profile the two states are
  distinct: `null` weights mean "never customized, use the slot/class
  defaults", an empty dictionary means "locked".
- `"effectiveness"` — the game's score. Weight windows still matter for the
  weapon-class pin. Weights explicitly spelled out by an AI directive
  (`HasExplicitWeights`) override this setting, because the player asked for
  them in that very request; AI directives without weights follow the setting.

## The AI interpreter

`LlmRequestInterpreter` posts the player's text plus a compact system prompt to
either the Anthropic Messages API or any OpenAI-compatible endpoint
(configurable in `settings.json`, key comes from the `EBI_AI_API_KEY`
environment variable by default). The model must answer with a single JSON
object; `LlmPlanParser` validates it, expands group slots (`AllArmor`,
`AllWeapons`, `AllMount`, `All`) and clamps weights.

The prompt is tuned for small local models: temperature 0, few-shot examples
(including deliberately tricky ones — contradictory requests, one/two-handed
grip confusion), a closed weapon-class list, and explicit rules separating
`Weight` (mass) from the `*Armor` protection params.

Requests work in any language the game ships: when the game does not run in
English, the prompt gains a glossary mapping the game's own localized terms
(params, weapon classes, slots) to the JSON identifiers — `PromptGlossary`
builds it once from the same `TextObject`s the weight popup uses, so no
hand-maintained word lists exist. It rides in `InterpretationContext`, which
is captured on the main thread.

Two backend quirks are detected from the first response and remembered for
the session, so every later request is a single HTTP call:

- a 400 mentioning `response_format` → retried and subsequently sent without
  the JSON response format;
- `usage.prompt_tokens` too small to contain the system prompt (some GGUF
  chat templates silently drop the system role) → retried and subsequently
  sent with the whole prompt merged into the user message.

**Applying a plan edits the slot filters.** The interpreted directives are
written into the per-slot profiles — the very filters the manual sliders
edit (weights, weapon class, and the culture/max-weight hard constraints) —
and the previews are recomputed; the status line reports each slot's new
settings. The AI never equips items directly, so the player always sees
(and can tweak) what a request produced before committing to it.

**Plans can target other heroes.** The optional plan-level `target` field
("current", "others" = everyone but the main hero, "all", or an exact hero
name from the party list included in the prompt) makes the same directives
apply to several heroes' profiles at once; non-current heroes get their
battle set edited. Since profiles are per-hero anyway, the existing
"equip all heroes" batch then dresses the whole party by those filters.

Threading contract: interpretation runs on a background task and only writes
view-model text properties; **game state is mutated exclusively on the main
thread**. The finished plan is handed over via `MainThread.Post` — a queue
drained once per frame in `SubModule.OnApplicationTick` — and applied there
automatically, no user action required.

## UI without generated XML

The old version built Gauntlet XML in ~20 C# classes. v3 uses only static,
parameterized prefabs:

- `GUI/PrefabExtensions/EbiSlot_*.xml` — one snippet per equipped slot (the
  banner slot is skipped), inserted at the native slot instantiation sites in
  `Inventory.xml` (matched by their unique `Parameter.DropTag`). Eleven files
  that differ only in the bound per-slot view model name: bindings cannot be
  parameterized, and each button must feed **its** found item to the native
  comparison tooltip. The button (legacy UX): visible only when a better item
  exists, hover previews the found item via the game's own tooltip
  (`EbiFoundItemTooltipWidget : InventoryItemButtonWidget`), left click equips
  the previewed item, right click opens the search settings, holding Alt
  reveals hidden buttons so settings stay reachable.
- Best items are recomputed **synchronously** on inventory change events
  (refresh, character switch, equipment set switch, weights popup close). The
  legacy version did this on a background task because its scoring was slow;
  with cached stat vectors a full 11-slot recompute is a few milliseconds, so
  the async complexity and its UI races are gone.
- `GUI/Prefabs/EbiWeightsPopup.xml` renders the weight sliders with a single
  `<ItemTemplate>` bound to `MBBindingList<ParamRowVM>` — rows come from data,
  not from code. The popup also edits the slot's hard constraints: a culture
  selector (the six major cultures or "any") and a max-item-weight slider
  (0 = off) — the same fields the AI writes.
- `GUI/Prefabs/EbiInventoryPanel.xml` places two toolbox plaques at the top
  of the center panel (legacy placement), built exactly like the game's
  `InventoryEquippedItemControls`: a fixed 146×69 background holding 35×35
  `toolbox_icon_bed` sockets. Left plaque: left-panel search lock +
  equip-all-heroes; right plaque: equip-current + right-panel lock + the AI
  button. Each plaque slides one socket-width past the panel edge to hide
  unused background (the right one only when no AI backend is configured,
  via a `PositionXOffset` binding). The AI status line sits at the bottom
  of the center panel.
- The AI request text is typed into the game's **native text inquiry**
  (`InformationManager.ShowTextInquiry`) opened by the AI socket button —
  proper keyboard focus with no inventory hotkeys firing mid-typing, and
  the previous request prefilled. No custom `EditableTextWidget` exists.

C# patch classes (`UI/Patches/*`) only declare *where* to insert and which
file to insert — they contain no markup.

## Dependencies

The module depends on the standalone `Bannerlord.Harmony` and
`Bannerlord.UIExtenderEx` mods (declared in `SubModule.xml`
`<DependedModules>` with `LoadBeforeThis` ordering); players install them
separately, as with most UI mods. The NuGet packages are compile-only
references (`IncludeAssets="compile"`), so no third-party DLL ships in the
module's `bin`.

Settings live in
`Documents/Mount and Blade II Bannerlord/Configs/EquipBestItem/settings.json`,
profiles in `profiles.json` next to it. MCM is an **optional** dependency
(`Bannerlord.MBOptionScreen`, `Optional="true"`): when the module is present,
`McmSettings` — a facade whose plain-typed properties delegate straight to
`ModSettings` and re-save the JSON — is registered from
`OnBeforeInitialModuleScreenSetAsRoot`, guarded by a loaded-modules check and
a non-inlined method so the MCM types are never touched without the module.
settings.json stays the single source of truth either way.

## Bulk equip

"Equip all" (current character or every party hero) plans every transfer up
front and executes them as **one** batch — per-command transfers make the
game rebuild the trade UI for each item, which visibly freezes large
inventories. While planning, a claims map keeps two slots or heroes from
taking the same physical item, and items displaced by earlier steps join the
candidate pool for later ones: the sword replaced on the first hero may
still be the best option for the last one.

## Known limitations / roadmap

- The banner slot (`Equipment_4`) is not searchable (banners have no scorable
  stats); its buttons report "no better item".
- The first AI request of a session may take up to three HTTP calls while
  backend quirks are being detected; later requests are a single call.
