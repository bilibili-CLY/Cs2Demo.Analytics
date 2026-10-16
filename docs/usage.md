# 调用文档（给引用本库的项目看）

本库做两件事：**解析 demo**拿原始数据，**算指标**拿统计结果。分两步调用，
第一步和第二步完全解开——`MatchData` 可以自己造（比如单元测试），不一定非要从 demo 来。

解析底层用 csda（[akiver/cs-demo-analyzer](https://github.com/akiver/cs-demo-analyzer)，MIT），
随包附带各平台预编译二进制，引用后自动复制到输出目录 `csda/<rid>/`，运行时就地调用。

## 两种引用方式

**A. NuGet 包引用（干净版）**：对方项目 `.csproj` 加：

```xml
<PackageReference Include="Cs2Demo.Analytics" Version="0.3.0" />
```

包内已带 csda 二进制，无需另装依赖（旧版要引的 `DemoFile.Game.Cs` 已不再需要）。

**B. 混淆 dll 直接引用（手交）**：把 `Cs2Demo.Analytics.dll`（混淆版）拷到对方项目
（比如 `lib/` 文件夹），`.csproj` 加：

```xml
<ItemGroup>
  <Reference Include="Cs2Demo.Analytics">
    <HintPath>lib\Cs2Demo.Analytics.dll</HintPath>
  </Reference>
</ItemGroup>
```

同时把随包的 `csda/` 目录（含 5 个平台的 `csda`/`csda.exe`）放到程序输出目录下，
保持 `csda/<rid>/csda[.exe]` 的相对结构（`<rid>` 见下）。运行时按当前平台自动挑对应的一份；
若部署位置不固定，用 `DemoAnalyzer.CsdaExecutablePath` 显式指定可执行文件路径即可。

## 平台支持

| 系统 | 架构 | 输出目录 `<rid>` |
|---|---|---|
| Windows | x64 | `win-x64` |
| macOS | x64 / Apple 芯片 | `osx-x64` / `osx-arm64` |
| Linux | x64 / arm64 | `linux-x64` / `linux-arm64` |

A 方式（NuGet）会自动把这些文件复制到输出目录，一般不用管；
B 方式（手交 dll）需要手动保证 `csda/<rid>/` 就在程序能找到的位置（输出目录或其同级的程序集目录）。
其他平台/架构会抛 `PlatformNotSupportedException`。

## 第一步：解析 demo → MatchData

```csharp
using Cs2Demo.Analytics;

// 从文件解析（只有这一种入口；demo 必须是本地文件）
MatchData data = DemoAnalyzer.ParseFile("match.dem");

// 可选：指定自备的 csda 可执行文件（部署位置不固定时用，设一次全局生效）
DemoAnalyzer.CsdaExecutablePath = "/opt/tools/csda";
```

| 方法 | 参数 | 返回 | 会抛什么异常 |
|---|---|---|---|
| `DemoAnalyzer.ParseFile(string path)` | demo 文件路径 | `MatchData` | 路径 null → `ArgumentNullException`；文件不存在 → `FileNotFoundException`；解析失败/无玩家回合 → `InvalidOperationException`（含超时、demo 损坏或截断、找不到 csda 二进制等情况） |

没有 `ParseStream` 了（csda 只吃本地文件路径）。`MatchData` 里有什么：
`Players`（玩家名单，含 csda 聚合值 `PlayerInfo.Csda`）、`Rounds`（回合）、`Kills`（击杀事件）、
`Damages`（伤害事件）、`Survivals`（每回合谁存活）、`Meta`（地图名、文件哈希）。
`Meta.FileSha256` 由 `ParseFile` 计算。

解析口径：热身回合不解析出来（csda 已剔除）；自杀丢掉，BOT 不计入；
**队杀与 C4 炸死记死亡**（凶手为空，不记击杀）；伤害只记敌方造成的扣血
（取自 csda 的原始 `healthDamage`，**不折叠溢出**，自伤/队伤/世界伤害已过滤）；
存活表由 csda 的每回合经济记录 + 死亡名单反推。

## 第二步（V1）：算 6 项指标 → PlayerMatchStats

```csharp
// 算全员
foreach (var s in StatsCalculator.CalculateAll(data))
    Console.WriteLine($"{s.Name}: Rating2.0={s.Rating20}, ADR={s.Adr}, KPR={s.Kpr}");

// 只要一个人（steamId 是字符串，就是记分板上那串数字）
var me = StatsCalculator.Calculate(data, "76561198xxxxxxxxx");
Console.WriteLine($"KAST={me.Kast}, 存活率={me.Surviving}, Impact={me.Impact}");
Console.WriteLine($"杀={me.Kills} 死={me.Deaths} 助攻={me.Assists} 总伤={me.TotalDamage}");
```

| 方法 | 参数 | 返回 | 会抛什么异常 |
|---|---|---|---|
| `CalculateAll(MatchData data)` | 第一步拿到的 `MatchData` | 全员的 `PlayerMatchStats` 列表 | data null → `ArgumentNullException` |
| `Calculate(MatchData data, string steamId)` | `MatchData` + 玩家 SteamId | 一个人的 `PlayerMatchStats` | data null → `ArgumentNullException`；steamId 空或人不在本场 → `ArgumentException` |

`PlayerMatchStats` 字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `SteamId / Name / Rounds` | `string / string / int` | 玩家 id、名字、总回合数 |
| `Kills / Deaths / Assists` | `int` | 杀 / 死 / 助攻（直接取 csda 计数） |
| `TotalDamage` | `int?` | 总伤害（csda 的 `HealthDamage`，原始扣血）；有回合时有值 |
| `SurvivedRounds / KastRounds` | `int` | 存活回合数 / 有贡献回合数 |
| `Kpr / Adr / Kast / Surviving / Impact / Rating20` | `double?` | 6 项指标。`Kast`（有贡献回合占比）与 `Surviving`（存活回合占比）都是 **0~100 百分数**；零回合时 6 项全 null，否则全部有值 |
| `Version` | `string` | 公式版本，当前 `"2.0.0"`，与 `StatsCalculator.Rating20Version` 一致 |

`Rating20` 是 csda 的逆向拟合近似值，不是 HLTV 官方值；公式见 `docs/formulas.md`。

## 第二步（Rating30）：算 9 项指标 → PlayerMatchStats30

```csharp
// 与 V1 完全隔开：独立方法、独立返回类型，互不影响
foreach (var s in StatsCalculator.CalculateAll30(data))
    Console.WriteLine($"{s.Name}: Rating30={s.Rating30}, Swing={s.RoundSwing}, MK={s.MultiKill}, eKPR={s.EKpr}");

var one = StatsCalculator.Calculate30(data, "76561198xxxxxxxxx");
Console.WriteLine($"Rating30={one.Rating30}, 版本={one.Version}");
```

| 方法 | 参数 | 返回 | 会抛什么异常 |
|---|---|---|---|
| `CalculateAll30(MatchData data)` | `MatchData` | 全员的 `PlayerMatchStats30` 列表 | data null → `ArgumentNullException` |
| `Calculate30(MatchData data, string steamId)` | `MatchData` + 玩家 SteamId | 一个人的 `PlayerMatchStats30` | data null → `ArgumentNullException`；steamId 空或人不在本场 → `ArgumentException` |

`PlayerMatchStats30` 字段：前面的计数和 `Kpr / Adr / Kast / Surviving / Version`
与 V1 同含义（`Kast` 同样是 0~100），另有 5 个自己的：

| 字段 | 类型 | 说明 |
|---|---|---|
| `EKpr` / `EAdr` | `double?` | 经济加权的 KPR / ADR：按对枪胜率难度给每次击杀 / 伤害加权（eco 里打赢、劣势对枪更值钱）。`Rating30` 用它俩替代 `Kpr` / `Adr` |
| `MultiKill` | `double?` | 多杀回合占比（单回合 ≥2 杀的回合数 / 总回合数 × 100，0~100 百分数，数据取自 csda 连杀回合数） |
| `RoundSwing` | `double?` | 回合胜率波动（百分点/回合）。v2 起按「经济 + 是否下包 + 存活人数」算胜率变化，并做击杀贡献分摊与回合末残值零和分配 |
| `Rating30` | `double?` | Rating 3.0 本地近似。零回合时 9 项全 null |
| `Version` | `string` | 公式版本，当前 `"3.0.0"`，与 `StatsCalculator.Rating30Version` 一致 |

Rating30 是实验性本地近似，不是 HLTV 官方值（官方从未公开精确公式）；
结构对齐官方公开的 Rating 3.0（6 个子项 + 经济加权 + Round Swing），系数由 10 张 EPL demo 拟合，
细节见 `docs/formulas.md`。

## 版本与口径

- `Version` 字段就是用来判断口径的：以后公式优化，版本号往上加
  （比如 Rating30 优化后变成 `"3.1.0"`），调用方存结果时把 `Version` 一起存，
  就知道这行数是哪个公式算的。
- 对方项目更新包后，原来调 `Calculate` 的代码不用改（只加了字段，没改签名）；
  要用 Rating30 就调 `Calculate30` 那一套。
- **0.3.0：解析后端与 V1 口径变化**：ADR / KAST 改由 csda 计算（KAST 单位从 0~1 变成 0~100），
  Rating20 `Version` 升到 `"2.0.0"`。旧版结果与新版本不可直接比较。
- **0.3.0：Rating30 升级为经济加权版**：新增 `EKpr` / `EAdr`（Rating30 用它俩替代 KPR / ADR），
  `RoundSwing` 改为经济 + 安包 + 贡献分摊的 v2，`Rating30` 用 10 张 EPL demo 重拟权重。
  Rating30 `Version` 升到 `"3.0.0"`，数值与旧的 `"2.0.0"` 不可直接比较；Rating20 仍是 `"2.0.0"` 不变。
