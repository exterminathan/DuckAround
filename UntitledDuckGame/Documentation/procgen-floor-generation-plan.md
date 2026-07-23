# Procedural Floor Generation — Block Assembly System (Milestone 1: Editor-Only)

## Context

DuckAround needs procedurally generated factory floors. The long-term goal is a **genetic algorithm** that evolves floor layouts, but the foundation comes first: a system that assembles **hand-authored block prefabs** (room chunks containing floor pieces, conveyor belts, AI waypoints, and props) into a complete floor, with **conveyor belts and waypoint graphs stitched across block boundaries**. The data model must be GA-ready: every block carries a cell-matrix representation so future fitness functions can evaluate layouts as pure array math without instantiating anything.

Nothing like this exists in the project today — no grid/tile/room concept, no seeded RNG, no runtime spawners beyond debug key-press ones. The closest pattern is `Assets/Editor/WaypointGraphGenerator.cs` (EditorWindow + prefab instantiation + SerializedObject wiring), which this system mimics.

## Decisions locked with the user

1. **Assembly layer first, GA later** (M3). Runtime generation later too (M2) — M1 is editor-only, but the generation core is **pure C# (no UnityEditor)** so both front-ends share it.
2. **Blocks** = hand-authored prefabs of 10u×10u floor pieces; footprint **2×2 or 3×3 pieces** (user undecided — support both families). **Uniform block size per generation** (picked in the window; no mixed-size packing).
3. **Cell matrix at 2u pitch** (belt grid spacing; 10u piece = 5×5 cells; 3×3-piece block = 15×15 cells). Belts sit anywhere on the 2u grid.
4. **Both belts and waypoints stitch across block boundaries.** Belts: merged into single `ConveyorPath`s at bake. Waypoints: reciprocal `neighbors` references added between border waypoints.
5. **Open belt ends allowed** (existing `exitForce` fling). The assembler **may flip a chain's direction** (reverse node order) for better continuity with neighbors.
6. **90° block rotations allowed** (rotate matrix/chains/sockets in data; rotate prefab instance in world).
7. **Block metadata auto-baked from the prefab** by an editor scan tool into a `BlockDefinition` ScriptableObject, with a validator.
8. **Generator places belt visual meshes** per-cell from final post-flip chain data (block prefabs contain only ConveyorPath + nodes as authoring scaffolding — "conveyor waypoints that link together", building on the existing ConveyorPath/ConveyorNode gizmo debug drawing).
9. **Worker patrol driving is out of scope** (workers idle until BTInputManager pushes destinations — unchanged). Optional: wire generated waypoints into `BTInputManager.waypoints[]`.
10. User explicitly wants: a proper **Tools-menu EditorWindow** panel and **extensive scene-viewport debug drawing** (gizmos/Handles) matching the project's gizmo-heavy style.

## Verified codebase facts the design depends on

- `ConveyorPath` ([ConveyorPath.cs](Assets/Scripts/Conveyors/ConveyorPath.cs)): private serialized `List<ConveyorNode> nodes`; **private `Rebuild()`** invoked only via `#if UNITY_EDITOR` OnEnable/OnValidate/OnDrawGizmos. Corner rule (line 63–65): node `i` with `turnDirection != 0` starts a corner to node `i+1`, legs must be equal X/Z (`isProper`) or it **silently degrades to straight**; arc center = `pA + nB.Forward * radius` — i.e. **a node's forward must be its outgoing travel direction** (verified geometrically). Setting `nodes` via `SerializedObject` fires `OnValidate` → `Rebuild()` in-editor.
- `ConveyorNode` (`turnDirection` 0/±1, `Forward` from transform); prefab `Assets/Prefabs/ConveyorNode.prefab`. `[ExecuteAlways]` with per-frame editor Update.
- `ConveyorObjectMover` rides items kinematically; assign `.path` (pattern in `Assets/Scripts/Debug/UnitSpawner.cs`); supports `loop`; flings via `exitForce` at open ends.
- `ConveyorManager` is **not a registry** — manually-assigned `MeshRenderer[] conveyorBeltRenderers` / `cornerBeltRenderers` whose material UVs it scrolls. Generator must append spawned renderers.
- `Waypoint` (`Assets/Scripts/AI/Waypoint Scripts/Waypoint.cs`): public serialized `List<Waypoint> neighbors`; BFS (`Pathfinding.FindPath`) is static/stateless over live neighbors — graph mutation is safe any time. Prefab `Assets/Prefabs/Waypoint.prefab` (Layer 6).
- Floor piece: `Assets/Prefabs/floor.prefab`, 10u×10u slab, Layer 13 Ground.
- Layers by index only: 3 Player, 6 Waypoint, 7 Prop, **12 "Interactable " (trailing space — `NameToLayer("Interactable")` returns -1!)**, 13 Ground, 15 Wall, 16 Worker, 17 Ragdoll.
- SO pattern to follow: `AlarmConfig` in `Assets/Scriptable Objects/AlarmLevelData.cs` (`[CreateAssetMenu]`).
- Style: tabs (w4), same-line braces, system-first usings, LF ([.editorconfig](.editorconfig)).

