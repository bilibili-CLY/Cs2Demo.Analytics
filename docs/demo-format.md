# Demo 解析笔记（csda 映射）

解析入口：`src/Cs2Demo.Analytics/Parsing/DemoAnalyzer.cs` → `CsdaRunner`（起 csda 进程）
→ `CsdaMatchMapper`（JSON → 本库模型）。本文件记录“csda 输出 → 本库模型”的映射关系，
与代码互相印证。csda 本体见 `docs/csda-integration.md`。

## 调用 csda

```
csda -demo-path=<demo> -output=<临时目录> -format=json -minify
```

- `-output` 传**已存在的目录**（本库固定这么用）；传**已存在的文件**也能写进去，
  传**不存在的路径**才会报 `invalid output provided`。输出文件名 = `<demo 文件名>.json`。详见 `docs/csda-integration.md` §3.1。
- 随包二进制是 csda **v1.14.1 + ADR 口径补丁**（0.4.1 起；补丁与对账见 `docs/csda-integration.md` §9.1）。
- 退出码 0 成功 / 1 分析失败 / 2 参数错误。
- ⚠️ **csda 对截断 demo 是容忍的**：照样 exit 0、照样出 JSON，只是回合/击杀数残缺（✅实测见
  `docs/csda-integration.md` §3.2）。所以本库那条「stderr 含 `ErrUnexpectedEndOfDemo` → 抛损坏异常」
  基本不会触发，残缺数据不会被自动拦下。
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
| `rounds[].teamAEconomyType / teamBEconomyType` | `RoundInfo.TEconomyType / CTEconomyType` | `pistol` / `eco` / `semi` / `force-buy` / `full`（五档，✅golden 全出现）；仅原样带出，不参与计算 |
| `rounds[].teamAMoneySpent / teamBmoneySpent` | （未入模型） | 注意键名大小写怪癖：`teamBmoneySpent`（小写 m） |
| `bombsPlanted[]` | `RoundInfo.BombPlantedTick` | 按回合取第一次下包的 tick（0 表示没下包） |
| `bombsDefused[]` | `RoundInfo.DefuserSteamId` | 拆包者 |
| `playerEconomies[]` | `SurvivalInfo.EquipmentValue / EconomyType` | 每回合每人的开局买装价值（pistol 1050 / eco 400 / full 5100）与经济类型 |
| `kills[].weaponType / isHeadshot / isAssistedFlash` | `KillEvent.WeaponType / IsHeadshot / IsAssistedFlash` | 武器类别、爆头、闪光助攻 |
| `playerEconomies`，加入对枪双方装等 | `KillEvent.KillerEquipmentValue / VictimEquipmentValue` | 击杀者/被击杀者本回合开局装备价值 |
| `damages[].victimArmor` | `DamageEvent.VictimArmor` | 命中时对方护甲 |
| `clutches[]` | `MatchData.Clutches`（`ClutchInfo`） | 残局：`opponentCount / side / hasWon / clutcherSteamId / clutcherSurvived` |
| `tickrate / maxRounds` | `MatchMeta.TickRate / MaxRounds` | 用于炸弹不可逆阶段判断与加时赛识别；缺失时为 0 |
| `source / serverName / date / duration / tickCount` | `MatchMeta.Source / ServerName / MatchDate / DurationSeconds / TickCount` | duration 为纳秒转秒；date 容错解析，非法为 null；date 实测取文件修改时间，不是真实比赛时间 |

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
  **3.2.0 起武器名参与计算**：`StatsCalculator.TierForWeapon` 用击杀武器名定六档装备，
  只有拿不到武器才回退到装备价值档位（`TierForEquipment`）——未列入六档的名字
  （刀、投掷物、C4、`World`、机枪 M249/Negev、Zeus x27、`UNKNOWN`）都走回退。
  `weaponType` 与 `EconomyType` 仍只原样带出、不参与计算；
  击杀的 `IsAssistedFlash` 已用于 RoundSwing 的贡献分摊。

## 异常与兜底

- 超时、找不到 csda、csda 退出码非 0：抛 `InvalidOperationException`
  （消息带原因；找不到二进制时提示用 `DemoAnalyzer.CsdaExecutablePath`）。
- ⚠️ **demo 损坏/截断不会抛异常**：✅实测 csda 对截断 demo 照样 exit 0、照样出 JSON，只是回合/击杀残缺
  （stderr 为空，`ErrUnexpectedEndOfDemo` 分支基本不会触发）。残缺数据**不会被本库拦下**，
  属已知风险，细节见 `docs/csda-integration.md` §3.2。
- 来源无法自动识别、且调用方没传 `-source` 时，csda 会直接失败（本库不传 `-source`，见 §3.2）；
  这类失败会走上面的「退出码非 0」分支。
- 解析后玩家数为 0 或有效回合数为 0：抛 `InvalidOperationException`。
- `Meta.MapName` 取 csda 的 `mapName`；`Meta.FileSha256` 由 `ParseFile` 本地计算。
- 头部字段（`source` / `serverName` / `date` / `duration` / `tickCount`）任何缺失或异常都只降级为空值
  （`""` / `0` / `null`），不抛异常；解析成功与否仍由玩家/回合校验决定。
- ⚠️ `date` 不是 demo header 里的比赛时间：实测 csda 取的是**文件修改时间**
  （改文件 mtime 后输出随之变化），下载/拷贝到新机器后会变，不能当作真实比赛时间使用。
