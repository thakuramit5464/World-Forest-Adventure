# Implementation Tasks — Trails of Greenhaven

Mark tasks complete only after implementation and appropriate verification.

Legend: `[x]` done and verified · `[~]` implemented and compiles, Play-mode/device verification pending · `[ ]` not started · *(deferred)* postponed by decision.

> **2026-10-11:** The project owner's Phase 1 brief combines the movement slice with a starter environment and an interaction foundation (parts of Phases 2–3 below). Build target is desktop for now; Android comes later. See `plan.md` §9 and `phase1-validation.md`.

## Phase 0 — Project inspection and baseline
- [x] Record Unity version and installed packages. — 6000.6.5f1, URP 17.6.0, Input System 1.20.0, uGUI 2.6.0, Test Framework 1.8.0 (see `plan.md` §9).
- [~] Confirm URP project and Android build support. — URP confirmed (Mobile/PC RP assets). Android Build Support module **not installed**; deferred by owner decision.
- [x] Inspect current scenes, scripts, input settings, and assets. — Template only: `SampleScene`, URP TutorialInfo, template input actions; active input handler = Input System only.
- [ ] Choose one representative low-end Android test device.
- [x] Create project folder structure and `ASSET_CREDITS.md`. — `Assets/_Game/...` per `plan.md` §3.
- [ ] Make a baseline Android build and record any existing failures. *(deferred until Android module is installed)*

**Exit criteria:** project state is understood, the baseline build status is documented, and asset licenses are tracked.

## Phase 1 — Player movement slice
- [~] Import one rigged humanoid character. — Kenney Animated Characters 1 (CC0), generic rig, custom clean skin. Facing and scale need checking in Play mode.
- [~] Configure Animator Controller and required animation parameters. — Character rig switched to **Humanoid**; `Player.controller` uses Universal Animation Library clips: idle/walk/jog/sprint blend on MoveSpeed, jump start/loop/land, and an upper-body Interact gesture layer. Needs a Play-mode check.
- [~] Implement input abstraction. — `PlayerInputReader`.
- [~] Implement walking, running, gravity, jumping, and grounded checks. — `PlayerMotor` (coyote time, jump buffer, no air jumps, fall respawn).
- [~] Configure third-person camera follow and collision. — `ThirdPersonCameraRig` (custom; Cinemachine not used).
- [~] Add landscape mobile controls. — On-screen joystick, Jump, Interact, touch-look area, safe-area fitting.
- [ ] Build and test on a physical Android device. *(deferred)*

**Exit criteria:** player movement and camera feel reliable; no major control blockers.

## Phase 2 — First environment
- [~] Create a compact forest trail and viewpoint. — Village slice with path to the forest gate and Lookout Hill (placeholder geometry).
- [~] Add terrain, trees, rocks, and one landmark. — Quaternius Nature MegaKit: about 87 trees (village broadleaf, forest twisted, edge pines, dead), 18 boulders, about 400 undergrowth props, pebble-lined trail and stepping stones. Landmarks: well, Great Old Oak (13 m twisted tree), forest gate. Farm pen with animals.
- [~] Set up basic lighting and materials. — One directional light, trilight ambient, linear fog, URP Simple Lit flat materials. Not baked.
- [~] Add collision and safe world boundaries. — Invisible boundary walls plus fall respawn.
- [ ] Tune LODs, shadows, and foliage density. — **Risk:** about 400 alpha-cutout foliage props were added; measure on device and reduce counts in `GreenhavenArt.BuildNature` if needed.
- [ ] Measure device frame time.

**Exit criteria:** one visually coherent path is explorable at the performance target or has documented optimization issues.

## Phase 3 — Interaction and collectibles
- [~] Define `IInteractable`.
- [~] Implement interaction detection and contextual prompt. — `PlayerInteractor`, `InteractionPromptView`.
- [~] Implement one collectible prefab with stable ID. — `Collectible` + `CollectibleRegistry`; 3 supply crates (`supply_01..03`) placed by the builder.
- [~] Prevent duplicate collection. — `CollectedItems` rejects duplicate IDs (EditMode tests); duplicate scene IDs are logged as errors.
- [~] Update a visible counter or objective display. — `CollectibleCounterView`: "Supplies n/3", top left.
- [ ] Test interaction range and repeated taps.

**Exit criteria:** player can reliably discover and collect items on touch devices.

## Phase 4 — Quest system
- [ ] Create quest definitions using ScriptableObjects.
- [ ] Implement quest states and objective progress.
- [ ] Implement collect-item objective.
- [ ] Implement talk-to-NPC objective.
- [ ] Implement inspect-landmark objective.
- [ ] Build the three MVP missions.
- [ ] Add mission start, progress, and completion UI.

**Exit criteria:** all three missions can be completed in a predictable sequence.

## Phase 5 — NPCs and world flow
- [~] Add at least two interactable NPCs. — Mira (standing, idle/talking) and Elder Rowan (sitting on a bench, idle/talking), plus a readable signpost.
- [~] Add dialogue UI with short text. — `DialogueView`.
- [ ] Add the village start point.
- [ ] Connect village, forest, and mountain trail.
- [ ] Add one route/landmark unlocked by progress.
- [ ] Check that mission objectives are understandable without external instructions.

**Exit criteria:** the region supports a complete short adventure loop.

## Phase 6 — Save/load and offline validation
- [ ] Define versioned save DTOs.
- [ ] Persist quest states and objective counts.
- [ ] Persist collected stable IDs and unlock state.
- [ ] Restore player position safely.
- [ ] Handle missing, malformed, and older save data.
- [ ] Test app restart and airplane mode.
- [ ] Test app pause/resume and Android back navigation.

**Exit criteria:** core progress survives restart and core gameplay needs no network.

## Phase 7 — Polish and performance
- [ ] Tune mobile UI for common landscape aspect ratios.
- [ ] Add basic audio feedback where available.
- [ ] Profile CPU/GPU frame time on target device.
- [ ] Optimize textures, shadows, foliage, and draw calls based on evidence.
- [ ] Check memory and thermal behaviour during a longer play session.
- [ ] Run regression tests for movement, interactions, quests, and saves.
- [ ] Record known issues and create a release candidate build.

**Exit criteria:** acceptance criteria in `spec.md` are checked and all remaining limitations are documented.

## Deferred backlog — not MVP
- [ ] Larger connected world or additive region loading.
- [ ] More villages, ruins, and story chapters.
- [ ] Day/night or weather.
- [ ] Advanced NPC schedules.
- [ ] Combat, crafting, or large inventory.
- [ ] Runtime AI-generated missions.
- [ ] Multiplayer or cloud save.
