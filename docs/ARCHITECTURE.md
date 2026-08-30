# Architecture

Equip Best Item v3 is a ground-up rework built around one idea:

> **Every way of asking for gear produces the same `ItemQuery`.**
> Manual weight sliders, built-in defaults and the AI interpreter are just
> different producers; the search and equip pipeline consumes queries without
> knowing where they came from.

```
                 ┌────────────────────────┐
  slider popup ─►│                        │
  priority chips►│        ItemQuery       │──► BestItemFinder ──► InventoryGateway
  defaults     ─►│ (weights / priorities  │    (filters + one of    (TransferCommand)
  AI interpreter►│      + constraints)    │     three strategies)
                 └────────────────────────┘
```

The same query and pipeline also drive the **"Why this?"** explainer, which
replays the scoring to describe a pick rather than make one.

## Layers

| Layer | Namespace | Depends on | Responsibility |
|---|---|---|---|
| Domain | `Domain.*` | TaleWorlds types only | Queries, filters, scorers/comparer, the finder, the explainer. No UI, no IO. |
| Profiles | `Profiles.*` | Domain, Persistence | Per-character/per-set/per-slot filters with defaults. |
| AI | `Ai.*` | Domain, Settings | Free text → `InterpretedPlan` (directives to apply, or an answer to a question). |
| Game adapter | `Inventory.*` | Domain, game VMs | The only code touching live inventory state and transfer commands. |
| UI | `UI.*` | everything above | UIExtenderEx mixin, view models, prefab patches, explanation formatting, runtime sprite loading. Thin. |
| Composition | `ModRuntime` | everything | The single static seam; builds the object graph once. |

## Search performance

- One pass, no LINQ, no allocations per candidate (`BestItemFinder`).
- Stat vectors (`float[20]`, indexed by `ItemParam`) are extracted once per
  `(item, modifier)` pair and cached in the shared `ItemStatCache`; the cache
  is invalidated on inventory refresh events, not per search.
- `ItemStatPercentiles` holds, per stat and per item class, the distribution
  of that stat over the whole `MBObjectManager` catalog (modded items
  included); it rebuilds only when the catalog size changes (e.g. crafting).

### Search method selection

The player chooses among **three** methods; MCM exposes them as a single
dropdown (backed by `settings.json` `searchMethod`), each with a hint
explaining the trade-off:

- `"weights"` (default) — each stat scores as its **percentile within the
  item's class** across the whole catalog, passed through `sqrt` for
  diminishing returns, and the score is `Σ(w·√percentile) / Σ|w|`
  (`WeightedItemScorer`). This keeps balanced items ahead of one-huge-stat
  outliers and needs no hand-tuned magnitudes. Every slot ships with balanced
  defaults, and a pinned weapon class swaps in that class's own parameter set
  (a bow scores missile damage/speed/accuracy, a shield hit points/swing
  speed, …). A swap is only suggested when the candidate clears an **upgrade
  guard** (`IItemScorer.BeatsCurrent`): a clearly higher score, or no downside
  on any weighted stat — no more sidegrade churn. `ParamScales` survives only
  as the fallback when a class has too few samples for a real distribution.
- `"priority"` — a strict lexicographic order instead of a blended score
  (`PriorityItemComparer`): the top-ranked stat decides, ties fall through to
  the next. Ranks are **groups** of equal-rank stats (`List<List<ItemParam>>`,
  serialized as `"HitPoints+Speed"` in the profile); a group is compared by
  its `ParamScales`-normalized sum. The slot filter edits the order by
  drag-and-drop chips.
- `"effectiveness"` — the game's built-in `ItemObject.Effectiveness`, a blunt
  single aggregate with nothing to configure.

Cross-cutting behavior:

- The weapon-class pin and the culture / weight-limit constraints are
  **filters**, so they apply in every method (a common early confusion —
  they are not tied to weights).
- Defaults are **player-editable**: "Set as default" in the filter popup
  stores the slot's current filter beside the profiles, and every hero
  without an override follows it instantly — the `null`-means-default
  semantics make this free, no per-hero migration (which the legacy version
  needed, materializing values for every character). "Clear" resets only the
  stat preferences; "Lock" stores empty preferences to exclude the slot. In
  the stored profile the states stay distinct: `null` = "use the slot/class
  defaults", empty = "locked".
- The weight labels in the popup show each parameter's **signed share** of the
  total (|w| / Σ|w|) rather than the raw number — the scorer normalizes by
  that sum, so only shares affect the result.
- Weights explicitly spelled out by an AI directive (`HasExplicitWeights`)
  switch that slot to weighted scoring regardless of the setting, because the
  player asked for them in that very request; directives without weights
  follow the setting.

## The "Why this?" explainer

`ItemExplainer` (Domain) answers *why the search made a given pick*, with no
LLM involved — it replays the same scoring and reports the result as pure
data (`SearchExplanation`), which `UI/ExplanationFormatter` renders into the
message log.

- For weighted scoring it uses `WeightedItemScorer.Breakdown`, whose per-stat
  contributions sum to `Score` **exactly** (the same `w·√percentile / Σ|w|`
  terms), so the explanation can never disagree with the ranking.
- For priority it uses `PriorityItemComparer.Explain`, which reports the
  deciding rank and the ties above it.
