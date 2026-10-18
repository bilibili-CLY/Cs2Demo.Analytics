# cs-demo-analyzer（csda）调用文档

> 目标读者：**Cs2Demo.Analytics 仓库**（以及其他想复用 demo 解析/指标计算的调用方）。
> 本文只讲一件事：怎么拿到、怎么调用 `csda` 这个二进制，以及怎么消费它的输出。
> 文中结论均来自 `akiver/cs-demo-analyzer` 源码（`pkg/api/player.go`、`pkg/api/damage.go`）、`akiver/cs-demo-manager` 的调用方式，以及本仓库在 golden demo 上的实测。

> **状态（0.3.0 起）**：本仓库已把 csda 作为**唯一解析后端**——`DemoAnalyzer.ParseFile` 起 csda 进程拿 JSON，
> 由 `CsdaMatchMapper` 映射成 `MatchData`；KPR/ADR/KAST/Rating2.0 直接用 csda 的值，
> 其余（Surviving/Impact/MultiKill/eKPR/eADR/Rating3.0/RoundSwing）由本库派生或自算。
> 实现见 `src/Cs2Demo.Analytics/Parsing/{CsdaRunner,CsdaBinaryLocator,CsdaMatchMapper,CsdaDtos}.cs`
> 与 `src/Cs2Demo.Analytics/Stats/{StatsCalculator,SwingCalculator}.cs`。实测细节（过滤对账、`-output` 目录要求、
> 整数阵营编码等）记录在 `docs/demo-format.md`。
>
> **0.3.0 起**：为支持 Rating30 的经济加权与 Swing v3，本库还会消费 csda 的**经济 / 下包 / 残局 / tickrate** 数据
> （`rounds` 的 `teamA*/teamB*` 装备与经济类型、`playerEconomies`、`bombsPlanted/bombsDefused`、`clutches`，
> 以及击杀/伤害里的 `weaponType` / `isAssistedFlash` / `victimArmor` 等），映射见 `docs/demo-format.md`。

---

## 1. 它是什么

- 仓库：`https://github.com/akiver/cs-demo-analyzer`
- 语言/形态：**Go 编写的独立命令行程序**，可执行文件名 `csda`（Windows 为 `csda.exe`）。
- 作用：解析 CS:GO / CS2 的 `.dem`，输出一整套比赛原始数据（回合、击杀、伤害、经济、位置、道具……），**并内置算出若干指标**（ADR / KPR / DPR / KAST / Impact / Rating 1.0 / Rating 2.0）。
- 输入：一个 `.dem` 文件；输出：`json` / `csv` / `csdm` 三种格式。
- 许可：**MIT License**（Copyright 2023-present AkiVer）。可以自由分发、商用，需保留许可证与版权声明。

一句话：**你不需要自己造解析轮子，直接调 `csda` 就能拿数据**；`cs-demo-manager` 就是这么做的。

---

## 2. 怎么拿到二进制

### 2.1 预编译 Release（推荐随应用分发）

在 GitHub Releases 下载对应平台的 zip（每个约 3.3–3.7 MB），解压得到单个可执行文件：

| 平台 | 二进制 |
|---|---|
| macOS Apple Silicon | `csda`（darwin-arm64） |
| macOS Intel | `csda`（darwin-x64） |
| Linux x64 | `csda`（linux-x64） |
| Linux arm64 | `csda`（linux-arm64） |
| Windows x64 | `csda.exe` |

### 2.2 npm 包（备选）

`npm` 包 **`@akiver/cs-demo-analyzer`**（当前 `1.11.0`）自带二进制，路径为
`node_modules/@akiver/cs-demo-analyzer/bin/<platform>/csda`（Windows 为 `csda.exe`）。
该包的 TS 封装（`js/src/index.ts`）本质就是用 `child_process` 去 spawn 这个二进制 —— 说明它就是一个纯 CLI，没有原生库依赖。

### 2.3 分发建议

- 把对应平台的可执行文件放进应用资源目录（`cs-demo-manager` 就是放进自己的 `static/`，再 `spawn`）。
- 二进制**无运行时依赖**（不是 .NET / Node 库），跨平台只需换文件。
- 记得附上 MIT 许可证原文。

---

## 3. 命令行用法

```
csda -demo-path=<demo文件> -output=<目录或文件> [-format=csv|json|csdm] [-source=<来源>] [-positions] [-minify]
```

