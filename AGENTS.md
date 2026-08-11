# Quasimorph Loadouts contributor notes

- Build against the user's currently installed Quasimorph assemblies. Never commit game DLLs, Workshop DLLs, or proprietary game assets.
- Current verification target: Quasimorph 1.0.1, Steam build 24612814, `Assembly-CSharp.dll` SHA-256 `A9C031111E126CA86CE515EB69D9E25AF2DB29BB00560AF2896AE3E3E0E374A6`.
- Prefer desired-state loadouts. Item provenance is not stable across stacking, consumption, repairs, and missions.
- Preserve item objects and their metadata when moving whole items. Treat splitting metadata-bearing stacks cautiously.
- Never remove an equipped item until its desired replacement is known to exist. Use the game's movement/cargo APIs so failed moves leave the source item intact.
- Keep the first milestone narrow: one persistent `Default` preset, F6 Save, F7 Apply, and clear result reporting on `ArsenalScreen`.
- Reference implementations may inform API usage, but new code must be original. Record any copied/substantially adapted upstream code and its license in `THIRD_PARTY_NOTICES.md`.
