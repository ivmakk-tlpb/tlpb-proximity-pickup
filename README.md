![version](https://img.shields.io/badge/version-1.1.0-blue) ![coverage](https://img.shields.io/badge/coverage-99%25-brightgreen)

# Proximity Pickup

Proximity Pickup is a mod for the Steam game *The Last Plague: Blight*. Items dropped close together are hard to pick: with the item labels on (Alt), the labels pile up on top of each other, a click often hits the wrong item, and a pile of 10 sticks takes 10 clicks. The mod merges the labels of a pile into one label such as "Stick (10)", keeps all labels apart, and picks the whole pile with one click. It does the same for a patch of mushrooms, herbs, or other things that you gather in a moment.

## Features

- **Merged labels.** With the item labels on, items of the same kind that lie within 2 m merge into one label with the total count, for example "Stick (5)" for 5 sticks or "Long Stick (10)" for 5 stacks of 2. A single item keeps the game's own label text, with "Wet" and its condition. Zooming the camera does not change the merged labels. A pile wider than 2 m can show two labels, each with its own count. Ready resources that you gather in a moment merge the same way, with the number of resources: "Spotted Red Mushroom (5)". An item label and a resource label never merge, and unripe resources keep their own labels.
- **No overlapping labels.** All labels that still overlap move up or down until each has its own space, also the labels of stockpiles, resources, and constructions. Labels never move sideways, and no lines are drawn.
- **Label boxes.** Each label of an item, stockpile, resource, or construction sits on a half-transparent dark box, and the whole box is clickable. Area names such as "Lake" have no box.
- **A hover that stays in place.** Move the cursor onto a label or onto its item, and the name stays where the label was, with the same color and font size. Its box gets darker and gains a thin border in the color of the label. The labels around it do not move. For a merged label, the name shows the total count.
- **Actions above the hotbar.** The actions of the hovered thing, such as "Pick up", and their keys show in one line above the hotbar, in the style of the game's inventory controls. Nothing shows under the label and covers the item. When the inventory is open, the line sits above the inventory controls. When you hold an action, the box of the label fills from the left as the action progresses.
- **Pick a pile with one click.** Click a label, or an item with a label, to pick all the items on that label: a click on "Stick (11)" picks 11 sticks. A label next to it stays. With the labels off, click an item to also pick every item of the same kind within 2 m of it, and hover it to see the total. Your character plays one pickup animation, then the items go into your inventory, with one "+N" message.
- **Gather a patch with one click.** Click a ready mushroom, herb, Dandelions, a stick or small stone that you gather, or a Clay Deposit to gather every ready resource of the same kind on its label: a click on "Dandelions (5)" gathers 5. With the labels off, the click gathers the same kind within `PickupRadius`, and the hover shows the total. Your character gathers once and takes the time of one gather, and one message shows the total. Each resource gives the normal yield, drop rolls, and XP of one gather, and regrows as after a normal gather. Unripe resources and other kinds stay. Timed gathers (Blueberry Bush, animal remains, Hardened Pine Resin, and rocks that give a Large Stone) stay as in the game. The game setting "Quick pickups" does not change which resources take part.
- **A full bag stops the gathering.** Before each extra resource, the mod checks that your bag has room for all its drops, not counting your hands. If not, the gathering stops, and that resource and the rest stay in the world. The check counts each drop as if every roll hits, so with an almost full bag it can stop one resource early. The resource you clicked follows the game's rules.
- **Take one with a middle-click.** Middle-click a label, or the thing under the cursor, to pick up one item or gather one resource, also from a merged label. Your character walks to it as for a normal click. While you hover a merged label, the line of actions above the hotbar shows the middle mouse button with "Pick up one" or "Gather one". Hold or drag the middle mouse button to turn the camera as before. A thing with no pickup or gather ignores the middle-click.
- **The game's rules apply.** Each item goes through the game's own pickup, so the inventory limits stay the same. When your inventory is full, the items that do not fit stay on the ground, and the game shows its usual message. Nothing is lost or duplicated. The whole pile takes the time of one pickup instead of one pickup for each item.
- **Large items are picked one at a time.** Items that the game puts into your hands or onto your back (large items, two-handed items, lit fuel, the substances that the game puts into your hands, and items worn on the back) never take part in a group pickup, so a pickup never changes what you hold or wear. Their labels still merge, and a click picks one of them. Small stones, tools, and weapons are picked as a group only while your bag has room for them: when the bag is full, they stay on the ground, and your hands stay as they are.
- **Collect clay and mud in one click.** Collect from clay, mud, or sand with your hands, and one action takes the same kind of ground within `DigRadius`: at the default of 1 m, up to 21 normal tries, at 2 m up to 69, with a limit of 256 tries at a large radius. Set `DigRadius` to `0` to collect one spot at a time. Each spot gives the normal yield of one try, and one message shows the total. Your character stops after the one action, so click again for more. Other kinds of ground and spots where the game does not let you collect are skipped. If the game charges Stamina or tool wear for a try, it charges each spot. When your bag is full, the collecting stops and the game shows its usual message. Dirt, tilled ground, and digging with a shovel stay as in the game. A Clay Deposit is a gather, see above.
- **Turn the group pickup, the group gather, and the group collect off.** Set `PickMode` to `One` to pick one item, gather one resource, and collect one try for each click, as in the game. The merged labels and the middle-click stay on.

## Save safety

The mod writes nothing to the save. You can remove it at any time, and your save loads as before.

## Multiplayer

Made and tested for single player. Only pickups by your own character pick a group; pickups by other characters are not changed. Gathering a patch and collecting from the ground work in a radius only for the player who hosts the game. Co-op is not tested.

## Requirements

- The Last Plague: Blight 0.12.7 or later.
- The [BepInEx Pack for The Last Plague: Blight](https://www.nexusmods.com/thelastplagueblight/mods/9), the BepInEx 6 (IL2CPP) build for the game.

## Install

Download the zip from [Nexus Mods](https://www.nexusmods.com/thelastplagueblight/mods/10) or [GitHub Releases](https://github.com/ivmakk-tlpb/tlpb-proximity-pickup/releases).

1. Install the [BepInEx Pack for The Last Plague: Blight](https://www.nexusmods.com/thelastplagueblight/mods/9). If no other mods were installed before, start the game once so BepInEx finishes setup, then quit.
2. Extract this mod's zip into the game folder (the folder with the game .exe). The DLL lands in `BepInEx\plugins`. Full path example:
   - Steam: `C:\Program Files (x86)\Steam\steamapps\common\The Last Plague\BepInEx\plugins\ProximityPickup.dll`

## Uninstall

Delete `ProximityPickup.dll` from the `BepInEx\plugins` folder. On the next game start, the labels and the pickup work as without the mod.

## Configuration

To change mod settings, edit `BepInEx\config\com.ivmakk.thelastplagueblight.proximitypickup.cfg` while the game is closed (see [CONFIG.md](CONFIG.md) for every setting). Changes apply at the next game start.

## Troubleshooting

If a game update renames a part of the game that the mod needs, that part of the mod turns itself off, and the game works as without it.

Check `BepInEx\LogOutput.log` for the `Proximity Pickup loaded.` line, and for warnings or errors from Proximity Pickup.

## Build

Proximity Pickup is a BepInEx 6 IL2CPP plugin. Building requires the .NET 8 SDK and a game installation with BepInEx. Start the game once after installing BepInEx to generate the IL2CPP interop assemblies. These assemblies come from the game and are not included in this repository.

```
dotnet build src/ProximityPickup.csproj -c Release
```

`Directory.Build.props` sets `GameDir` to the default Steam installation path. For another location, set the `GameDir` environment variable or pass `-p:GameDir=...` to the build command. The output DLL is `src\bin\Release\ProximityPickup.dll`.

Unit tests cover all files of `src/`: the label merge, the label layout, the pickup flow, the plugin load, the hover state, and the values that the drawing code sets. The tests run with no game and no BepInEx, because the test project compiles the patch code against the fake game types in `tests/ProximityPickup.Tests/Fakes/`. A passing test does not show that a patch fires in the game.

```
dotnet test tests/ProximityPickup.Tests
```

## Package

```
dotnet build src/ProximityPickup.csproj -c Release -p:Package=true
```

The zip is `dist/ProximityPickup-<version>.zip`, laid out as `BepInEx/plugins/ProximityPickup.dll`.

## License

Licensed under the GNU General Public License v3.0. Copyright (C) 2026 ivmakk. See [LICENSE](LICENSE).

You may reuse and modify this mod, but you must keep it open under the same license and give credit. Do not reupload it without credit.
