# 调用方法（给引用本库的项目看）

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

## 代码（两种引用写法完全一样）

```csharp
using Cs2Demo.Analytics;

// 第一步：解析（文件或流二选一）
MatchData data = DemoAnalyzer.ParseFile("match.dem");
// MatchData data = DemoAnalyzer.ParseStream(stream);

// 第二步：算全员 6 项
foreach (var s in StatsCalculator.CalculateAll(data))
    Console.WriteLine($"{s.Name}: Rating2.0={s.Rating20}, ADR={s.Adr}, KPR={s.Kpr}");

// 只要一个人
var me = StatsCalculator.Calculate(data, "76561198xxxxxxxxx");
Console.WriteLine($"KAST={me.Kast}, 存活率={me.Surviving}, Impact={me.Impact}");
Console.WriteLine($"杀={me.Kills} 死={me.Deaths} 助攻={me.Assists} 总伤={me.TotalDamage}");

// Rating30（实验性本地近似，非 HLTV 官方值）：独立方法、独立返回类型，与上面互不影响
foreach (var s in StatsCalculator.CalculateAll30(data))
    Console.WriteLine($"{s.Name}: Rating30={s.Rating30}, Swing={s.RoundSwing}, MK={s.MultiKill}");
```

## 字段速查

- `PlayerMatchStats.Rounds / Kills / Deaths / Assists / TotalDamage / SurvivedRounds / KastRounds`：原始计数。
- `Kpr / Adr / Kast / Surviving / Impact / Rating20`：6 项指标（`double?`）。
- `Adr` 缺伤害数据时为 null（此时 `Rating20` 按 ADR=0 照算）；零回合时 6 项全 null。
- `SteamId` 是字符串；阵营只有 `"T" / "CT" / ""` 三种。
- `Version`：公式版本（Rating20 和 Rating30 当前都是 `"1.0.0"`，与 `StatsCalculator.Rating20Version / Rating30Version` 一致）。公式优化后版本号往上加，调用方靠它判断口径。
