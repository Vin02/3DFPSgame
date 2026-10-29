# NEFASA Eyes — 1.3.0

One eye and a linked eye pair, with nine appearance/pupil parameters, six color presets in Unity and an optional optic nerve switch. The Asset Store package includes a URP base, embedded Built-in/HDRP supplements and Blender source.

## Blender

Copy `Assets/NEFASA_Eyes/Blender_Source.zip` to a working folder outside your Unity project and extract it. Open the extracted `Blender/Augen_Original.blend`, select **Augenfarbe einstellen**, then open **Modifiers → Augenfarbe und Details**. Adjust both colors, pupil radius and surface details. **Sehnerv anzeigen** hides or restores both separate optic nerves. Use Cycles for the layered corneal optics.

`Blender/Augen_Demo.blend` adds an original studio camera and lights; press F12 to render. The clean source remains a separate file. Maps are packed and are also supplied in `Blender/Textures`.

To fit a character, choose File → Append → Augen_Original.blend → Collection and select both `Augen` and `Sehnerven – optional` together. Select only `Augen` when you intentionally want no optic nerves. Parent the controller to the head and position/rotate each eye child. The optional wet eyelid strips start hidden; fit them to your eyelid opening before showing them. Nerve children follow the eye; reshape or reposition their rear segment if your head requires a different path.

## Unity — choose one rendering pipeline

| Pipeline | Import | Required dependency |
| --- | --- | --- |
| Built-in | Common assets, then PipelinePackages/NEFASA_Eyes_BuiltIn_Supplement.unitypackage | Unity built-in renderer |
| URP | Complete base package | Universal Render Pipeline 17.3.0 |
| HDRP | Common assets, then PipelinePackages/NEFASA_Eyes_HDRP_Supplement.unitypackage | High Definition Render Pipeline 17.3.0 and its Shader Graph dependency |

Install your required render-pipeline dependency manually in Package Manager before importing its assets. For Built-in or HDRP, deselect `Assets/NEFASA_Eyes/Pipelines/URP` in the first Asset Store import dialog; import the remaining common assets and `PipelinePackages`. Then open the matching embedded supplement in `Assets/NEFASA_Eyes/PipelinePackages`. Keep only the active pipeline folder. See `AssetStore_Setup.md` for the full import and pipeline-switching steps. Tested with Unity **6000.3.23f1**. Other editor/pipeline versions and mobile devices have not been tested.

1. Import the base and, when required, the matching supplement as described above.
2. Drag `Assets/NEFASA_Eyes/Pipelines/<your pipeline>/Prefabs/EyePair` or `SingleEye` into your scene. Folder names are `URP`, `BuiltIn` and `HDRP`.
3. Select its root and adjust the **Eye Controller** Inspector. Presets change the two colors.
4. Uncheck **Sehnerv anzeigen / Show optic nerve** to disable the rear parts.
5. **Tools → NEFASA Eyes → Augen einstellen** opens the same native controls in a separate tool window.
6. Open `Assets/NEFASA_Eyes/Pipelines/<your pipeline>/Demo/Eyes_Demo` for the pipeline's lighting example.

Parent the root to your character's head bone, then fit each child. Default eye spacing is 60 mm; models are in meters and face +Z. Prefab roots have zero position/rotation and unit scale. Rotate eye children for gaze. Controls run in edit mode and gameplay, and update all three LODs. Per-instance material property blocks preserve independent colors without modifying shared materials.

Unity uses a realtime reflective corneal dome, not the full Cycles internal optical refraction. Lighting, reflection probes and exposure affect appearance. Linear color space is recommended. Gaze/blinking animations are not supplied. Wet eyelid strips are available in the Blender source only.

```csharp
using NEFASA.Eyes;
using UnityEngine;

EyeController eyes = characterEyes.GetComponent<EyeController>();
eyes.irisColor = new Color(0.28f, 0.33f, 0.20f);
eyes.innerColor = new Color(0.44f, 0.33f, 0.15f);
eyes.pupilRadius = 2.4f; // radius, millimeters
eyes.showOpticNerve = false;
eyes.ApplySettings();
```

After changing the hierarchy, call `CacheRenderers()` and `ApplySettings()`.

## Controls

| Inspector | API field | Effect |
| --- | --- | --- |
| Augenfarbe | irisColor | Main iris color |
| Innenfarbe | innerColor | Inner/pigment color |
| Pupillenradius (mm) | pupilRadius | Actual radius, 0.9–3.2 mm |
| Fasertiefe | fiberDepth | Fine iris relief strength |
| Äderchen | vessels | Branched vessel visibility |
| Pigmentierung | pigmentation | Secondary pigment mixing |
| Irisrand | limbalRing | Outer iris ring darkness |
| Glanz | gloss | Corneal/surface smoothness |
| Feuchtigkeit | moisture | Wet surface response |
| Sehnerv anzeigen | showOpticNerve | Hide/show both optional optic nerves |

## Geometry and texture data

- Surface plus cornea LOD0/1/2: **21,600 / 10,304 / 4,200 triangles per eye**.
- Optional nerve LOD0/1/2: **7,232 / 2,760 / 888 additional triangles per eye**. Eye-pair counts double.
- `Iris_Structure.png`: 3072×3072 linear RGBA; R fibers, G cavities, B pigment, A height. Preserve alpha; it is not transparency.
- `Sclera_Vessels.png`: 2048×2048 linear vessel mask. Both data maps use sRGB off and transparency processing off.
- `Sclera_Tissue.png`: 2048×2048 sRGB warm ivory/pink tissue color.
- `Nerve_Color.png`: 1024×2048 sRGB nerve sheath color with original branched pink vessels.
- `Nerve_Normal.png`: 1024×2048 linear tangent normal map, OpenGL +Y; fine longitudinal tissue detail.
- Default: warm gray-brown iris, 2.15 mm pupil radius and 0.60 vessel strength; editable without changing textures.
- `Models/RuntimeFBX` contains the twelve runtime meshes: surface, cornea and separate left/right nerve for each of three LODs. Surface meshes have pupil blend shapes. Blender Geometry Nodes and shader graphs do not transfer through FBX.
- A ready-made Unreal material is not supplied.

## Image guide

`Documentation/Gallery_DE_EN` contains bilingual boards for anatomy, the nerve switch, six colors, modeling structure, real before/after views, the three pipelines and actual control screenshots. Before/after images compare the actual default palettes of version 1.2 and version 1.3 using matched cameras and lighting; mesh/exploded views are technical illustrations of the actual completed model, not historical construction screenshots.

The Unity controls screenshot shows example values from 1.2; the controls layout is unchanged in 1.3. The new color renders show 1.3.
