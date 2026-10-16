# Demo 解析笔记（csda 映射）

解析入口：`src/Cs2Demo.Analytics/Parsing/DemoAnalyzer.cs` → `CsdaRunner`（起 csda 进程）
→ `CsdaMatchMapper`（JSON → 本库模型）。本文件记录“csda 输出 → 本库模型”的映射关系，
与代码互相印证。csda 本体见 `docs/csda-integration.md`。

## 调用 csda

```
csda -demo-path=<demo> -output=<临时目录> -format=json -minify
```

- `-output` 必须是**已存在的目录**（传文件路径会报错）；输出文件为 `<demo 文件名>.json`。
- 退出码 0 成功 / 1 分析失败 / 2 参数错误。
- 失败且 stderr 含 `ErrUnexpectedEndOfDemo` → 本库抛 `InvalidOperationException`（demo 损坏/截断）。
- 超时 10 分钟；进程启动失败（找不到/无执行权限）也包成 `InvalidOperationException`。

## 二进制定位（`CsdaBinaryLocator`）

按当前平台挑 `csda/<rid>/csda`（Windows 为 `csda.exe`）：
`win-x64` / `osx-x64` / `osx-arm64` / `linux-x64` / `linux-arm64`。
依次找 `AppContext.BaseDirectory/csda/<rid>/…`、`…/runtimes/<rid>/native/…`、程序集目录下同样两项；
都没有则抛 `FileNotFoundException`。也可用 `DemoAnalyzer.CsdaExecutablePath` 显式指定。
非 Windows 平台启动前会补执行位（`File.SetUnixFileMode`），因为包解压可能丢 `+x`。

## JSON 结构

- `players`：**对象/字典，键是 steamId 字符串**（`ulong`，超过 2^53 必须按整数解析，
  否则精度丢失）。每个值是 csda 算好的聚合（kill/death/assist、kast、healthDamage、
  平均 KPR/DPR/ADR、hltvRating/hltvRating2、1~5 连杀回合数、`team` 等）。
- `kills` / `damages` / `rounds` / `playerEconomies`：数组。
- 阵营是整数枚举 `common.Team`：`0` 未分配 / `1` 观战 / `2` T / `3` CT。
  击杀、伤害、回合、经济里的 side 字段都是数字。
- 武器名（`weaponName`）csda 已给**显示名**（`AK-47`、`HE Grenade`、`Knife`、`World` 等），
  `WeaponNames.Normalize` 做了显示名反查，保证与旧口径一致。

csda **不提供**：Impact、Surviving、每回合存活表、Rating3.0、RoundSwing。
前三者本库用原料派生，后两者本库自算。

## 经济 / 武器 / 下包 / 残局映射（0.3.0 起）

Rating30 的经济加权与 Swing v2 需要这些字段，`CsdaMatchMapper` 已把它们映射进模型：

| csda 字段 | 本库字段 | 说明 |
|---|---|---|
| `rounds[].teamAEquipmentValue / teamBEquipmentValue` | `RoundInfo.TEquipmentValue / CTEquipmentValue` | 队伍**开局总**装备价值（5 人求和，如 4250）；按 `teamASide/teamBSide` 判断哪队是 T |
| `rounds[].teamAEconomyType / teamBEconomyType` | `RoundInfo.TEconomyType / CTEconomyType` | `pistol` / `eco` / `full` |
| `rounds[].teamAMoneySpent / teamBmoneySpent` | （未入模型） | 注意键名大小写怪癖：`teamBmoneySpent`（小写 m） |
| `bombsPlanted[]` | `RoundInfo.BombPlantedTick` | 按回合取第一次下包的 tick（0 表示没下包） |
| `bombsDefused[]` | `RoundInfo.DefuserSteamId` | 拆包者 |
| `playerEconomies[]` | `SurvivalInfo.EquipmentValue / EconomyType` | 每回合每人的开局买装价值（pistol 1050 / eco 400 / full 5100）与经济类型 |
| `kills[].weaponType / isHeadshot / isAssistedFlash` | `KillEvent.WeaponType / IsHeadshot / IsAssistedFlash` | 武器类别、爆头、闪光助攻 |
| `playerEconomies`，加入对枪双方装等 | `KillEvent.KillerEquipmentValue / VictimEquipmentValue` | 击杀者/被击杀者本回合开局装备价值 |
| `damages[].victimArmor` | `DamageEvent.VictimArmor` | 命中时对方护甲 |
| `clutches[]` | `MatchData.Clutches`（`ClutchInfo`） | 残局：`opponentCount / side / hasWon / clutcherSteamId / clutcherSurvived` |

`playerEconomies` 覆盖到每回合每人（golden demo 为 10 人 × 18 回合 = 180 条），是存活表和经济加权的共同数据源。

## 过滤规则（`CsdaMatchMapper`）

1. **击杀**：`victimSteamId == 0` 丢掉；自杀（killer==victim）丢掉；
   队杀/世界击杀（killer 为 0 或同阵营）记为**只记死亡的条目**（凶手为空，不记 K）。
2. **伤害**：`attacker==0` / `victim==0` / `attacker==victim` / 同阵营（数字相等）/
   攻击者是 BOT 控制的，全部丢掉；其余取 `max(0, healthDamage)`。
   这套过滤与 csda 算 `healthDamage` 时一致，逐人求和能对上。
3. **热身**：csda 已剔除；未打完的回合 csda 也会丢掉。

## 存活表（本库派生）

csda 没有逐回合存活表，本库用 `playerEconomies` + 死亡名单反推：
每回合先收集所有击杀的受害者集合；对经济记录里的每个玩家（steamId≠0），
不在受害者集合中即 `Survived = true`。阵营取该条的 `playerSide`。
（正好 10 人 × 回合数条，golden demo 为 180 条。）

## 阵营与武器

- 阵营映射：`2 → "T"`、`3 → "CT"`、其他 `""`（未知）。
- 武器归一化（`WeaponNames`）：csda 显示名反查回规范名；未知非空名字记 `UNKNOWN`。
  武器名 / 类型目前不参与计算（经济加权的难度只看装备价值），记下来是为以后免 breaking change；
  击杀的 `IsAssistedFlash` 已用于 RoundSwing 的贡献分摊。

## 异常与兜底

- demo 损坏/截断、超时、找不到 csda、csda 退出码非 0：抛 `InvalidOperationException`
  （消息带原因；找不到二进制时提示用 `DemoAnalyzer.CsdaExecutablePath`）。
- 解析后玩家数为 0 或有效回合数为 0：抛 `InvalidOperationException`。
- `Meta.MapName` 取 csda 的 `mapName`；`Meta.FileSha256` 由 `ParseFile` 本地计算。
