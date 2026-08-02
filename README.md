# LocalWorkshop

Load custom placeable objects directly from a local folder without uploading them to the Steam Workshop.

---

## Overview

LocalWorkshop is a MelonLoader mod for **BOXROOM** that extends the game's built-in Workshop system by loading custom placeables from a local `Mods` folder.

Instead of publishing every object to Steam Workshop, LocalWorkshop allows creators to develop, test, and use custom items entirely offline while still remaining compatible with Workshop content.

The mod patches BOXROOM's `ModsPlaceableLoader` and injects additional placeables after the normal Workshop loading process.

---

## Features

- Load custom placeables from a local folder
- No Steam Workshop upload required
- Unlimited local mods
- Supports GLB models
- Supports custom preview images
- Fast development workflow
- Compatible with Steam Workshop
- Automatically creates the Mods folder on first launch

---

## Why?

Steam Workshop is great for distributing finished creations, but not everything needs to be uploaded.

LocalWorkshop is useful for:

- Developing new placeables
- Testing models before publishing
- Keeping experimental content private
- Community collaboration
- Creating personal collections
- Reducing Workshop clutter from test uploads and frequent revisions

---

## Requirements

- BOXROOM
- MelonLoader 0.7+
- Harmony (included with MelonLoader)

---

## Installation

1. Install MelonLoader.
2. Copy `LocalWorkshop.dll` into your game's `Mods` folder.

Example:

```text
BOXROOM/
└── Mods/
    └── LocalWorkshop.dll
```

Launch the game once.

LocalWorkshop will automatically create the following directory if it does not already exist:

```text
%LocalLow%\NestedLoop\BOXROOM\Mods
```

---

## Folder Structure

Each custom object should be placed inside its own folder.

Example:

```text
Mods/
├── NES_Controller/
│   ├── manifest.json
│   ├── controller.glb
│   └── preview.png
│
├── Book/
│   ├── manifest.json
│   ├── book.glb
│   └── cover.png
│
└── ServoSkull/
    ├── manifest.json
    ├── skull.glb
    └── preview.png
```

There is no limit to the number of folders or placeables.

---

## Manifest Format

Example:

```json
{
    "ID": "NES_Controller",
    "DisplayName": "Nintendo Controller",
    "PlacementType": "Prop",
    "ModelPath": "controller.glb",
    "SpritePath": "preview.png",
    "ToolType": "Placeable",
    "Scale": 0.05
}
```

---

## Manifest Fields

| Field | Description |
|--------|-------------|
| `ID` | Unique identifier for the object. |
| `DisplayName` | Name displayed in-game. |
| `PlacementType` | BOXROOM placement type (Prop, Furniture, etc.). |
| `ModelPath` | Relative path to the GLB model. |
| `SpritePath` | Relative path to the preview image. |
| `ToolType` | Tool category used by BOXROOM. |
| `Scale` | Default scale applied when loaded. |

---

## How It Works

LocalWorkshop patches:

```csharp
ModsPlaceableLoader.LoadPlaceableData()
```

After BOXROOM finishes loading Steam Workshop items, LocalWorkshop:

1. Scans the local `Mods` directory.
2. Reads every `manifest.json`.
3. Creates a new `PlaceableData` instance.
4. Registers the model path.
5. Registers the preview sprite.
6. Adds the object to BOXROOM's placeable list.

The game treats these objects exactly like normal Workshop placeables.

---

## Notes

- Steam Workshop continues to function normally.
- LocalWorkshop simply adds additional placeables after Workshop content has loaded.
- All assets remain on your local machine.
- This mod does not modify or replace the Steam Workshop.

---
## Licence

MIT License

---

## Credits

Created by **MidgetBrony**

Built using:

- MelonLoader
- Harmony
- BOXROOM