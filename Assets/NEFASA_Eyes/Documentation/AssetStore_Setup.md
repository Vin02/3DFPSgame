# NEFASA Eyes 1.3.0 - Asset Store setup

Tested with Unity 6000.3.23f1. URP and HDRP versions: 17.3.0. Choose one rendering pipeline.

## Import

URP: install Universal Render Pipeline 17.3.0 through Package Manager, then import the product normally.

Built-in/HDRP: in the first product import dialog deselect Assets/NEFASA_Eyes/Pipelines/URP. Import the remaining common assets and PipelinePackages. Open the matching BuiltIn/HDRP supplement in PipelinePackages. HDRP requires High Definition Render Pipeline 17.3.0 and its Shader Graph dependency, installed manually before its supplement.

Use Pipelines/<your pipeline>/Prefabs/SingleEye or EyePair. The demo is Pipelines/<your pipeline>/Demo/Eyes_Demo.

Import only the matching active pipeline folder. URP shaders require URP packages. When switching an already configured project, remove the previous Pipelines/<pipeline> folder after saving your scene changes; then install the target supplement and use its prefabs. Shared Runtime, Editor, Models, Textures and documentation remain common.

The supplements contain pipeline-specific shaders, materials, prefabs and demo settings. They do not duplicate or install common code, geometry, textures or Unity Registry packages.

## Controls

Select an eye prefab root and use Eye Controller, or Tools > NEFASA Eyes > Augen einstellen. Original nine appearance/pupil controls, six presets and optional nerve switch are retained. Pupil radius is 0.9-3.2 mm; models use meters and +Z forward. Parent to the character head and fit each eye. No skeletal rig, gaze/blinking animation or automatic project-manifest modifications are supplied.

README_EN.md and START_HIER.txt provide the English/German controls and technical guide for this Store layout.
