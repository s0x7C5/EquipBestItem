# Game-version compatibility

Policy: the mod targets the latest game version and stays compatible down to a
declared **support floor**. Current floor: **v1.3.5**. The release target (what
`csproj` compiles against by default) is `BannerlordRefsVersion` in
[Bannerlord.EquipBestItem.csproj](../Bannerlord.EquipBestItem/Bannerlord.EquipBestItem.csproj).

The core principle: **the codebase is anchored to the current version; older
versions are served by shims, never the other way around.** We do not compile
against old reference assemblies for release — that could silently bind to
signatures that no longer exist in the current game.

## The compile matrix

```powershell
.\tools\check-compat.ps1                # floor + release target + latest
.\tools\check-compat.ps1 -Versions 1.4.8.xxxxx   # a new game version appeared
```

The script builds the project against each listed
[Bannerlord.ReferenceAssemblies](https://www.nuget.org/packages/Bannerlord.ReferenceAssemblies)
version into `%TEMP%` (regular `bin`/`obj` are untouched) and lists real
compiler errors. Run it:

- before every release,
- whenever a new game version ships (add it to the default list in the script),
- after adding code that touches a new TaleWorlds API.

A clean matrix proves the *statically called* surface exists on every version.
It does **not** cover reflection, runtime behavior, or prefab/data differences —
see below.

## Categories of breaks and how to handle each

### 1. Called member missing / changed on some version

`MissingMethodException` at runtime. Note the granularity: the JIT compiles a
whole method at once, so a version-fragile call crashes when the *containing
method* is first entered, even if the fragile branch is never taken.

Handling: route the fragile call through `Compat/GameCompat.cs` — it resolves
the member once into a cached delegate and degrades the feature (or falls back
to a local port of the native logic) when the member is missing. Prefer
**feature probes over version comparisons** (`GetMethod("X") != null`, not
`version >= 1.4`) — probes keep working on future versions we have not seen
yet.

Shimmed today (all missing on 1.3.5):

- `CharacterHelper.CanUseItem` — native when present, local port of the 1.4.6
  logic otherwise (`EquippableFilter`).
- `EventManager.HoveredWidget` — null getter disables the Alt-reveal yielding
  to the native compare cycle (`EbiEquipButtonWidget`).
- `EventManager.DragHoveredWidget` — null getter disables drag highlights and
  insert lines; dropping itself is stock engine behavior
  (`EbiLinkDropZoneWidget`, `EbiPriorityListWidget`).

### 2. Changed signature of a virtual we override

This is the one category that **cannot be shimmed inside a single assembly**:
the override slot is bound at type load, so the whole type fails with
`TypeLoadException` on the version where the base signature differs (same
mechanics as the known Vector2-override ban).

Options, in order of preference:

1. Restructure so the override is not needed (different hook, per-frame check
   in `OnUpdate` — several widgets already work this way).
2. Move only the affected widget subclasses into tiny per-version satellite
   assemblies (`*.Compat.v135.dll` / `*.Compat.v14.dll`), and load exactly one
   from `SubModule` based on `ApplicationVersion`. Gauntlet resolves widget
   types by class name across loaded assemblies, so prefabs keep working
   unchanged. Only reach for this if restructuring is impossible.

Known instance (resolved): `Widget.OnMouseReleased()` gained a `bool
isFromInput` parameter after 1.3.5. The `EbiEquipButtonWidget` override was
replaced by guarding `ButtonWidget.HandleClick()` (same signature on every
version), which suppresses the click on all paths while the base release
handler keeps the button's internal press state consistent.

### 3. Reflection-accessed members

Invisible to the compiler and to the matrix. Current inventory (keep this list
in sync when adding reflection):

- `SPInventoryVM.RefreshInformationValues` (private method) — `InventoryGateway`
- `EventManager.MousePosition` (property, Vector2-safe boxed access) — `EbiPriorityListWidget`
- `EventManager.ReleaseDraggedWidget` (internal method) — `EbiPriorityListWidget`
- `Widget.AreaRect` field + its `TopLeft`/`BottomLeft`/`Y` subfields — `EbiPriorityListWidget`

All lookups are already null-tolerant. Rule: every new reflection lookup must
degrade gracefully (feature off + one `GameLog` line naming the missing member),
never throw. If the list grows, add a startup probe that resolves all of them
once and logs a single summary.

### 4. Third-party DLLs shipped by the game

The game ships its own `Newtonsoft.Json` (and friends); our packages are
compile-only. The pinned package version must match the **oldest** copy shipped
across the supported range, otherwise calls bind to overloads the old game does
not have. When lowering the floor, check the DLL versions in the old game's
`bin\Win64_Shipping_Client` first (1.4.6/1.4.7 ship 13.0.1; 1.3.5 — unverified).

### 5. Data and prefab differences

Native inventory prefab layout, sprite names, `str_*` text ids and native
widget behavior can differ even when signatures match; the matrix cannot see
this. UIExtenderEx insertions already anchor on unique names (`DropTag`), not
indices — keep that style. The only real check is a smoke test on an anchor
version (Steam beta branches let you install old game versions): open the
inventory, equip via button, open the filter popup, run one AI request.

Known instance (found by the 1.3.15 smoke test, resolved): prefab XML resolves
enum attribute values **by name at runtime**, and TaleWorlds swapped the names
of the vertical `LayoutMethod` members after v1.3 while keeping the behavior
of the numeric slots — so `StackLayout.LayoutMethod="VerticalTopToBottom"` in
XML stacked the popup bottom-up on 1.3.x. Enum constants compiled against the
current references are baked in by number and behave identically everywhere;
hence the rule: **vertical stacks use `EbiVerticalStackPanel` (sets the
direction from code), never the XML attribute.** Horizontal members did not
change. More generally, treat every enum-valued prefab attribute as
version-sensitive.

## Dependencies

Harmony, UIExtenderEx and MCM are BUTR-ecosystem projects built against game
v1.0.0 and installed by users as separate modules — they do not constrain our
floor within the 1.3.5+ range.

## Release checklist addition

1. `.\tools\check-compat.ps1` must be green for the floor and the latest game
   version (add newly released versions to the script's default list).
2. Workshop/Nexus tags list every game version the matrix (plus smoke test)
   actually covers.