## Architecture

```
EDITOR (Assets/Editor/ProcGen/)          — baker, validator, windows, scene baker
RUNTIME DATA (Assets/Scripts/ProcGen/)   — BlockDefinition SO, FloorPlanDebugView
PURE CORE (Assets/Scripts/ProcGen/Core/) — no UnityEditor, no GameObjects, System.Random only
```

Core: `(seed, dims-in-blocks, block palette) → FloorPlan` — a complete instantiation-free floor description. M1 editor window consumes it; M2 runtime bootstrapper and M3 GA reuse it unchanged.

### Constants & conventions (`ProcGenConstants`)
- `CellSize = 2f`, `FloorPieceSize = 10f`, `CellsPerFloorPiece = 5`.
- Block prefab local origin = **min (SW) corner**; cell `(cx,cz)` center at local `(cx*2+1, y, cz*2+1)`; floor piece `(px,pz)` center at `(px*10+5, 0, pz*10+5)`. The baker detects off-lattice content and errors rather than guessing (blocks are new content; we define the convention).
- Row-major indexing: `index = cx + cz * width`.

### Data model

**`CellGrid`** (core): flat `byte[]`, one byte per 2u cell:
- bits 0–2 `CellContent`: Empty, Floor, Belt, BeltCorner, Waypoint, Prop, Wall
- bits 3–4 `Dir4` (N/E/S/W — belt travel direction leaving the cell)
- bit 5 corner handedness; bits 6–7 reserved (future GA tags)
- Ops: pack/unpack, `RotateGrid` (90° steps: `cell' = (h-1-cz, cx)`, Dir4 increments, handedness invariant), blit into floor-sized aggregate. 15×15 block = 225 bytes; whole floor a few KB — GA-cheap.

**`BlockDefinition : ScriptableObject`** (`[CreateAssetMenu("ProcGen/Block Definition")]`, assets in `Assets/Scriptable Objects/Blocks/`):
- `prefab`, `sizeInFloorPieces` (Vector2Int), `cells` (byte[], rotation 0), `beltChains`, `waypoints`, `bakeWarnings`, `contentHash` (staleness check vs prefab).
- `BeltChainDef`: ordered `List<BeltNodeDef>` (+ `beltY`); `BeltNodeDef` = `{ Vector2Int cell; sbyte turnDirection }`. Entry/exit **ports are derived**, not stored: endpoint node in a border cell whose final segment is perpendicular to that edge ⇒ port at (edge, lane).
- `WaypointDef`: exact `localPos`, quantized `cell`, in-block `neighborIndices`, `borderMask` (N/E/S/W), `childPath` (transform path in prefab — scene baker resolves live instances deterministically).
- Props/walls live only in the prefab; they appear in the matrix as rasterized `Prop`/`Wall` cells (from collider bounds) for GA occupancy reasoning.

**`BlockData`** (core): built once per session from a definition; precomputes all 4 rotations (grid, chains, waypoint cells/masks, per-edge port tables). The GA/assembler only ever touches this, never the SO.

**Rotation & flip mechanics:**
- Rotation in world: prefab instance gets `Euler(0, 90*rot, 0)` + offset so the rotated footprint covers the assigned slot (exact offset table in the scene baker).
- **Chain flip**: reverse node list AND remap corners — `new[j].turnDirection = -old[k-j-1].turnDirection`, last node = 0 (turnDirection lives on the segment-starting node; reversal swaps which node starts each corner and flips handedness). Node forwards are never stored — recomputed at spawn: node i forward = direction to node i+1 (last node: from predecessor). This satisfies the verified `pCenter = pA + nB.Forward * radius` corner math.
- Flipped visuals are automatic because meshes are placed from post-flip data (decision 8).

