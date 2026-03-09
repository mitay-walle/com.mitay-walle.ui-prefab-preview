<img width="947" height="1209" alt="{170C4F1D-3084-4B3B-9B3B-1B5EFDAB0FFF}" src="https://github.com/user-attachments/assets/0a799baf-0100-41ee-8c44-4fd35a7f64e2" />

# Problem

UI prefabs built with [UGUI / Canvas](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/index.html) usually have no useful preview
in the [Project window](https://docs.unity3d.com/2020.1/Documentation/Manual/ProjectView.html)

# Solution
Script that renders custom previews for UGUI prefabs using
`PreviewRenderUtility`.

# Installation
Add the script `UiPrefabProjectPreviewDrawer.cs` to folder `Project/Assets/Editor`
# Usage
- zoom in project window. minimized icons not draw preview (except textures)
