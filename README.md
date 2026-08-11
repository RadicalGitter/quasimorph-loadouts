# Quasimorph Loadouts

Inventory/loadout quality-of-life mod for Quasimorph. The first milestone proves the core loop against the current game: save the visible mercenary's desired loadout, then apply missing equipment and quantities from ship cargo.

Verified build target: **Quasimorph 1.0.1**, Steam build **24612814** (11 August 2026).

## Current controls

Open the preparation/loadout (Arsenal) screen with a mercenary selected:

- **F6 — Save Default:** saves weapon/equipment slots plus exact item-ID quantities in the backpack and vest.
- **F7 — Apply Default:** equips the saved gear and tops backpack/vest quantities up from normal ship cargo tabs and the fridge.

Apply reports missing items and lack of inventory space in an in-game dialog. It does not remove miscellaneous backpack loot or quantities above the preset yet; that belongs to the later Normalize workflow.

The preset is stored outside the save files at:

`%USERPROFILE%\AppData\LocalLow\Magnum Scriptum Ltd\Quasimorph_ModConfigs\QuasimorphLoadouts\presets.json`

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
3. Press F6 and confirm the saved summary appears.
4. Move one saved item back to cargo and reduce one saved ammo/consumable stack.
5. Press F7. Confirm the item returns, the quantity is topped up, and any unavailable item is listed rather than causing an error.
6. Quit and relaunch the game, then press F7 again to verify preset persistence.

If loading or applying fails, inspect `Player.log` in Quasimorph's LocalLow folder and search for `[QuasimorphLoadouts]`.

## Deliberate milestone-one rules

- Identity is exact game item ID. Different ammo, weapon variants, and custom-item IDs are different preset entries.
- Whole items retain their live durability, loaded ammo, expiration, and other metadata. When several matching pieces of gear exist, Apply chooses the highest-condition one.
- Stack quantities are desired counts, not specific stack objects. Splits use the game's stack/usage helpers; provenance is intentionally not preserved.
- Loaded ammunition inside a weapon is part of the weapon object, not the loose-ammo target. This milestone does not reload weapons.
- Apply can replace equipped gear only after the desired replacement is found. Empty preset equipment slots are cleared to cargo.
- Apply searches normal cargo tabs and the fridge. It never takes from the recycling storage.
- Apply only tops up backpack/vest contents. **Normalize** will later unload non-preset and excess items before topping up.

## Planned progression

1. Validate Save → Apply in game and harden edge cases found in real saves.
2. Multiple named presets and a selector.
3. Explicit Restock action, including optional weapon reload semantics.
4. Normalize Loadout: safely unload non-preset/excess items, then top up the desired state.
5. Native buttons and preset selection in the loadout UI; Workshop packaging and polish.

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for open-source mods consulted as API/convention references.
