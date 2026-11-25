# Probably Stolen — mod collection

Mods for **[Probably Stolen](https://store.steampowered.com/app/4348910/)** (Questing Goose
Studio / tinyBUILD), built against the game's own modding API. Each mod occupies one folder
of this repository and installs into its own folder under the game's `Mods/`.

## Mods

| Mod | What it does | Install folder |
|---|---|---|
| [FilterMachine](FilterMachine/) | A 3×3 water filter machine: a 6×6 filter rack, one liquid container slot and a Start button that spends filter durability evenly to clean water you already own. Sold by Dr. Jackson. | `Mods/FilterMachine/` |

## Requirements

- A copy of the game. It ships the managed assemblies (`Probably Stolen_Data/Managed/`) that
  mods compile against, and there is no redistributable SDK, so the game is mandatory.
- The .NET SDK 6.0 or later.

## Build a mod

Clone this repository **inside the game install folder** (the directory containing
`Probably Stolen.exe`), then build the folder of the mod you want:

```bash
cd FilterMachine
dotnet build -c Release
```

A successful build installs the mod into `<game>/Mods/FilterMachine/`, ready to be enabled in
the game's **Mods** menu.

A checkout stored anywhere else builds too, as long as you point at an install:

```bash
dotnet build -c Release -p:GameDir="/path/to/Probably Stolen"
```

## How the shared build works

`Directory.Build.props` at the repository root is imported by every project below it and
provides the three things each mod needs:

1. **Game location.** It searches upwards from the repository root for `Probably Stolen.exe`
   and derives `GameDir` from it. No mod hardcodes how deep it sits, so moving the repository
   or adding another mod folder changes nothing.
2. **Assemblies.** It references `Assembly-CSharp`, `UnityEngine` and
   `UnityEngine.CoreModule` from the game's `Managed/` folder with `<Private>false</Private>`,
   so the game's assemblies are never copied into a build output. That matters twice over:
   redistributing them is not allowed, and the mod scanner refuses a mod folder containing
   assemblies its manifest does not list.
3. **Install step.** The `InstallMod` target copies only what the loader expects into
   `<game>/Mods/<project name>/`: the built DLL, `manifest.xml`, `Textures/**` and
   `Localization/*`. Building straight into `Mods/` would leave a `.pdb` and a `.deps.json`
   there, which the scanner also flags.

If the game cannot be found, the build stops with an explicit error instead of a wall of
missing-type errors.

## Adding a new mod

A mod folder needs four things, and the build fills in the rest:

```
NewMod/
├── NewMod.csproj        netstandard2.1; references and install come from ../Directory.Build.props
├── manifest.xml         the game reads this at startup
├── <one or more>.cs     implements IMod (Init / OnEnable / OnDisable)
└── Textures/, Localization/    optional; copied to the mod folder on build
```

The minimum project file looks like this:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyName>NewMod</AssemblyName>
    <RootNamespace>NewMod</RootNamespace>
  </PropertyGroup>
</Project>
```

Three names have to agree, or the mod silently misbehaves:

| Where | Must match |
|---|---|
| `<AssemblyName>` in the csproj | `<Assemblies><Assembly>NewMod.dll` in `manifest.xml` |
| `<ID>` in `manifest.xml` | the mod ID your code passes to `SetSpriteAndShapeFromMod` / `ModHelper.GetLocalized` |
| project folder name | the folder the build installs into (`Mods/NewMod/`) |

See `FilterMachine/README.md` for a worked example, and the
[official modding guide](https://github.com/dragonpan2/probably-stolen-modding) for the API.

## Releases

Build artifacts are not committed (`.gitignore` excludes `bin/`, `obj/`, `dist/` and every
`*.dll`). Each mod produces its own archive:

```bash
cd FilterMachine
tools/package.sh   # -> dist/FilterMachine.zip
```

Upload that zip as a release asset. Players extract it into `Mods/`, which yields
`Mods/FilterMachine/manifest.xml`.

## License

No license is provided. All rights reserved — the code is public for reference, but reuse,
modification and redistribution are not granted.

---

## 中文速览

这是《Probably Stolen》的模组集合仓库，**每个 mod 占一个文件夹**，编译后各自安装到游戏的
`Mods/<文件夹名>/`。

- **编译**：把仓库克隆到游戏安装目录（含 `Probably Stolen.exe` 的那层）里，然后
  `cd FilterMachine && dotnet build -c Release`。编译成功会自动装进 `Mods/FilterMachine/`。
- **仓库在别处**：`dotnet build -c Release -p:GameDir="/游戏路径"`。
- **共享构建**：根目录的 `Directory.Build.props` 负责定位游戏、引用游戏的程序集、以及把成品
  只挑需要的文件复制到 `Mods/`。每个 mod 的 csproj 因此只有几行。
- **游戏程序集绝不提交**：既是版权问题，也会被模组扫描器拒绝。

各 mod 的详细说明见其文件夹内的 `README.md` / `README.zh-CN.md`。
