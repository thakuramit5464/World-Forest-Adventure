# Asset Credits — Trails of Greenhaven

Record every third-party asset here **before** it is used in a build: source, license, and attribution obligations. Do not assume an asset is free for commercial use without reading its license.

| Asset | Used for | Source | License | Attribution required | Added |
|---|---|---|---|---|---|
| Unity built-in UI sprites (`UI/Skin/Knob.psd`, `UI/Skin/UISprite.psd`) | Joystick, buttons, prompt/dialogue panels | Shipped with the Unity Editor | Unity Editor license / Terms of Service | No | 2026-10-11 |
| Unity built-in font (`LegacyRuntime.ttf`) | HUD and dialogue text | Shipped with the Unity Editor | Unity Editor license / Terms of Service | No | 2026-10-11 |
| Kenney **Animated Characters 1** (v1.1): `characterMedium.fbx`, `idle/run/jump.fbx` | Player adventurer model and animations (`Assets/_Game/Art/Characters/Adventurer/`) | https://opengameart.org/content/animated-characters-1 (by Kenney, www.kenney.nl) | CC0 1.0 (license text kept as `Kenney_License.txt`) | No (credit "Kenney" appreciated) | 2026-10-11 |
| `adventurerSkin.png`, derived from Kenney's `survivorMaleB.svg` | Player skin: dirt and blood layers removed, shirt recoloured teal, trousers brown. Editable source in `ArtSource/Characters/Adventurer/` | Derivative of the CC0 asset above | CC0 1.0 | No | 2026-10-11 |

## Placeholder content

As of Phase 1, the NPC, village, trees, rocks, and landmarks are all built from Unity primitive meshes and flat-colour URP materials created by `Assets/_Game/Scripts/Editor/Phase1SceneBuilder.cs`. They contain no third-party art.

## Candidate free sources (not yet imported, licenses to be verified at import time)

- Character: a walk clip is missing (the Kenney pack has idle, run and jump only); a CC0 animation set such as Quaternius's could add one. Confirm the license on the specific pack page before importing.
- Environment: low-poly nature/village packs in a single consistent style.
