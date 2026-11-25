# FilterMachine

给 **Probably Stolen** 做的机器模组：最多装 36 个滤芯，均匀消耗它们的耐久来过滤你**已经拥有**的水。

游戏原版只能在**往水壶里倒水的那一刻**过滤，一瓶已经脏了的水没有办法补救。这台机器补的就是这个缺口：把脏容器放进去，按**开始**，滤芯架就把它清干净。

English version: [README.md](README.md)

## 功能

- **滤芯架** —— 6×6 网格，36 格。接受 `water_filter` 和 `water_filter_adv`。
- **水** —— 1 格，接受任何液体容器（瓶子、水壶、水箱都行）。
- **控制** —— 开始按钮、纯度进度条、状态文字。

按开始后，容器里的水会流经整个滤芯架：

- **平分磨损，每轮用一颗滤芯** —— 一轮里所有污染物各用这颗滤芯过一次（颗粒粗的优先，`mineral` / `chemical_contaminant` / `heavy_metal` 垫底），然后换下一颗。每颗滤芯做的活和上一颗一样多，所以整架磨损均匀；耐久到 0 的滤芯自动退出轮转；滤芯中途耗尽时，是所有污染物各清掉一部分，而不是清完一种、剩下的完全没动。
- **数值与原版一致** —— 走的是游戏自己的 `WaterHelper.HandleFilter`，所以颗粒尺寸判断、`filterVolumeRemoved`、`filterDurabilityUsage` 和原版滤水壶完全相同。
- **滤掉的脏东西随滤芯一起走** —— 和原版滤水壶倒水过滤一样，被截住的体积不会回流成液体。所以装的是酸或其它非水液体时，里面的颗粒污染物同样能被清掉，而酸本身不动。

## 安装

1. 从 [Releases](../releases) 下载 `FilterMachine.zip` 附件。
2. 解压到游戏的 `Mods/` 目录，最终路径应为 `Mods/FilterMachine/manifest.xml`。
3. 启动游戏 → **Mods** 菜单 → 选中 **Filter Machine** → 审核并启用。
4. 重启游戏。

本模组没有声明任何能力：代码只调用游戏自己的 API，扫描器除了文件指纹之外不会有需要你批准的东西。

## 在游戏里拿到机器

机器只由**杰克逊博士**（发明家）出售，按物品自身价值计价：你去他的工坊、或他来你店里时，
他的货单里都会带上一台。机器排在他自己的货品之前上架，所以不会被挤掉。

调试时仍然可以用开发菜单：

1. 在 `Probably Stolen.exe` 同级目录建一个空的 `dev.txt`，重启游戏。
2. 进入存档，按 **F9** → **Spawn** 列 → **Spawn Filter Machine**。
3. 机器会进你的背包。它占地 **3×3**，需要先腾出 9 格。
4. 双击它打开窗口。

出问题按 `F8` 看控制台；`mod-scan filter_machine` 可以查看扫描器结论。

## 从源码编译

需要装好游戏本体（它提供托管程序集）以及 .NET SDK 6.0 或更高版本。

把本仓库克隆到游戏安装目录里，然后编译模组文件夹：

```bash
cd FilterMachine
dotnet build -c Release
```

仓库根目录的 `Directory.Build.props` 会向上查找 `Probably Stolen.exe` 来定位游戏，所以每个
mod 都不需要知道自己埋了多深。仓库放在别处也能编译，只要显式指向一个游戏目录：

```bash
dotnet build -c Release -p:GameDir="/path/to/Probably Stolen"
```

普通编译会自动安装到 `<游戏目录>/Mods/FilterMachine/`。要出发布包：

```bash
tools/package.sh   # -> dist/FilterMachine.zip
```

`Directory.Build.props` 里共享的 `InstallMod` 目标只复制加载器需要的文件。如果直接把编译
输出指向 `Mods/`，会顺带扔进去 `.pdb` 和 `.deps.json`，而模组扫描器会对清单未列出的文件报警。

## 目录结构

本 mod 占用仓库中的一个文件夹：

```
FilterMachine/
├── ModEntry.cs              IMod 入口、物品工厂、过滤流程
├── VendorStock.cs           把机器上架到杰克逊博士的货架
├── FilterMachine.csproj     netstandard2.1；引用来自 ../Directory.Build.props
├── manifest.xml             模组清单（ID 为 filter_machine）
├── Textures/Items/
│   └── filter_machine.png   48×48，即 3×3 占地（gridSize 是 16）
├── Localization/
│   ├── en.csv
│   └── zh.csv               游戏里中文的语言代码是 zh
├── tools/
│   ├── generate_icon.py     重新生成贴图
│   ├── package.py           打包发布用的 zip
│   └── package.sh           编译 + 打包
└── build.sh                 编译 + 安装
```

## 占地是怎么算的

`GameItem.SetSpriteAndShapeFromMod` 会把图片尺寸除以 `UISettings.current.gridSize`（硬编码 16），
并对**每格的中心像素**做透明度采样。所以 48×48 的图就是完整的 3×3 物品。不是 16 倍数的尺寸会被
静默截断——47×48 会变成 2×3，没有任何警告。

## 已知边界

- **不耗电** —— 没有电池槽，也没有模块槽，只靠滤芯工作。
- **只有 1 个容器槽** —— 想批量过滤需要换成 `GameGridInventory(1, 4)` 再遍历。
- **杀不掉微生物** —— 水滤芯本来就不杀菌，那是 `uv_filter` 的职责；它是拖到容器上用的工具、没有耐久，
  所以滤芯架按原版滤水壶的写法把它排除了。
- **酸、碱、酒精这类溶解物不会被滤掉** —— 它们的颗粒尺寸比滤芯的 `FILTER_SIZE_TAG` 还小，滤芯架
  会原样放过，这也是原版滤水壶的规则。
- **没有纯度上限** —— 不像净化机那样强制留下一部分污染，所以一趟可以清到 0。唯一会剩下的是单次
  咬一口时滤芯吞不下的那一点（`filterMinimumPurity`：矿物/有机垃圾/物理污染物是这一口的 0.16%，
  微塑料是 0.2%），下一轮会接着处理这点残余。
- **只由杰克逊博士出售** —— 不在任何掉落表里。

## 许可证

未提供许可证，保留所有权利。代码公开仅供阅读参考，未授权复用、修改与再分发。