- Effectiveness is a black box, so the explainer says so honestly.
- It also answers **"why not this named item?"**: given an item name it finds
  the candidate and, if the item was excluded, re-runs the filter list to name
  the exact reason (wrong class, wrong culture, over the weight limit, …);
  otherwise it explains the stat-by-stat loss to the winner. The filter list
  is the very one the finder uses, injected into the explainer so the two can
  never drift.

The split between pure-data `SearchExplanation` and the UI formatter is what
lets the AI narrate the same facts (below) without recomputing anything.

## The AI interpreter

`LlmRequestInterpreter` posts the player's text plus a compact system prompt to
either the Anthropic Messages API or any OpenAI-compatible endpoint
(configurable in `settings.json` or MCM, key comes from the `EBI_AI_API_KEY`
environment variable by default). The model must answer with a single JSON
object; `LlmPlanParser` validates it, expands group slots (`AllArmor`,
`AllWeapons`, `AllMount`, `All`), clamps weights and reads an optional stat
`priorities` order (for the priority method, `"A+B"` = equal rank).

A request is classified as an **edit** or a **question**. An edit yields
directives; a question yields a plain-language `answer` and no directives.
The prompt is fed the explainer's deterministic facts for the current hero's
slots (`InterpretationContext.SlotExplanations`, collected on the main
thread), and the model is instructed to answer questions **only** from those
facts — it narrates, it does not compute numbers. For "why not <named item>"
the mod resolves the item and rejection reason itself (via the explainer) and
hands the result to the model to phrase.

The backend is always configured **explicitly** — there is no automatic
discovery (an earlier startup probe proved unreliable). A bare server
address gets the standard chat-completions path appended, and any explicit
endpoint (local or LAN) works without a key: servers that need one reject
the request themselves. `BackendConnectionTest`, wired to the MCM
"connection test" button, verifies the endpoint by listing the server's
models and fills the model setting in when it is empty (skipping embedding
models).

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
written into the per-slot profiles — the very filters the manual controls
edit (weights or a priority order, weapon class, and the culture/max-weight
hard constraints) — and the previews are recomputed; the reply (each slot's
new settings, or the answer to a question) goes to the game **message log**.
The AI never equips items directly, so the player always sees (and can tweak)
what a request produced before committing to it.

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
- `GUI/Prefabs/EbiWeightsPopup.xml` adapts to the search method. In weighted
  mode it renders sliders from a single `<ItemTemplate>` bound to
  `MBBindingList<ParamRowVM>`; in priority mode the same rows become
  drag-and-drop chips; in effectiveness mode both are hidden. It always edits
  the slot's hard constraints — a culture selector (the six major cultures or
  "any") and a max-item-weight slider (0 = off), the same fields the AI writes
  — and carries the **"Why this?"** button plus two rows of actions
  (Default / Set as default, Clear / Lock).
- Priority chips are custom widgets, because Gauntlet's drag events do not
  reach plain widgets and its drop index is miscomputed at a UI scale ≠ 1:
  `EbiPriorityListWidget` computes its own insertion index from `AreaRect` and
  overrides `OnDrop`; `EbiLinkDropZoneWidget` lights its glow per frame off
  `EventManager.DraggedWidget`. Both avoid any engine member with a
  `System.Numerics.Vector2` in its signature — the mod's `Vector2` (from the
  NuGet vectors package, needed for TW `Color`) is a different type identity
  than the game's and throws at runtime.
- `GUI/Prefabs/EbiInventoryPanel.xml` places two toolbox plaques at the top
  of the center panel (legacy placement), built like the game's
  `InventoryEquippedItemControls`: a fixed 146×69 background holding 35×35
  `toolbox_icon_bed` sockets. Left plaque: left-panel search lock +
  equip-all-heroes; right plaque: equip-current + right-panel lock + the AI
  button. Each plaque slides one socket-width past the panel edge to hide
  unused background (the right one only when no AI backend is configured,
  via a `PositionXOffset` binding), and both ride a `VisualDefinition` that
  eases them in from the sides with the same curve as the native side panels.
  The mod's own icons (equip-current, open/closed locks) come from custom
  sprites; the AI assistant's replies go to the game message log, not a panel
  line.
- The AI request is typed into the mod's **own dialog**, a wide
  `EbiPromptInputWidget : EditableTextWidget` shown inside the inventory (the
  native text inquiry fit only a few words). It autofocuses on open via
  `EventManager.FocusedWidget`; a focused text widget in the layer also
  silences the inventory hotkeys, matching the native search field.

C# patch classes (`UI/Patches/*`) only declare *where* to insert and which
file to insert — they contain no markup.

### Runtime sprites

The engine resolves sprite-sheet textures only from its compiled asset
registry (`.tpac` + `RuntimeDataCache`), which the official asset packer would
have to rebuild. To keep custom icons a plain file edit, the sheet PNG is
baked from the loose parts at the coordinates in `EquipBestItemSpriteData.xml`
and `EbiSpriteSheetLoader` swaps it into the loaded sprite category at
runtime — asserted idempotently on inventory open. `GUI/SpriteParts/` holds
the per-icon sources (bake input, never read by the game); `GUI/SpriteSheets/`
holds the baked sheet that ships.

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
