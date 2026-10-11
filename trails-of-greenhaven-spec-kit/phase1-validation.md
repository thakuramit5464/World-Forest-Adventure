# Phase 1 — Setup and Validation

## 1. One-time Editor steps

1. Let Unity finish compiling. Open the Console, which should show **no errors** from `Assets/_Game`.
2. From the menu bar, choose **Greenhaven ▸ Build Phase 1 Scene**. This creates:
   - `Assets/_Game/Scenes/GreenhavenRegion.unity`, which is added first in Build Settings
   - `Assets/_Game/Materials/M_*.mat` (flat-colour URP Simple Lit)
   - `Assets/_Game/Prefabs/Player.prefab` and `NPC_Villager.prefab`
3. Press **Play**.

`SampleScene` is left untouched. You can delete it, or remove it from Build Settings, once you are happy with the new scene.

## 2. Controls

| Action | Keyboard / mouse (Editor) | Gamepad | Touch |
|---|---|---|---|
| Move | WASD / arrow keys | Left stick | Joystick (bottom left) |
| Run | Hold Left Shift | Click the left stick, or push it fully | Push the joystick past 90% |
| Jump | Space | South (A) | Jump button |
| Interact / continue dialogue | E | West (X) | Interact button, or tap the dialogue panel |
| Look | Hold right mouse button and drag | Right stick | Drag on the right side of the screen |

In the Editor you can click and drag the on-screen joystick and buttons with the mouse. To check touch layouts, use **Window ▸ General ▸ Device Simulator**.

## 3. Play-mode test checklist (manual, not yet run)

| # | Test | Expected |
|---|---|---|
| 1 | Walk in each direction with the camera rotated | Movement is relative to the camera and the character turns to face the direction of travel |
| 2 | Hold Shift, or push the stick fully | Speed rises from about 2.6 to about 5.5 m/s |
| 3 | Press Jump repeatedly while in the air | Only one jump until landing. A press just before landing still jumps (buffer) |
| 4 | Walk off a step, then jump within about 0.1 s | Jump still fires (coyote time) |
| 5 | Lookout Hill (east): stepping stones, then the platform, then the ramp | Stones need a jump. The ramp can be walked |
| 6 | Walk to the map edge | Invisible walls stop the player |
| 7 | Back the camera into a house or tree | Camera moves in front of the obstacle and the player stays visible |
| 8 | Walk to the signpost, then to Mira (by the well) | "Read Signpost" or "Talk to Mira" appears and the Interact button brightens |
| 9 | Interact with Mira repeatedly | Shows 3 lines, then closes. The prompt reads "Continue" while the dialogue is open |
| 10 | Walk away mid-dialogue | Dialogue closes |
| 11 | Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All | 14 test cases pass (`MovementMathTests` 9, `InteractionScoringTests` 5) |

## 4. Performance notes and risks (unmeasured)

- **Draw calls:** about 75 trees × 3 renderers, plus houses and rocks. Everything except the player, the NPC, and UI is marked Batching Static, and URP's SRP Batcher is on by default. Check the Frame Debugger and Stats.
- **Shadows:** one soft directional shadow, with shadow distance taken from the active URP asset. On Android, use `Mobile_RPAsset` and consider hard shadows or a shorter shadow distance.
- **Lighting:** nothing is baked yet. Baking needs lightmap UVs and a Lighting Settings asset. Postponed until real art replaces the primitives.
- **Fog** is linear and cheap. There is no skybox; the camera clears to a solid colour.
- **UI:** a single Screen Space Overlay canvas. The legacy `Text` font avoids a TextMeshPro import step.

## 5. Android switch (later)

1. Unity Hub ▸ Installs ▸ 6000.6.5f1 ▸ Add modules ▸ **Android Build Support** (with OpenJDK and the Android SDK/NDK).
2. File ▸ Build Profiles ▸ Android ▸ Switch Platform. Confirm that the Android quality level uses `Mobile_RPAsset`.
3. Player Settings: landscape only (already `defaultScreenOrientation: 4`, LandscapeLeft), IL2CPP with ARM64 (already set), minimum API 26 (already set). Change `companyName`/`productName` and the package identifier.
4. Build and run on the test device. Record the device model, RAM, Android version, resolution, FPS and frame time (Profiler over USB), memory, and thermal behaviour in `tasks.md`.
5. Check touch layout at both 16:9 and 20:9, safe-area behaviour on a notched device, and pause/resume.