| 参数 | 必填 | 说明 |
|---|---|---|
| `-demo-path` | 是 | `.dem` 文件路径 |
| `-output` | 是 | 输出位置。`csv` **必须是目录**；`json`/`csdm` 文档说可给文件或目录，但**本仓库实测 v1.11.0 给文件路径会报 `invalid output provided`（exit 1），给已存在的目录才正常**，故统一用目录 |
| `-format` | 否 | `csv`（默认）/ `json` / `csdm` |
| `-source` | 否 | 数据来源：`valve` / `faceit` / `esea` / `esl` / `esportal` / `ebot` / `5eplay` / `perfectworld` / `popflash` / `fastcup` / `challengermode` |
| `-positions` | 否 | 额外输出逐 tick 玩家位置（体积很大，非必要别开） |
| `-minify` | 否 | JSON 输出压缩（默认是缩进美化） |

### 3.1 输出位置规则

- `-format=json`：`-output` 给目录时写入 `<demo文件名>.json`（本库就这么用）；文档说也可给 `*.json` 文件路径，但 v1.11.0 实测会报错，别用文件路径。默认缩进，`-minify` 变紧凑。
- `-format=csv`：`-output` 必须是目录，每个数据类别写成一个 csv 文件。
- `-format=csdm`：`cs-demo-manager` 用的格式（喂给它的导入器）。

### 3.2 退出码

| 码 | 含义 |
|---|---|
| `0` | 成功 |
| `1` | 分析失败（demo 坏、格式不支持等） |
| `2` | 参数错误 |

### 3.3 错误信号

- 损坏/截断的 demo：**stderr** 会打印含 `ErrUnexpectedEndOfDemo` 的信息（`cs-demo-manager` 就是靠 grep 这个字符串判定 demo 损坏并抛 `CorruptedDemoError`）。
- 其它失败信息也走 stderr，退出码非 0。

### 3.4 示例

```bash
# 输出紧凑 JSON 到指定文件
csda -demo-path=/tmp/match.dem -output=/tmp/match.json -format=json -minify

# 输出到目录（自动命名为 <demo名>.json）
csda -demo-path=/tmp/match.dem -output=/tmp/out -format=json
```

---

## 4. 输出结构（`-format=json`，根为 `Match`）

### 4.1 Match 根对象

`checksum, game`(csgo/cs2/cs2lt)`, demoFilePath, demoFileName, source, type`(GOTV/POV)`, mapName, shareCode, tickCount, tickrate, framerate, date, duration, serverName, clientName, networkProtocol, buildNumber, gameType, gameMode, gameModeStr, isRanked, maxRounds, overtimeCount, hasVacLiveBan, teamA, teamB, winner, players`(map: steamId → Player)`, kills, shots, rounds, clutches, bombsPlanted, bombsDefused, bombsExploded, bombsPlantStart, bombsDefuseStart, playersFlashed, grenadePositions, infernoPositions, hostagePickUpStart, hostagePickedUp, hostageRescued, hostagePositions, smokesStart, decoysStart, heGrenadesExplode, flashbangsExplode, grenadeBounces, grenadeProjectilesDestroy, chickenPositions, chickenDeaths, damages, playerPositions`(仅 `-positions`)`, playersBuy, playerEconomies, chatMessages`

**Team**：`name, letter, score, scoreFirstHalf, scoreSecondHalf, currentSide`

**阵营/`side` 字段在 JSON 里是整数枚举**（Go `common.Team`）：`0` 未分配 / `1` 观战 / `2` T / `3` CT。
击杀、伤害、回合、经济里的 side 都是数字，映射时按此转成 `"T"`/`"CT"`/`""`。

### 4.2 Player（已算好的字段）

标识：`steamId, userId, name, score, team, mvpCount, rankType, rank, oldRank, winCount, crosshairShareCode, color, inspectWeaponCount`

计数：`killCount, deathCount, assistCount, killDeathRatio, kast`(0–100 百分数)`, bombDefusedCount, bombPlantedCount, healthDamage, armorDamage, utilityDamage, headshotCount, headshotPercent, oneVsOne..oneVsFive`(各含 `Count/WonCount/LostCount`)`, hostageRescuedCount`

**指标（直接可用）**：`averageKillPerRound`(KPR)`, averageDeathPerRound`(DPR)`, averageDamagePerRound`(ADR)`, utilityDamagePerRound, firstKillCount, firstDeathCount, firstTradeDeathCount, tradeDeathCount, tradeKillCount, firstTradeKillCount, oneKillCount..fiveKillCount, hltvRating`(1.0)`, hltvRating2`(2.0)