**`FloorPlan`** (core output): seed, dims, `List<BlockPlacement>{blockId, slot, rot}`, `List<MergedChain>{parts(placement,chainIdx,flipped), worldNodes, isLoop}`, `List<WaypointLink>{placementA,wpA,placementB,wpB}`, aggregate `CellGrid`, `StitchStats` (matched ports, open ends, links, waypoint-graph components via union-find), messages.

### Bake pipeline (prefab → BlockDefinition)

Authoring convention (documented in the baker window's help box): block prefab root contains `Floor/`, `Belts/Chain_N` (one **ConveyorPath per chain** with ordered ConveyorNode children — gives chain order for free and doubles as live authoring preview via existing gizmos), `Waypoints/`, `Props/`, `Walls/` (classified by layer index).

`BlockPrefabScanner` (editor, `PrefabUtility.LoadPrefabContents` + finally-unload):
1. Floor pieces (Layer 13) → footprint, verify rectangular 2×2/3×3, min corner at origin.
2. Per ConveyorPath child: read private `nodes` via `SerializedObject.FindProperty("nodes")`, quantize positions to cells (error if >0.05u off-lattice), copy turnDirection, rasterize chain into Belt/BeltCorner cells with Dir4.
3. Waypoints: pos, cell, neighbor indices (error if neighbor outside prefab), borderMask, childPath.
4. Rasterize prop/wall colliders; remaining covered cells = Floor. Compute contentHash.

`BlockValidator` checks (hard errors vs warnings):
- off-lattice nodes/pieces/props; non-rectangular footprint;
- **chain endpoint at a border must be a straight node with its final segment perpendicular to that edge** (hard error — guarantees cross-boundary joins are always straight, sidestepping `isProper` at seams);
- corner pairs with unequal legs or non-2u-multiple legs (would silently degrade);
- mixed Y within a chain; two chains on one cell; chain < 2 nodes;
- waypoint with no in-block neighbors (warn); no border waypoints on any edge (warn — unstitchable);
- stale contentHash (warn in generator window).

### Assembly core (`FloorAssembler.Assemble`, deterministic via `System.Random(seed)`)

- Floor dims specified **in blocks** (uniform size per generation → perfect grid, no packing/filler).
- Scanline over slots; candidates = every (block, rot 0–3); score against already-placed W/S neighbors: +w per belt-port lane match (direction resolved later — flips are free), +w per aligned waypoint-port pair (exact lane; nearest-within-2-lanes at half weight), −w per port facing a neighbor with no counterpart; floor-edge ports score 0 (open ends OK). Weighted-random pick with a floor probability + temperature (variety survives).
- **`ChainStitcher`**: collect chain endpoints on interior boundaries; endpoints meeting across a boundary at the same lane join; connected components → open paths or loops; orient each component (keep lowest-placementIdx part's direction, set `flipped` on others via walk), concatenate node lists (boundary endpoints 2u apart become an ordinary straight segment), apply flip remap; mark loops (`ConveyorObjectMover.loop`). Unmatched endpoints = open ends.
- **Waypoint stitching**: per interior boundary, pair facing border waypoints (same lane, else nearest within tolerance, greedy) → reciprocal `WaypointLink`s. Union-find over in-block edges + links → component count in `StitchStats` (future GA fitness term).
- Aggregate grid = blit of all rotated block grids. `IPlacementSource` seam: scored-random now, genome-driven in M3.

### Editor scene baking (`FloorPlanSceneBaker`)

Hierarchy in the open scene (all creation wrapped in Undo; **Clear** destroys container by name, like `WaypointGraphGenerator.ClearExistingWaypoints`):

```
GeneratedFloor_Container   ← FloorPlanDebugView (plan snapshot + gizmo toggles)
 ├─ Blocks/   Block_A (0,0) …   ← PrefabUtility.InstantiatePrefab
 ├─ Belts/    Path_0/Node_0…    ← fresh ConveyorNode instances + one ConveyorPath per MergedChain
 └─ BeltMeshes/                 ← straight/corner visual prefabs per belt cell
```

1. Instantiate blocks (pos/rot per rotation table); `SetActive(false)` each instance's `Belts/` authoring group (benign prefab-instance override).
2. **Spawn fresh ConveyorNodes from `MergedChain` data** (world pos from cell + beltY, forward = outgoing direction, turnDirection copied) — the FloorPlan is the single source of truth; no prefab-child reordering. Position all nodes first, then assign the path list (avoids `OnAnyNodeMoved` rebuild storms).
3. Per merged chain: GO + `ConveyorPath`, set private `nodes` via SerializedObject → `ApplyModifiedProperties()` (fires OnValidate → Rebuild in-editor). Also add `public void RebuildPath() => Rebuild();` to ConveyorPath now (one-line pre-authorized edit, M2 readiness).
4. Belt meshes: window takes `straightBeltPrefab`/`cornerBeltPrefab` fields (user authors these two wrappers around conveyor_belt.fbx/corner_belt.fbx once); place one per belt cell, yawed to travel Dir4; corner yaw from (entry Dir4, turn sign) lookup — **calibrate the mesh-axis-vs-travel-direction phase against one hand-placed belt in an existing scene during implementation**.
5. `ConveyorManager`: find scene instance, SerializedObject-**append** spawned renderers to the straight/corner arrays (warn if absent).
6. Waypoint links: resolve via placement instance + childPath; add to **both** `neighbors` lists (Undo + SetDirty). Existing Waypoint gizmos show them blue immediately.
7. Optional toggle: fill `BTInputManager.waypoints[]` with a % of generated waypoints (same SerializedObject pattern as `WaypointGraphGenerator.AssignToInputManager`).

### Editor windows

**`Tools ▸ Block Definition Baker`**: prefab field, target/auto-create BlockDefinition asset, Scan & Validate (report only), Bake (writes asset), preview toggle drawing the baked matrix/ports over the prefab-stage instance (`SceneView.duringSceneGui`).

**`Tools ▸ Floor Generator`** (mirrors WaypointGraphGenerator structure):
1. Palette — reorderable BlockDefinition list + weights; staleness badges + Rebake shortcut.
2. Floor — dims in blocks, block family (2×2/3×3), seed + Randomize, weights foldout (belt/waypoint/open-port, temperature).
3. Scene — belt mesh prefab fields, ConveyorManager/BTInputManager toggles, waypoint fill %.
4. Actions — **Generate Plan** (pure core only; snapshot to preview `FloorPlanDebugView` — instant seed-scrubbing with nothing instantiated), **Bake To Scene**, **Regenerate**, **Clear**.
5. Report — blocks placed, merged chains (parts/flips/loops), open ends, waypoint links, graph components, errors with ping-to-SceneView buttons.

**Debug draw layers** (all in `FloorPlanDebugView`, `#if UNITY_EDITOR` bodies, per-layer + master toggles; colors avoiding existing gizmo palette — ConveyorPath yellow/green/black, Waypoint blue/red, ConveyorNode red/white):

| Layer | Draw |
|---|---|
| Cell grid | 2u lattice, cells tinted by content (belt orange, corner amber, waypoint cyan, prop purple, wall dark, floor faint) |
| Block bounds | wire cube + label "Block_A r90 (2,3)" + rotation arrow |
| Ports | boundary-lane arrows: green matched, gray open end, magenta mismatched |
| Merged chains | polyline per chain (distinct hue per chain id), `ArrowHandleCap` direction ticks, "F" label on flipped parts, loop badge |
| Waypoint links | thick cyan cross-boundary lines (in-block edges already drawn by Waypoint.cs) |
| Validation | red spheres + labels at error positions |
| Aggregate heat | optional whole-floor matrix overlay |
| Header | seed/dims/stats label at container origin |

Snapshot is a compact serialized DTO on the component, so gizmos survive window close, domain reload, and scene save.

## File layout (all new unless marked)

**Assets/Scripts/ProcGen/Core/** — `ProcGenTypes.cs` (enums, defs, constants), `CellGrid.cs`, `BlockData.cs`, `FloorPlan.cs`, `FloorAssembler.cs`, `ChainStitcher.cs`
**Assets/Scripts/ProcGen/** — `BlockDefinition.cs` (SO + BlockData adapter), `FloorPlanDebugView.cs`
**Assets/Editor/ProcGen/** — `BlockPrefabScanner.cs`, `BlockValidator.cs`, `BlockDefinitionBaker.cs`, `FloorGeneratorWindow.cs`, `FloorPlanSceneBaker.cs`
**Modified existing (pre-authorized, one line):** `Assets/Scripts/Conveyors/ConveyorPath.cs` — add `public void RebuildPath() => Rebuild();`
**User-authored content (not by Claude — prefab/scene editing stays manual per CLAUDE.md):** 2+ sample block prefabs (`Assets/Prefabs/Blocks/`), `BeltStraight`/`BeltCorner` visual wrapper prefabs, baked BlockDefinition assets.

## Implementation order

1. `ProcGenTypes` + `CellGrid` (rotation math) — self-contained foundation.
2. `BlockDefinition` SO + `BlockData` adapter.
3. Scanner + Validator + Baker window → **checkpoint: bake & preview a hand-authored block**.
4. `FloorAssembler` + `ChainStitcher` + `FloorPlan` (pure, headless-testable).
5. `FloorPlanDebugView` gizmo layers.
6. `FloorGeneratorWindow` plan-only mode → **checkpoint: seed-scrub layouts visually with zero instantiation**.
7. `FloorPlanSceneBaker` + the one-line ConveyorPath edit.
8. End-to-end verification; tune weights/tolerances.

New files/classes require a **full recompile** (Hot Reload won't pick them up) — after each batch, `echo recompile > Library/hotReloadRemoteRequest.txt` and tell the user to focus Unity.

## Verification (end-to-end)

1. User authors `Block_Straight` (2×2: one W→E chain, 2 border waypoints) and `Block_Corner` (3×3: W→N chain with proper corner, 3 waypoints). Bake both; confirm matrices/ports preview and zero errors; nudge a node off-lattice to confirm the validator catches it.
2. In a gameplay scene (has ConveyorManager, BTInputManager, GlobalAlarm): Generate Plan at a fixed seed, 3×3 blocks — inspect gizmo layers (green matched ports, at least one "F" flipped chain across seeds). Bake To Scene: hierarchy correct, ConveyorPath yellow gizmo continuous across seams, manager arrays grown, cross-block waypoint lines blue/reciprocal.
3. Play: spawn an item with `ConveyorObjectMover.path` = a merged path (UnitSpawner pattern) — rides across ≥2 blocks, corners sweep (no silent straight-degrade), open end flings; belt UVs scroll with travel on flipped and unflipped parts. Push a worker a cross-block destination via the debug canvas — BFS crosses stitched links.
4. Regenerate with a new seed; Undo restores (container gone, manager arrays back).

## Risks / notes

- **Belt mesh direction phase** must be calibrated from an existing scene's hand-placed belt; if 180° yaw can't flip apparent scroll for some mesh, fallback is per-instance negative scroll (small authorized ConveyorManager change — flagged, not planned).
- `ConveyorManager` uses `rend.material` (per-renderer material instances; batching cost at scale) — keep as-is for M1; MaterialPropertyBlock refactor is future work.
- Corner `isProper` at seams neutralized by the border-endpoint-straightness hard validation (baker + assembler re-check).
- Many `[ExecuteAlways]` ConveyorNodes drag the SceneView at scale — mitigated by emitting nodes only at direction changes/ends, never per-cell.
- Stitching writes prefab-instance overrides (neighbors lists, deactivated Belts/ groups) — normal and undo-safe, but the user must not "Apply overrides" to block prefabs (help-box warning).
- Score weights and waypoint lane tolerance are initial guesses — exposed in the window for tuning.

## M2 outline — runtime generation (separate plan later)

`FloorBootstrapper` MonoBehaviour: same core; `Object.Instantiate` instead of PrefabUtility; uses `RebuildPath()` plus a new `SetNodes(List<ConveyorNode>)` public setter (second small authorized edit); direct `neighbors`/manager-array mutation (no serialization); verifies/spawns scene services (ConveyorManager, BTInputManager, GlobalAlarm + AlarmConfig); serialized palette on the component.

## M3 outline — genetic algorithm

Chromosome = fixed grid of `(blockId, rot)` slots (flips stay derived — stitcher resolves them deterministically; genome stays small and always-valid). Decode = `Assemble` with genome-driven `IPlacementSource`. Fitness from `aggregateGrid` + `StitchStats` only (belt continuity ratio, longest chain, single waypoint component bonus, content-mix targets) — pure array math, thousands of evals/sec. Evolution loop in an editor window (elitism, tournament, mutation = re-roll/rotate slot, crossover = rectangular patch swap), live best-plan preview via the same `FloorPlanDebugView`, "Bake winner" reuses `FloorPlanSceneBaker`.
