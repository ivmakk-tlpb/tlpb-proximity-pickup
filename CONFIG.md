# Configuration

Every setting lives in `BepInEx\config\com.ivmakk.thelastplagueblight.proximitypickup.cfg`, written the first time you run the game with the mod installed. Edit it with any text editor while the game is closed. Changes apply at the next game start.

## General

| Setting | Default | Values | What it does |
|---|---|---|---|
| `PickMode` | `Group` | `Group` / `One` | `Group`: a click on a label, or on an item with a label, picks all the items on that label. A click on an item with no label also picks the items of the same kind within `PickupRadius`. Collecting clay, mud, or sand by hand takes the same kind of ground within `PickupRadius` in one action. `One`: each click picks one item and each try collects one spot, as in the game. The merged labels and the label layout stay on in both modes. |
| `PickupRadius` | `2` | meters, `0` or more | How close items of the same kind must lie to merge into one label, and how far a click on an item with no label reaches. It also sets how far one action of collecting from the ground reaches: 2 m gives up to 69 tries, with a limit of 256 tries. `0` merges no labels, picks only the clicked item, and collects one spot at a time. |
| `Verbose` | `false` | `true` / `false` | Writes extra diagnostic lines to the BepInEx log for troubleshooting. Keep off in normal play. |

The label boxes and the line of actions above the hotbar have no settings.
