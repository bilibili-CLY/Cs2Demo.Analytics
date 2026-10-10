# Cs2Demo.Analytics

CS2 demo 解析与玩家数据计算的 .NET 库（`net8.0`，net8/9/10 的项目都能引用）。

## 安装

```bash
dotnet add package Cs2Demo.Analytics --version 0.1.0
```

## 快速开始

```csharp
using Cs2Demo.Analytics;

// 第一步：解析 demo，拿原始数据
MatchData data = DemoAnalyzer.ParseFile("match.dem");

// 第二步：算 6 项指标（KPR / ADR / KAST / Surviving / Impact / Rating2.0）
foreach (var s in StatsCalculator.CalculateAll(data))
    Console.WriteLine($"{s.Name}: Rating2.0={s.Rating20}, ADR={s.Adr}, KPR={s.Kpr}");
```

## 说明

- 热身回合、自杀、队伤会被丢掉；BOT 不计入。
- 缺伤害数据时 `Adr` 为 null（`Rating20` 会按 ADR=0 继续算）；零回合时 6 项全为 null。
- `Rating20` 是社区逆向拟合的近似值，不是 HLTV 官方值；`Impact` 口径见 `docs/formulas.md`。
