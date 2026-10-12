# 调用文档（给引用本库的项目看）

本库只做两件事：**解析 demo**拿原始数据，**算指标**拿统计结果。分两步调用，
第一步和第二步完全解开——`MatchData` 可以自己造（比如单元测试），不一定非要从 demo 来。

## 两种引用方式

**A. NuGet 包引用（干净版）**：对方项目 `.csproj` 加：

```xml
<PackageReference Include="Cs2Demo.Analytics" Version="0.1.0" />
```

**B. 混淆 dll 直接引用（手交）**：把 `Cs2Demo.Analytics.dll`（混淆版）拷到对方项目
（比如 `lib/` 文件夹），`.csproj` 加：

```xml
<ItemGroup>
  <Reference Include="Cs2Demo.Analytics">
    <HintPath>lib\Cs2Demo.Analytics.dll</HintPath>
  </Reference>
  <PackageReference Include="DemoFile.Game.Cs" Version="0.43.1" />
</ItemGroup>
```

注意 B 方式也要引用 `DemoFile.Game.Cs`（公开包，nuget.org 直接装），
否则运行时报 `FileNotFoundException`（已验证）。

## 第一步：解析 demo → MatchData

```csharp
using Cs2Demo.Analytics;

// 从文件解析
MatchData data = DemoAnalyzer.ParseFile("match.dem");

// 或从已打开的流解析（流自己关，本库不关）
using var stream = File.OpenRead("match.dem");
MatchData data2 = DemoAnalyzer.ParseStream(stream);
```

| 方法 | 参数 | 返回 | 会抛什么异常 |
|---|---|---|---|
| `ParseFile(string path)` | demo 文件路径 | `MatchData` | 路径 null → `ArgumentNullException`；文件不存在 → `FileNotFoundException`；解析不出玩家/回合 → `InvalidOperationException` |
| `ParseStream(Stream stream, string? sourceName = null)` | 已打开的流；`sourceName` 只用来拼错误信息（比如文件名） | `MatchData` | 流 null → `ArgumentNullException`；解析失败 → `InvalidOperationException`（ inner 是原始错误） |

`MatchData` 里有什么：`Players`（玩家名单）、`Rounds`（回合）、`Kills`（击杀事件）、
`Damages`（伤害事件）、`Survivals`（每回合谁存活）、`Meta`（地图名、文件哈希等）。
用流解析时没有文件哈希，`Meta.FileSha256` 为空。

解析口径（跟官方记分板对齐过的）：热身回合、自杀会被丢掉，BOT 不计入；
**队杀与 C4 炸死记死亡**（凶手为空，不记击杀）；伤害只记敌方造成的真实扣血
（溢出伤害已折掉，自伤/队伤/世界伤害不计入）。

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
| `Kills / Deaths / Assists` | `int` | 杀 / 死 / 助攻 |
| `TotalDamage` | `int?` | 总真实伤害；demo 缺伤害数据时为 null |
| `SurvivedRounds / KastRounds` | `int` | 存活回合数 / 有贡献回合数 |
| `Kpr / Adr / Kast / Surviving / Impact / Rating20` | `double?` | 6 项指标。缺伤害时只有 `Adr` 为 null（`Rating20` 按 ADR=0 照算）；零回合时 6 项全 null |
| `Version` | `string` | 公式版本，当前 `"1.0.0"`，与 `StatsCalculator.Rating20Version` 一致 |

`Rating20` 是社区逆向拟合的近似值，不是 HLTV 官方值；公式见 `docs/formulas.md`。

## 第二步（Rating30）：算 7 项指标 → PlayerMatchStats30

```csharp
// 与 V1 完全隔开：独立方法、独立返回类型，互不影响
foreach (var s in StatsCalculator.CalculateAll30(data))
    Console.WriteLine($"{s.Name}: Rating30={s.Rating30}, Swing={s.RoundSwing}, MK={s.MultiKill}");

var one = StatsCalculator.Calculate30(data, "76561198xxxxxxxxx");
Console.WriteLine($"Rating30={one.Rating30}, 版本={one.Version}");
```

| 方法 | 参数 | 返回 | 会抛什么异常 |
|---|---|---|---|
| `CalculateAll30(MatchData data)` | `MatchData` | 全员的 `PlayerMatchStats30` 列表 | data null → `ArgumentNullException` |
| `Calculate30(MatchData data, string steamId)` | `MatchData` + 玩家 SteamId | 一个人的 `PlayerMatchStats30` | data null → `ArgumentNullException`；steamId 空或人不在本场 → `ArgumentException` |

`PlayerMatchStats30` 字段：前面的计数和 `Kpr / Adr / Kast / Surviving / Version`
与 V1 同含义，另有 3 个自己的：

| 字段 | 类型 | 说明 |
|---|---|---|
| `MultiKill` | `double?` | 连杀评分（1 杀权重 1，2 连杀权重 4……5 杀权重 25，加权平均） |
| `RoundSwing` | `double?` | 回合胜率波动，单位百分点/回合（击杀加胜率、被杀扣胜率，纯零和） |
| `Rating30` | `double?` | Rating 3.0 本地近似。零回合时 7 项全 null；缺伤害时 `Adr` 为 null 但按 ADR=0 照算 |
| `Version` | `string` | 公式版本，当前 `"1.0.0"`，与 `StatsCalculator.Rating30Version` 一致 |

Rating30 是实验性本地近似，不是 HLTV 官方值（官方从未公开精确公式）；
拟合方法与误差见 `docs/formulas.md`。

## 版本与口径

- `Version` 字段就是用来判断口径的：以后公式优化，版本号往上加
  （比如 Rating30 优化后变成 `"1.1.0"`），调用方存结果时把 `Version` 一起存，
  就知道这行数是哪个公式算的。
- 对方项目更新包后，原来调 `Calculate` 的代码不用改（只加了字段，没改签名）；
  要用 Rating30 就调 `Calculate30` 那一套。
