TRAILS OF GREENHAVEN — PROCEDURAL ADVENTURER MODEL

Files:
- Greenhaven_Adventurer.obj: low-poly character model, with separate named mesh objects for body parts.
- Greenhaven_Adventurer.mtl: material colors.

Unity import:
1. Extract both files into Assets/Greenhaven/Models/Adventurer/.
2. Select the OBJ in Unity and confirm its materials are imported. If needed, create URP/Lit materials using the colors in the MTL.
3. Drag the model into a test scene and adjust scale/orientation as needed.
4. Keep your existing player controller root and use this model as its visual child.

Limitations:
- This is a static low-poly OBJ model. It is not rigged, skinned, or animated yet.
- It is a procedural starter model, not a production-quality sculpt. The mesh parts are separate objects and may need visual tuning.
- Before animation, create a skeleton/rig and weight the mesh or animate the separate parts using Unity transforms.
- Verify material import and orientation in your specific Unity version.