### 4.3 事件表（结构要点）

- **Kill**：`frame, tick, roundNumber, weaponType, weaponName, killerName, killerSteamId, killerSide, killerTeamName, killerX/Y/Z, is_killer_airborne, is_killer_blinded, isKillerControllingBot, victimName, victimSteamId, victimSide, victimTeamName, victimX/Y/Z, is_victim_airborne, is_victim_blinded, isVictimControllingBot, isVictimInspectingWeapon, assisterName, assisterSteamId, assisterSide, assisterTeamName, assisterX/Y/Z, isAssisterControllingBot, isHeadshot, penetratedObjects, isAssistedFlash, isThroughSmoke, isNoScope, isTradeKill, isTradeDeath, distance`（**含自杀与 C4 炸死**）
- **Damage**：`frame, tick, roundNumber, healthDamage, armorDamage, attackerSteamId, attackerSide, attackerTeamName, isAttackerControllingBot, victimHealth, victimNewHealth, victimArmor, victimNewArmor, victimSteamId, victimSide, victimTeamName, isVictimControllingBot, hitgroup, weaponName, weaponType, weaponUniqueId`
- **Round**：`number, startTick/Frame, freezeTimeEndTick/Frame, endTick/Frame, endOfficiallyTick/Frame, overtimeNumber, teamAName, teamBName, teamAScore, teamBScore, teamASide, teamBSide, teamAEquipmentValue, teamBEquipmentValue, teamAMoneySpent, teamBmoneySpent`（**注意实测 JSON key 是小写 `m`：`teamBmoneySpent`**）`, teamAEconomyType, teamBEconomyType, duration, endReason, winnerName, winnerSide, teamAStartMoney, teamBStartMoney`
- **Clutch**：`frame, tick, roundNumber, opponentCount, side, hasWon, clutcherSteamId, clutcherName, clutcherSurvived, clutcherKillCount`
- **PlayerEconomy**：`roundNumber, name, steamId, startMoney, moneySpent, equipmentValue, type, playerSide`
- **PlayerBuy**：`frame, tick, roundNumber, playerSteamId, playerSide, playerName, weaponName, weaponType, weaponUniqueId, hasRefunded`
- **PlayerFlashed**：`frame, tick, roundNumber, duration, flashedSteamId, flashedName, flashedSide, isFlashedControllingBot, flasherSteamId, flasherName, flasherSide, isFlasherControllingBot`
- **Shot**：`frame, tick, roundNumber, weaponName, weaponId, projectileId, x, y, z, playerName, playerSteamId, playerTeamName, playerSide, isPlayerControllingBot, playerVelocityX/Y/Z, yaw, pitch, recoilIndex, aimPunchAngleX/Y, viewPunchAngleX/Y`
- **PlayerPosition**（`-positions`）：`frame, tick, roundNumber, isAlive, name, steamId, x, y, z, yaw, pitch, flashDurationRemaining, side, money, health, armor, hasHelmet, hasBomb, hasDefuseKit, isDucking, isAirborne, ...`
- **HeGrenadeExplode**：`frame, tick, roundNumber, grenadeId, projectileId, x, y, z, throwerSteamId, throwerName, throwerSide, throwerTeamName, throwerVelocityX/Y/Z, throwerPitch, throwerYaw`
- **ChatMessage**：`frame, tick, roundNumber, message, senderSteamId, senderName, senderSide, isSenderAlive`

---

## 5. 内置指标口径（csda 源码）

```
rounds = len(match.Rounds)

ADR   = HealthDamage / rounds
KPR   = KillCount   / rounds        // averageKillPerRound
DPR   = DeathCount  / rounds
KAST% = 有(击杀 | 助攻 | 存活 | 被换掉)的回合数 / rounds × 100   // 返回 0–100
Impact = 2.13*KPR + 0.42*APR - 0.41        // APR = AssistCount/rounds
Rating2.0 = 0.0073*KAST% + 0.3591*KPR - 0.5329*DPR + 0.2372*Impact + 0.0032*ADR + 0.1587
Rating1.0 = (KPR/0.679 + 0.7*((rounds-Deaths)/rounds)/0.317 + Σ(n²·nKillCount)/rounds/1.277) / 2.7
// Rating 1.0/2.0 结果 < 0 时截断为 0

HealthDamage = Σ max(0, damage.HealthDamageTaken)
   over 满足 isValidPlayerDamageEvent 的伤害：攻击者==本人、非自伤、victim != 0、
   非控制 BOT、攻守不同边
```

