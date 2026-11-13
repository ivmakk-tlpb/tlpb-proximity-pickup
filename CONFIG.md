# Configuration

Every setting lives in `BepInEx\config\com.ivmakk.thelastplagueblight.proximitypickup.cfg`, written the first time you run the game with the mod installed. Edit it with any text editor while the game is closed. Changes apply at the next game start.

## General

| Setting | Default | Values | What it does |
|---|---|---|---|
| `PickMode` | `Group` | `Group` / `One` | `Group`: a click on a label, or on an item with a label, picks all the items on that label. A click on an item with no label also picks the items of the same kind within `PickupRadius`. A gather of a ready mushroom, herb, or other resource that you gather in a moment also gathers the resources of the same kind on its label, or within `PickupRadius` with no label. Collecting clay, mud, or sand by hand takes the same kind of ground within `DigRadius` in one action. `One`: each click picks one item, gathers one resource, and each try collects one spot, as in the game. The merged labels, the label layout, and the middle-click stay on in both modes. |
| `PickupRadius` | `2` | meters, `0` or more | How close items or resources of the same kind must lie to merge into one label, and how far a click on an item or resource with no label reaches. `0` merges no labels, and picks or gathers only the clicked thing. Collecting from the ground has its own `DigRadius`. |
| `DigRadius` | `1` | meters, `0` or more | How far one action of collecting clay, mud, or sand by hand reaches: 1 m gives up to 21 tries, 2 m up to 69, with a limit of 256 tries. `0` collects one spot at a time, as in the game. |
| `Verbose` | `false` | `true` / `false` | Writes extra diagnostic lines to the BepInEx log for troubleshooting. Keep off in normal play. |

The label boxes, the line of actions above the hotbar, and the middle-click have no settings.
