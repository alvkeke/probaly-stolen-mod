# FilterMachine

给 **Probably Stolen** 做的机器模组：最多装 36 个滤芯，均匀消耗它们的耐久来过滤你**已经拥有**的水。

游戏原版只能在**往水壶里倒水的那一刻**过滤，一瓶已经脏了的水没有办法补救。这台机器补的就是这个缺口：把脏容器放进去，按**开始**，滤芯架就把它清干净。

English version: [README.md](README.md)

## 功能

- **滤芯架** —— 6×6 网格，36 格。接受 `water_filter` 和 `water_filter_adv`。
- **水** —— 1 格，接受任何液体容器（瓶子、水壶、水箱都行）。
- **控制** —— 开始按钮、纯度进度条、状态文字。

按开始后，容器里的水会流经整个滤芯架：

- **平分磨损** —— 每一小步只消耗「当前剩余耐久最高」的那颗滤芯 1 点耐久，然后重新挑选。36 颗滤芯一起磨到底，不会先烧穿一颗。
- **数值与原版一致** —— 走的是游戏自己的 `WaterHelper.HandleFilter`，所以颗粒尺寸判断、`filterVolumeRemoved`、`filterDurabilityUsage` 和原版滤水壶完全相同。
- **污染物折算成水** —— 被滤掉的污染物按原版净化机的做法转换成干净水，容器总量不会莫名减少。
- **顺序与原版净化机一致** —— 颗粒大的先滤，`mineral` / `chemical_contaminant` / `heavy_metal` 垫底。

## 安装

1. 从 [Releases](../releases) 下载 `FilterMachine.zip` 附件。
2. 解压到游戏的 `Mods/` 目录，最终路径应为 `Mods/FilterMachine/manifest.xml`。
3. 启动游戏 → **Mods** 菜单 → 选中 **Filter Machine** → 审核并启用。
4. 重启游戏。

本模组没有声明任何能力：代码只调用游戏自己的 API，扫描器除了文件指纹之外不会有需要你批准的东西。

## 在游戏里拿到机器

机器在两位卖水相关装备的商人处上架，按物品自身价值出售：

- **退休水商** —— 每周第 3 天来店里一次的顾客，只有「水商开局」或解锁 Water Trader 后才会出现。
  他的货架是 18×4 格，自己的货已经快占满，所以机器放不下时会随机挤掉他一件大件货给它腾位置；
  他的滤芯、试纸、净化片这些小耗材仍然留在货架上。
- **杰克逊博士** —— 发明家的货单里会带上一台，无论你去他的工坊还是他来你的店里。

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
├── VendorStock.cs           把机器上架到退休水商与杰克逊博士的货架
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
- **纯度上限 99.01%** —— 沿用原版净化机无模块档位，同时也是过滤循环的终止条件。
- **只在两位商人处出售** —— 退休水商和杰克逊博士，不在任何掉落表里。

## 许可证

未提供许可证，保留所有权利。代码公开仅供阅读参考，未授权复用、修改与再分发。
