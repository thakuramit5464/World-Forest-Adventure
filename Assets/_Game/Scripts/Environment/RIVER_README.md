# Greenhaven Flowing Rivers

Stylised flowing water made entirely in Unity: a custom URP shader with no textures, procedural meshes, and no packages or downloaded assets.

## Using it

- **Greenhaven ▸ Environment ▸ Generate Flowing River** upgrades the water in the open scene: the village **Stream** and the Cloudpeak **Whispering Brook**, or any mesh named like water.
  - It creates a `Flowing River (generated)` object along the same course and level.
  - The old flat water mesh is only **hidden** (renamed "… (replaced by flowing river)"), never deleted.
  - It also adds pebbles, plants and a few rocks in the shallows, kept off trails, paths and bridges.
  - Undo reverts everything.
- **Rebuilding scenes:** the village and Cloudpeak builders now create flowing water directly.
- **Greenhaven ▸ Environment ▸ Create River Demo Scene** builds `Scenes/RiverDemo.unity`: a winding river with two bends, a slow pool, normal flow and fast narrows, plus a bridge and dressed banks.
- **Greenhaven ▸ Environment ▸ Capture River Preview** renders Standard quality at two moments and Low quality to `Generated/River/River_Preview.png`, and reports how much the water moved.
- **Editing a river:** select it. Drag the numbered control points in the Scene view; the arrows show the path direction. Then change any of these and press **Rebuild River**:
  - width and depth (depth is visual only);
  - cross-section resolution;
  - segments: a speed per stretch starting at a control point, where negative reverses the flow;
  - edge, bend, rock and fast-water foam;
  - quality.

  **Regenerate Bank Details** re-dresses the banks.

## How the flow works

- **Mesh:** a smooth strip along a Catmull-Rom spline. UV0 is in metres: x across the river, y along it. Downstream is +y everywhere, so the flow follows every bend without seams.
- **Baked per-vertex data (UV2):**
  - foam: banks, the outside of bends, around rocks with a trail downstream, and fast stretches;
  - signed segment speed, blended over 4 m at every segment boundary;
  - depth, for colour and opacity.
- **Shader (`Greenhaven/RiverWater`):** two layers of procedural ripples scroll downstream using the **two-phase flow-map technique**. Two copies half a cycle apart cross-fade, so the pattern never stretches, whatever the speed differences or play time.
  - Colour runs from a turquoise shallow to a deep blue-green, with darker banks.
  - Brightness varies with the ripples.
  - Sun highlights use a cheap normal derived from the ripple height, plus small sparkles.
  - Soft foam streaks appear only where the mesh asks for them.
  - Fog is supported.
  - Everything runs from `_Time`, so it's frame-rate independent and **the mesh never changes at runtime**.
- **`RiverFlowController` at runtime only:**
  - swaps the Low/Standard material;
  - moves a pool of about 10 floating leaves along the centre line at the local flow speed (one particle system, no GameObjects);
  - emits a few splash particles at in-water rocks (one particle system, at most 48 particles);
  - glides one optional looping audio source to the nearest point of the river.

  All of it pauses beyond Active Distance (60 m).

## Quality and performance

| | Standard | Low |
|---|---|---|
| Blending | Transparent (soft edges) | Opaque (no overdraw) |
| Ripple layers | 2 (two octaves each) | 1 (one octave) |
| Specular highlight | Yes | Sparkles only |

- **Materials:** `M_RiverWater` and `M_RiverWater_Low` (in `Generated/River`) are shared by every river, which keeps them SRP-Batcher friendly.
- **No expensive effects:** no reflections, refraction, depth or opaque texture, or screen-space effects.
- **No collider on the water:** the bed and banks are the walkable terrain.

## Tested so far

- The shader and scripts compile.
- The preview renders were inspected; see the session report.
- The EditMode tests check the mesh data: `RiverTests`.

**Not tested yet:** Play Mode with the player, and performance on an Android device.

## Audio

The project contains no water sounds. Each river has a `River Ambience` AudioSource with **no clip**. Assign a looping CC0 river clip (and record it in `ASSET_CREDITS.md`); it then plays with distance falloff (3–22 m).
