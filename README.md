# Sea Power CrossOver fix

A small [BepInEx](https://github.com/BepInEx/BepInEx) plugin that makes [Sea Power: Naval Combat in the Missile Age](https://store.steampowered.com/app/1286220/) load its units and terrain correctly when it runs under CrossOver (and possibly other Wine-based setups such as Proton).

This is a temporary workaround until the issue is fixed in the game itself. It does not modify any game files and contains no game code.

## Symptoms

Since version 0.8.3, under CrossOver on macOS:

- Missions start without ships, or with only a few of them.
- The campaign task force builder lists ships, but every unit costs 0 points.
- Terrain can be missing, with airfields and installations floating on water.
- The game's session log (`Sea Power_Data\Logs\*.html`) is full of errors like:

```
ERROR: Could not find a part of the path "...\Sea Power_Data\StreamingAssets\user\vessels\usn_ssn_los_angeles.ini". while parsing Ini File ... (from mod 'User Data')
ERROR: usn_ssn_los_angeles: Unknown or missing unit role: ''
```

Version 0.8.2 works fine in the same setup.

## Cause

Since 0.8.3 the game loads unit, ammunition and terrain data on many threads in parallel. For every file it first checks whether a user override exists in `StreamingAssets\user\` using `File.Exists`. Under CrossOver/Wine, `File.Exists` sometimes returns `true` for files that do not exist when it is called from those worker threads; on the main thread the same check answers correctly. The game then tries to read the base-game files from the nearly empty `user` folder, fails, and the units end up without data.

This was confirmed by logging the calls in a running game: in one session the plugin rejected about 2,700 false positives, all on worker threads.

## What the plugin does

It patches `System.IO.File.Exists`. Whenever the answer is `true` for a path inside the game's `StreamingAssets` folder, the plugin confirms it with a directory listing, which stays reliable, and turns the answer into `false` if the file is not really there. Paths outside `StreamingAssets` are not touched.

## Installation (CrossOver on macOS)

1. Download `SeaPowerCrossOverFix-2.0.0.zip` from the [latest release](../../releases/latest). It contains [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) (unmodified) together with the plugin, so nothing needs to be built or downloaded separately.
2. Unpack it into the game folder, next to `Sea Power.exe`. In a CrossOver bottle that is `drive_c/Program Files (x86)/Steam/steamapps/common/Sea Power/` (in CrossOver: select the bottle, then "Open C: Drive"). Note that `.doorstop_version` is a hidden file; Finder's Archive Utility and `unzip` both extract it.
3. Tell Wine to load BepInEx's `winhttp.dll` for the game. In CrossOver, open the bottle's Wine Configuration, go to Applications, add `Sea Power.exe`, then on the Libraries tab add `winhttp` and set it to "Native then Builtin". Alternatively, run this from Terminal (adjust the bottle name):

   ```sh
   "/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/wine" --bottle Steam reg add 'HKCU\Software\Wine\AppDefaults\Sea Power.exe\DllOverrides' /v winhttp /t REG_SZ /d native,builtin /f
   ```

4. Start the game. `BepInEx/LogOutput.log` in the game folder should contain a line like `Verifying File.Exists under '.../StreamingAssets/': True`, followed by `Rejected false File.Exists for ...` warnings once data loads.

If you already use BepInEx 5 for other mods, only copy `BepInEx/plugins/SeaPowerFileExistsFix.dll` (also available on its own in the release).

If the campaign point values were already cached as 0, delete the `cache` folder in `AppData/LocalLow/Triassic Games/Sea Power/` inside the bottle so the game recomputes them.

For Proton, the usual BepInEx setup should apply (launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%`). This has not been tested.

## Configuration

`BepInEx/config/soph.seapower.fileexistsfix.cfg` has a single switch, `VerifyFileExists` (default `true`), to turn the fix off without uninstalling it.

## Known issues

With BepInEx installed, the game may not quit properly from its own menu under CrossOver. Quitting it from macOS (Quit in the Dock, or Force Quit) works.

## Uninstall

Delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and the `BepInEx` and `licenses` folders from the game folder, and remove the `winhttp` override for `Sea Power.exe` again.

## Details

The analysis behind this, with log excerpts and a call stack, is in [docs/analysis.md](docs/analysis.md).

## Building

Requires the .NET SDK and a Sea Power install with BepInEx 5 already set up (the build references the game's and BepInEx's assemblies from there):

```sh
dotnet build src -c Release -p:GameDir="/path/to/Sea Power"
```

The plugin ends up in `src/bin/Release/netstandard2.1/SeaPowerFileExistsFix.dll`.

## License

MIT, see [LICENSE](LICENSE). The release zip also contains BepInEx (MIT) and UnityDoorstop (LGPL-2.1), unmodified, with their licenses in the `licenses` folder. Not affiliated with Triassic Games.
