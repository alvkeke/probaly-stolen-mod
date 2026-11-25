# FilterMachine

A machine mod for **Probably Stolen** that racks up to 36 water filters and spends
their durability evenly to clean water you already own.

The base game can only filter water *as it is poured* into a pitcher. There is no
way to clean a bottle that is already dirty. This machine adds that: load a dirty
container, press **Start**, and the rack works through it.

中文说明见 [README.zh-CN.md](README.zh-CN.md)。

## What it does

- **Filter Rack** — 6×6 grid, 36 slots. Accepts `water_filter` and `water_filter_adv`.
- **Water** — one slot for any liquid container (bottles, jugs, pitchers, the water tank).
- **Control** — Start button, purity bar, status line.

Pressing Start runs the water in the container through the rack:

- **Even wear, one cartridge per round.** Every contaminant gets one turn on the same
  cartridge, coarsest particles first, with `mineral` / `chemical_contaminant` /
  `heavy_metal` coming out last; then the next cartridge takes over. Each filter does the
  work the previous one did, so the rack wears down together, a cartridge at zero drops out
  of the rotation, and a rack that runs dry halfway leaves every contaminant partly cleaned
  instead of finishing one and never touching the rest.
- **Game-accurate numbers.** Filtering goes through the game's own `WaterHelper.HandleFilter`,
  so particle-size checks, `filterVolumeRemoved` and `filterDurabilityUsage` match the
  vanilla water pitcher exactly.
- **Dirt leaves with the cartridge.** What the rack traps is gone with the filter, exactly
  like pouring through the vanilla pitcher: nothing is converted back into liquid. A
  container holding acid or another non-water liquid therefore gets its particles cleaned
  out too — the acid itself is not touched.

## Install

1. Download the `FilterMachine.zip` asset from [Releases](../releases).
2. Extract it into the game's `Mods/` folder, so you end up with `Mods/FilterMachine/manifest.xml`.
3. Launch the game → **Mods** menu → select **Filter Machine** → review and enable.
4. Restart the game.

The mod declares no capabilities: it only calls the game's own API. The scanner will
show nothing to approve beyond the file fingerprint.

## Getting a machine in game

Dr. Jackson (the inventor) lists the machine with the rest of his catalogue, at his own
value, both at his workshop and on the days he visits your store. He stocks it before his
own goods, so it is always on his table.

For testing there is still the dev menu:

1. Create an empty `dev.txt` next to `Probably Stolen.exe` and restart the game.
2. In a save, press **F9** → **Spawn** column → **Spawn Filter Machine**.
3. The machine lands in your inventory. You need a free 3×3 area for it.
4. Double-click it to open the window.

`F8` opens the console if something goes wrong; `mod-scan filter_machine` shows the
scanner's verdict.

## Build from source

You need the game installed (it ships the managed assemblies) and the .NET SDK 6.0 or later.

Clone this repository inside the game install folder and build the mod folder:

```bash
cd FilterMachine
dotnet build -c Release
```

`Directory.Build.props` in the repository root finds the game by walking up for
`Probably Stolen.exe`, so no mod has to know how deep it sits. A checkout stored
elsewhere builds too, as long as you point at an install:

```bash
dotnet build -c Release -p:GameDir="/path/to/Probably Stolen"
```

A plain build installs into `<game>/Mods/FilterMachine/`. To produce a release zip:

```bash
tools/package.sh   # -> dist/FilterMachine.zip
```

The shared `InstallMod` target in `Directory.Build.props` copies only the files the
loader expects. Building straight into `Mods/` would drop a `.pdb` and a `.deps.json`
there, and the mod scanner flags files a manifest does not list.

## Layout

This mod occupies one folder of the repository:

```
FilterMachine/
├── ModEntry.cs              IMod entry, item factory, filtering pass
├── VendorStock.cs           stocks the machine on Dr. Jackson's sale table
├── FilterMachine.csproj     netstandard2.1; references come from ../Directory.Build.props
├── manifest.xml             mod manifest (ID filter_machine)
├── Textures/Items/
│   └── filter_machine.png   48x48 = a 3x3 footprint (gridSize is 16)
├── Localization/
│   ├── en.csv
│   └── zh.csv               the game's locale code for Chinese is zh
├── tools/
│   ├── generate_icon.py     regenerates the sprite
│   ├── package.py           zips the payload for a release
│   └── package.sh           build + package
└── build.sh                 build + install
```

## How the footprint works

`GameItem.SetSpriteAndShapeFromMod` divides the image size by `UISettings.current.gridSize`
(hardcoded to 16) and samples the **centre pixel of each cell** for opacity. So a 48×48
image is a full 3×3 item. Sizes that are not multiples of 16 are silently truncated —
47×48 becomes 2×3 with no warning.

## Limitations

- **No power draw.** No battery slot and no module bay; it runs on filters alone.
- **One container slot.** Batch filtering would need `GameGridInventory(1, 4)` and a loop.
- **Cannot remove microbes.** Water filters never could; that is `uv_filter`'s job, and it is
  a drag-on tool with no durability, so the rack rejects it the same way the water pitcher does.
- **Dissolved chemicals stay.** Acid, base, alcohol and the like have a smaller particle size
  than a cartridge's `FILTER_SIZE_TAG`, so the rack passes them through untouched — again the
  same rule the vanilla pitcher applies.
- **No purity ceiling.** Nothing is held back the way the purifier machine holds back
  contamination, so a pass can strip the container to zero. The only residue is the bit a
  cartridge cannot trap in a single bite (`filterMinimumPurity`: 0.16% of the bite for
  mineral / organic waste / physical contaminant, 0.2% for microplastic); the next turn
  works on that remainder.
- **Dr. Jackson only.** He sells it; it is in no loot table.

## License

No license is provided. All rights reserved — the code is public for reference, but
reuse, modification and redistribution are not granted.
