# Copilot code-review instructions

This repo is a BepInEx 6 (IL2CPP) Harmony mod for *The Last Plague: Blight*. Plugins derive from `BasePlugin` and call the game through Il2CppInterop proxy assemblies. Review with these traps in mind; a general reviewer misses them.

## IL2CPP Harmony traps

- **Getter/setter patches often never fire.** il2cpp inlines trivial accessors, so a `MethodType.Getter`/`.Setter` patch silently does nothing. Flag a new getter patch used as the only mechanism. The fix is a patch of the caller, or a change of the backing field at a load hook.
- **No `is`/`as` across the interop boundary.** Flag `is`, `as`, or a direct cast on a game type. The correct form is `x.TryCast<T>()` then a null check.
- **No `foreach` over game collections.** The interop enumerator lacks the pattern. Expect `GetEnumerator()` / `MoveNext()` / `Current`, or a count plus an indexer.
- **Never read an `Il2CppSystem.ValueTuple<...>` result of a game method.** The interop layer reads the fields wrongly and gives garbage with no error.
- **A method can compile and still throw.** Some interop methods throw `NotSupportedException: Method unstripping failed` at run time, for example `ColorUtility.ToHtmlStringRGBA`. Flag a new engine call on a hot path that no game run has confirmed.
- **Guard game lookups.** Entities, labels, and interface fields can be null. Flag an unchecked dereference inside a patch.
- **A patch must not break the game.** Each patch body sits in a try/catch that logs the error once with `Plugin.WarnOnce`. A throw inside `LabelPatches.Run` stops the frame of the mod, so the labels go back to their game positions for that frame. A new optional step inside `Run` needs its own try/catch.

## Structure and tests

- **Feature folders.** `src/Plugin.cs` holds only the plugin: the config, the patch list, and `Load`. `src/Labels/` holds the label patches, and `src/Pickup/` the pickup patches.
- **Pure logic is separated and tested.** Logic that does not need the running game lives in its own file with no BepInEx or Il2Cpp reference (`LabelGrouping`, `LabelLayout`, `GroupPickupLogic`, `HintText`, `HoverGeometry`, `HoverFreeze`).
- **Each file of `src/` has a link in the test project.** The game-facing files compile against the fake game types of `tests/ProximityPickup.Tests/Fakes/`, so the tests run with no game. Flag a new source file with no `<Compile Include>` link. A fake holds only the types and members that the mod reaches. A passing test does not show that a patch fires in the game.
- **The per-frame hook has a budget.** `HudUI.Update` runs each frame, so its postfix must stay within a mean of 2 ms. Flag a physics call, a game lookup for each pair of labels, or a text measurement with no cache in that path.
- **Patches** prefer a postfix, and tie the `Harmony` instance to the plugin GUID.
- **One rule for the label and the click.** The number on a label is the number of items that a click on the label takes. Flag a change to the label groups or to the pickup that breaks this.
- **The game's pickup does the work.** Each extra item goes through `PickupEffect`, so the inventory rules, the messages, and the entity removal stay the game's. Flag code that moves an item or removes an entity directly.

## Release and config hygiene

- **Verbose ships off.** The `Verbose` config binds with default `false`. Diagnostic tracing goes on `LogDebug` behind it; `LogInfo` stays quiet apart from the load line.
- **The plugin GUID never changes.** It is `com.ivmakk.thelastplagueblight.proximitypickup`, the BepInEx identity and the config file name. Flag any edit to it.
- **The version is in two places that must agree:** `<Version>` in the csproj and the `BepInPlugin` attribute.
- **No committed build output.** Flag `bin/`, `obj/`, `dist/`, or a game DLL in the diff.
- **Save safety.** The mod writes nothing to the save. Flag a change that does.
- **Changelog matches the change.** A player-visible change adds an `[Unreleased]` entry to `CHANGELOG.md` in player-facing wording. An internal-only refactor gets none.
