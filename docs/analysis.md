# Sea Power ≥ 0.8.3 under CrossOver: units load empty (no ships, 0 points)

## Summary

Since 0.8.3, Sea Power running under CrossOver on macOS (Wine) loads most units without data: missions start without ships, the campaign task force builder shows every unit at 0 points, and terrain can be missing. 0.8.2 works in the same bottle. The cause is that `File.Exists` returns `true` for nonexistent files when it is called from the worker threads of the new parallel loading code, so the game resolves base-game files to `StreamingAssets\user\...` and then fails to read them.

## Environment

- Sea Power 0.8.5 (build 261008), also seen with 0.8.3 (build 261001) and 0.8.4. 0.8.2 works.
- CrossOver 26.3 and CrossOver Preview (build 20261006) on macOS 27.0.1, Apple Silicon (Mac16,9), Steam inside the bottle.
- No mods, no Workshop items. Reproduces with a fresh profile (LocalLow folder and `StreamingAssets\user` renamed before launch).

## Steps to reproduce

1. Run Sea Power ≥ 0.8.3 under CrossOver.
2. Open the campaign task force builder, or start any mission.
3. Units show 0 points / ships are missing. The session log contains thousands of `Could not find a part of the path "...\StreamingAssets\user\vessels\<unit>.ini" ... (from mod 'User Data')` followed by `<unit>: Unknown or missing unit role: ''`.

## Analysis

- The errors happen while `UnitMetadataProvider.OnStart` loads unit metadata in a `Parallel.For`, and also from `IniPreloader` and terrain loading.
- The `original\` file index is complete (7,623 entries, including all unit files).
- For the `user` folder, `FileManager.FileExistsIn` uses `File.Exists`. On the main thread this correctly returns `false` for missing files. On the `Parallel.For` worker threads it returns `true` for files that do not exist (the folders `user\vessels`, `user\aircraft` and `user\land_units` do not exist at all), so `GetFile` returns a path under `user\`, and reading it fails. See [`3-file-exists-true-on-worker-threads.txt`](logs/3-file-exists-true-on-worker-threads.txt), including a call stack.
- The terrain loader also chooses between `user\terrain\data\` and `original\terrain\data\` with `File.Exists`, which produces the same kind of errors for `chunk_*.dat` files once a mission loads.
- Plain Wine file checks from a single thread (e.g. `cmd /c if exist`) answer correctly in the same bottle.

## Workaround

A BepInEx plugin that confirms every positive `File.Exists` answer inside `StreamingAssets` with a directory listing fixes it completely: 0 `user\` path errors and 0 empty units, with about 2,700 false positives rejected in one session ([`4-session-with-workaround.txt`](logs/4-session-with-workaround.txt)). Source and installation: see the [README](../README.md).

Possible fixes on the game side: also index the `user` folder (and invalidate the index when files are written there), or resolve file locations on the main thread before the parallel loading starts.

## Files

- [`1-broken-session-0.8.5.txt`](logs/1-broken-session-0.8.5.txt): session log excerpt, normal profile, no plugin.
- [`2-broken-session-fresh-profile.txt`](logs/2-broken-session-fresh-profile.txt): the same with a fresh profile.
- [`3-file-exists-true-on-worker-threads.txt`](logs/3-file-exists-true-on-worker-threads.txt): diagnostic logging showing `File.Exists` results per thread, with a call stack.
- [`4-session-with-workaround.txt`](logs/4-session-with-workaround.txt): session log excerpt with the workaround plugin.
- [`Player.log`](logs/Player.log), [`usersettings.ini`](logs/usersettings.ini): from `AppData\LocalLow\Triassic Games\Sea Power\`, as requested in the pinned bug report post. ([`Player.log`](logs/Player.log) is from a session with the workaround installed; it contains nothing relevant either way.)

The full session logs (30–120 MB each) are available on request.
