# Cs2Demo.Analytics

CS2 demo 解析与玩家数据计算的 .NET 库（`net8.0`，net8/9/10 的项目都能引用）。
解析交给 [csda](https://github.com/akiver/cs-demo-analyzer)（akiver/cs-demo-analyzer，MIT）；
本库负责统一口径、映射数据、并补算 csda 没有的指标。

## 安装

```bash
dotnet add package Cs2Demo.Analytics --version 0.3.0
```

## 平台支持

包内附带 csda 预编译二进制（macOS arm64/x64、Linux x64/arm64、Windows x64），
引用后会自动复制到输出目录 `csda/<rid>/csda[.exe]`，运行时就地调用。
若要用自备 csda（或非常规部署），设 `DemoAnalyzer.CsdaExecutablePath` 指向可执行文件即可。
csda 的许可证见包内 `csda/LICENSE-csda.txt`（MIT, Copyright (c) 2023-present AkiVer）。

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

- **口径来源**：KPR / ADR / KAST / Rating2.0 直接取 csda 算好的值（KAST 为 `0~100` 百分数）；
  Surviving（存活回合占比，`0~100`）/ Impact 由 csda 原料派生；Rating30 的 MultiKill（多杀回合占比，`0~100`）
  由 csda 连杀回合数派生，eKPR / eADR（经济加权的 KPR / ADR）/ RoundSwing / Rating30 由本库计算
  （Rating30 用 eKPR / eADR 替代 KPR / ADR，并把经济 / 下包 / 贡献分摊算进 RoundSwing）。
- 自杀会被丢掉；BOT 不计入。队杀与 C4 炸死记死亡（不记击杀），与官方记分板一致。
- 零回合时所有指标为 null；否则全部有值（伤害取 csda 的 `HealthDamage`，原始扣血、不折叠溢出）。
- `Rating20` / `Rating30` 是社区逆向拟合的近似值，不是 HLTV 官方值；口径见 `docs/formulas.md`。
- Rating30（实验性本地近似）：`StatsCalculator.Calculate30 / CalculateAll30` → `PlayerMatchStats30`（6 项评分与诊断字段），用法见 `docs/usage.md`。
- 返回数据带 `Version` 公式版本（Rating20 当前 `"2.0.0"`，Rating30 当前 `"3.2.0"`）；口径或公式有改动时往上加，靠它判断口径。