要点：

- **ADR 用的是原始每次命中的 healthDamage，不做武器加权（不做 overflow 折减）。**
- **KAST 返回的是百分比 0–100**，不是 0–1。
- 换人判定/首次击杀等指标都在 Player 上直接给好了。
- Rating 是**社区逆向拟合的近似公式**（非 HLTV 官方）。

---

## 6. csda 输出 → 雷达 8 项指标的映射

| 指标 | csda 是否直接提供 | 说明 |
|---|---|---|
| ADR | ✅ `averageDamagePerRound` | 原始伤害/回合 |
| KPR | ✅ `averageKillPerRound` | |
| KAST | ✅ `kast` | 注意是 0–100，需 /100 |
| Surviving | ❌ 需推导 | `(rounds - deathCount) / rounds * 100`（0~100） |
| Impact | ❌ 需推导 | `2.13*KPR + 0.42*(assistCount/rounds) - 0.41` |
| Multi-Kill | ⚠️ 近似 | `rounds 内 ≥2 杀的回合占比 × 100`（0~100）= `(twoKillCount+threeKillCount+fourKillCount+fiveKillCount)/rounds * 100` |
| Rating 2.0 | ✅ `hltvRating2` | |
| Rating 1.0 | ✅ `hltvRating` | |
| Rating 3.0 | ❌ **不提供** | 需自己实现 |
| Round Swing | ❌ **不提供** | 需自己实现 |

推导用的 `rounds` = `match.rounds.length`。

---

## 7. C# 调用示例（进程方式）

```csharp
using System.Diagnostics;
using System.Text.Json;

static string RunCsda(string demoPath, string outputJson, string csdaExe)
{
    var psi = new ProcessStartInfo
    {
        FileName = csdaExe, // 例如 ./bin/osx-arm64/csda  或  csda.exe
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    psi.ArgumentList.Add($"-demo-path={demoPath}");
    psi.ArgumentList.Add($"-output={outputJson}");
    psi.ArgumentList.Add("-format=json");
    psi.ArgumentList.Add("-minify");

    using var p = Process.Start(psi)!;
    string err = p.StandardError.ReadToEnd();
    p.WaitForExit();

    if (p.ExitCode != 0)
        throw new InvalidOperationException(
            err.Contains("ErrUnexpectedEndOfDemo")
                ? "Demo 损坏或截断。"
                : $"csda 分析失败（exit {p.ExitCode}）：{err}");

    return File.ReadAllText(outputJson);
}
```

反序列化：用 `System.Text.Json` 建一个与第 4 节字段对应的 DTO（字段名就是 JSON key；Go 侧是小驼峰/蛇形，见各表）。

> 若要跨平台随应用分发：把 `bin/<rid>/csda` 打进输出目录，运行时按 `RuntimeInformation` 选对应文件。

---

## 8. 健壮性建议

- **超时**：大 demo（带枪线/位置）可能跑几十秒到一两分钟，给进程加超时并在超时后 kill。
- **隔离**：用系统临时目录做 `-output`，避免污染用户目录；解析完删除。
- **并发**：每个进程处理一个 demo，可并行多个进程（互不共享状态）。
- **不要开 `-positions`**：除非确实要逐 tick 轨迹，否则输出体积会暴涨。

---

## 9. 已知缺口 / 注意事项

1. **没有 Rating 3.0、没有 Round Swing** —— 这两个必须另做（Cs2Demo.Analytics 目前也没提供官方 3.0）。
2. **没有武器加权伤害**：csda 的 ADR 是原始 healthDamage/回合。想要"武器对位加权 ADR"（如 CSStatsRadar 的 Rating 3.0 eADR），csda 的字段不够：击杀事件里**没有受害者的当前武器、没有攻击者护甲**（伤害事件只有受害者护甲）。
3. **KAST 是百分数**，别当成 0–1 用。
4. **Kill 表含自杀与 C4/世界伤害死亡**，自己按指标口径过滤。
5. **Rating 是本地近似**，非 HLTV 官方，展示时要标注。
6. 输出里的时间字段是 tick/frame，要自己按 `tickrate` 换算秒。

---

## 10. 许可

csda 为 **MIT License**（Copyright 2023-present AkiVer）。
分发二进制时保留 `LICENSE` / 版权声明即可；商用、二次分发、包装成自己的 API 都没问题。
