# Quasimorph Loadouts

Inventory/loadout quality-of-life mod for Quasimorph. Save named desired loadouts, apply missing equipment and quantities from ship cargo, or normalize a post-mission inventory in one action.

Verified build target: **Quasimorph 1.0.1**, Steam build **24612814** (11 August 2026).

## Current controls

Open the preparation/loadout (Arsenal) screen with a mercenary selected. A draggable **Loadout Presets** window provides the same actions as the hotkeys:

- **F5 — Select next preset.** The `<` and `>` buttons select in either direction.
- **F6 — Save:** saves weapon/equipment slots plus exact item-ID quantities in the backpack and vest under the name shown. Edit the name before saving to create another preset.
- **F7 — Apply:** equips the selected preset and tops backpack/vest quantities up from normal ship cargo tabs and the fridge.
- **F8 — Normalize:** unloads non-preset and excess backpack/vest items to normal cargo, then applies and tops up the selected preset.

Apply and Normalize report missing items, locked items, and lack of inventory space in an in-game dialog.

The preset is stored outside the save files at:

`%USERPROFILE%\AppData\LocalLow\Magnum Scriptum Ltd\Quasimorph_ModConfigs\QuasimorphLoadouts\presets.json`

The previous preset file is retained as `presets.json.bak` whenever the mod writes a change.

## Build and install

Quasimorph must be installed locally. Its DLLs are referenced at build time and are never copied into this repository or mod package.

From PowerShell in the repository:

```powershell
.\build.ps1 -GamePath "X:\SteamLibrary\steamapps\common\Quasimorph" -Install
```

This builds `artifacts\QuasimorphLoadouts` and installs the two mod files into Quasimorph's supported local development folder:

`%USERPROFILE%\AppData\LocalLow\Magnum Scriptum Ltd\Quasimorph\LocalUserPresets\QuasimorphLoadouts`

Restart Quasimorph after installing an assembly. The mod should appear in the game's Mods list as `RadicalGitter_QuasimorphLoadouts`.

## First in-game test

1. Back up the active save before testing any new inventory mod.
2. Open the preparation/loadout screen and arrange a small, easy-to-recognize loadout.
3. Enter a useful preset name, press F6, and confirm the saved summary appears.
4. Move one saved item back to cargo and reduce one saved ammo/consumable stack.
5. Press F7. Confirm the item returns, the quantity is topped up, and any unavailable item is listed rather than causing an error.
6. Add a miscellaneous item, press F8, and confirm it moves to normal cargo while preset quantities remain.
7. Quit and relaunch the game, then press F7 again to verify preset selection and persistence.

If loading or applying fails, inspect `Player.log` in Quasimorph's LocalLow folder and search for `[QuasimorphLoadouts]`.

## Deliberate milestone-one rules

- Identity is exact game item ID. Different ammo, weapon variants, and custom-item IDs are different preset entries.
- Whole items retain their live durability, loaded ammo, expiration, and other metadata. When several matching pieces of gear exist, Apply chooses the highest-condition one.
- Stack quantities are desired counts, not specific stack objects. Splits use the game's stack/usage helpers; provenance is intentionally not preserved.
- Saved stack coordinates are best-effort placement hints. Newly transferred items try their saved cells and fall back to any valid cell. Correct existing items are not shuffled merely to reproduce layout.
- Loaded ammunition inside a weapon is part of the weapon object, not the loose-ammo target. This milestone does not reload weapons.
- Apply can replace equipped gear only after the desired replacement is found. Empty preset equipment slots are cleared to cargo.
- Apply searches normal cargo tabs and the fridge. It never takes from the recycling storage.
- Apply only tops up backpack/vest contents. Normalize unloads non-preset and excess contents first. Locked items are left alone and reported.

## Planned progression

1. Validate named presets and Normalize against varied real inventories.
2. Decide whether Apply and Restock should have separate semantics, including optional weapon reload behavior.
3. Replace the functional draggable panel with native-styled controls after behavior stabilizes.
4. Add preset rename/delete affordances, configuration, Workshop packaging, and polish.

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for open-source mods consulted as API/convention references.
