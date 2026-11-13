# csda Rating 3.0 新算法实现文档（HLTV 形状 · 系数自拟合）

> 目标读者：**Cs2Demo.Analytics 仓库**（本仓库是对接方 CSPlayerStatsRadar，只消费 `Cs2Demo.Analytics` 0.3.0 的公开 API）。
> 目标：把现在 `StatsCalculator.Calculate30` 里的「只有装备差 + 固定 T 惩罚」的本地近似，替换为**形状与 HLTV Rating 3.0 一致、系数用自有语料自拟合**的实现。
> 交付边界：本文只描述**算法与工程实现**，不含 UI。应用侧已确认的约束见 §2.2。
> 版本：v1 · 2026-10-18。

---

## 1. 背景：现状是什么，问题在哪

### 1.1 现在的口径（0.3.0，反编译自 `Cs2Demo.Analytics.dll`，64000 字节）

`StatsCalculator.Calculate30(data, steamId)` 的流程：

```csharp
(eKpr, eAdr) = ComputeEcoMetrics(data, steamId);
EKpr = Round3(eKpr);  EAdr = Round3(eAdr);
Rating30 = Round3(max(0,
    0.148393 * (EKr / 0.62724)
  + 0.223011 * (EAdr / 69.72221)
  + 0.083448 * (surviving / 0.337601)
  + 0.322636 * (kastPercent / 100 / 0.723368)
  + 0.135395 * (multiKillShare / 0.152553)
  + 0.161542 * (1 + roundSwingShare / 0.038818)));
```

`ComputeEcoMetrics` 的全部逻辑（唯一的经济调整）：

```csharp
// 1) 每回合每人的初始装备总值（csda 的 playerEconomies.equipmentValue）
// 2) 对每个"我造成"的击杀：w = EcoDifficulty(DuelWinProb(我的装备, 对手装备, 我是否 T))
// 3) 对每个"我造成"的伤害：按事件分别算 w，累加 damage * w
// 4) 返回 (Σw / 回合数, Σ加权伤害 / 回合数)

DuelWinProb(eOwn, eFoe, ownIsT)
    = σ(-0.08 + 0.5124 * (eOwn - eFoe) / 1000.0 - 0.0905 * (ownIsT ? 1 : 0));
EcoDifficulty(p) = (1.0 - p) / 0.5;
```

**问题清单**（与新目标逐条对应）：

| # | 现状 | 与 HLTV 的差距 |
|---|---|---|
| 1 | 对枪胜率只看**装备总价差** + 固定 `-0.0905` 的 T 惩罚 | HLTV 用**地图 + 阵营 + 双方装备**，并装备分档 |
| 2 | 无**地图**先验 | HLTV 的地图专属先验是其模型的核心 |
| 3 | 只有 kills / damage 两项被调整 | HLTV 对 **Kills / Damage / Survival / KAST / Multi-Kill 五项**都做经济调整 |
| 4 | 归一化用硬编码常量（`0.62724` 等） | 应改为**从参考语料统计**得到，可随模型版本重算 |
| 5 | 合成权重固定、Swing 占 0.1615 | HLTV 2025-10 再平衡后目标为「输出 ≈60 / 成本 ≈40」，Swing 占比下调 |
| 6 | Swing 的回合胜率模型不含地图 | HLTV 的 Swing 基于「地图 + 阵营 + 当局经济」的胜率 |
| 7 | 系数不可解释、不可复现（无来源语料） | 需要可复现的训练流水线与制品 |

### 1.2 目标 / 非目标

**目标**

- 六个子评分（Kills、Damage、Survival、KAST、Multi-Kill、Round Swing）**全部做经济调整**，合成一个 Rating 3.0。
- 对枪胜率与回合胜率模型**感知地图、阵营、双方装备**（含装备分档与武器类别）。
- 系数**自拟合**：离线训练器产出 `model.json`，运行时只做求值（不训练），保证确定性。
- 标定到「人群均值 = 1.00」，且能复现 HLTV 公开的若干锚点数字。

**非目标**

- 不追求与 HLTV 官方数值相等（其先验与权重未公开、语料量级差几个数量级）。
- 不引入外部依赖/网络调用；不改变解析（`csda`）本身。
- 不修改应用侧的显示口径（见 §2.2）。

---

## 2. 需求来源与对接约束

### 2.1 HLTV Rating 3.0 的公开设计（2025-08-20 上线，2025-10 再平衡）

- 结构：**「做过经济调整的 Rating 2.1」+ 全新的 Round Swing**；**Impact 被删除**，由 Round Swing + Multi-Kill 共同承担。
- 六个子评分：**Kills、Damage、Survival、KAST、Multi-Kills、Round Swing**。
- 经济调整口径（HLTV 官方措辞）：
  - *Kill, Damage, Survival rating are all eco-adjusted based on the win-rate of a duel based on the map, side, and equipment of the individuals involved.*
  - *KAST and Multi-Kill ratings are eco-adjusted based on the probability of a given action taking place given the map, side, equipment of the individual, and average equipment value of the opposing team.*
- 装备分**六档**：Sniper / tier-one rifles / tier-two rifles / SMGs & shotguns / upgraded pistols / starter pistols。HLTV 说明只有 **44.7%** 的对枪发生在同档之间。
- 公开锚点数字（用于标定与回归测试）：
  - T 方步枪 vs 步枪（胜率 ≈48%）的一次击杀 ≈ **1.10 点**。
  - 打死持初始手枪的对手（胜率 75%）≈ **0.54 点**；无甲击杀远低于 1。
  - AWP 优势对枪（AWP 侧 T ≈56% / CT ≈60%）**被下调**。
  - Swing：开局首杀 ≈ **+20%**；1v1 残局 ≈ **+50%**；若对手满 eco（胜率 96%），全队整局最多只能拿到约 **+4%**（而 50/50 的回合可达 +50%）。
  - 顶级选手赛季 ≈ **+3.7~3.8%**，一般选手 ±1.5%；整体**零和**。
- 再平衡（2025-10）：Swing 权重下调（初期约 40% 的 rating，击杀仅约 12%），把 Kills/Damage 拉回中心，目标回到 2.0 的「**输出 ≈60 / 成本 ≈40**」结构；回合末 Swing 残值分配改为 **1 份额残局 + 2 份额正向击杀（WPA）+ 1 份额拆包或补枪 + 1 份额存活**。

### 2.2 应用侧（CSPlayerStatsRadar）的对接约束 —— 必须遵守

1. `StatsCalculator.Calculate` / `Calculate30` 的**签名与既有字段语义**保持不变；应用侧只读这些字段：
   `Rating20`、`Rating30`、`RoundSwing`（百分点/回合）、`MultiKill`（0~100 的**多杀回合占比**）、`Kast`（0~100）、`Surviving`（0~100）、`Kpr`、`Adr`、`Version`。
2. **`Kpr`/`Adr`/`Kast`/`Surviving` 必须继续是 csda 的原始口径**，不得改成经济调整值。
   应用侧已有一条不变式：**Rating 3.0 剖面里显示的 KPR/ADR 必须与 Rating 2.0 完全相同**（用户切版本时这两个数字不允许变化）；`impact2.13`/`Rating20` 也不得改变。
3. `MultiKill` 必须保持「单回合 ≥2 杀的回合 ÷ 全部回合 × 100」这一**占比口径**（应用侧另有自己的本地实现做交叉校验）。
4. 经济调整后的 eKPR / eADR / eKAST / eSurviving **只作为 Rating 的内部输入**，不要求应用侧展示；可以新增字段（建议名为 `EKpr`/`EAdr`/`EKast`/`ESurviving`）供调试与将来的明细面板使用。
   ⚠️ 应用侧**当前不再读取** `PlayerMatchStats30.EKpr/EAdr`（一次已修复的口径问题：它们曾被当作 KPR/ADR 展示，导致「切到 3.0 数值变小」），因此本仓库可以安全地重定义这两个字段的数值含义；但**不要**把它们再塞回 `Kpr`/`Adr`。
5. `Rating30Version` / `Rating20Version` 是 `const string`，会被 MSBuild **内联进调用方程序集**。升级版本号后应用侧必须 `dotnet clean` 再跑测试，否则会看到「假的回归」。
6. 新字段必须可空或带默认值；`MatchData` 中的缺失数据（老 demo、缺伤害表、缺经济表）必须走降级路径而不是抛异常。
7. 加时赛回合、热身回合、BOT、自杀/队杀/世界击杀（`KillEvent.AttackerSteamId == ""`）都要有明确处理（见 §3.3）。

---

## 3. 数据契约

### 3.1 输入字段（全部来自现有 `MatchData`，无需新增解析）

| 用途 | 字段 |
|---|---|
| 地图 | `MatchMeta.MapName`（如 `de_mirage`，取不到为 `""`） |
| 回合 | `RoundInfo.RoundNumber/StartTick/EndTick/WinnerSide/TEquipmentValue/CTEquipmentValue/TEconomyType/CTEconomyType/BombPlantedTick/DefuserSteamId` |
| 击杀 | `KillEvent.RoundNumber/Tick/AttackerSteamId/VictimSteamId/AssisterSteamId/IsTradeKill/IsTradeDeath/AttackerSide/VictimSide/Weapon/WeaponType/IsHeadshot/IsAssistedFlash/KillerEquipmentValue/VictimEquipmentValue` |
| 伤害 | `DamageEvent.RoundNumber/AttackerSteamId/VictimSteamId/Damage/AttackerSide/VictimSide/Weapon/VictimArmor` |
| 存活/经济 | `SurvivalInfo.RoundNumber/SteamId/Survived/Side/EquipmentValue/EconomyType` |
| 残局 | `ClutchInfo.RoundNumber/SteamId/OpponentCount/Side/Won/Survived/KillCount` |
| 既有聚合值 | `PlayerCsdaStats.*`（仅作校验用，不参与新模型） |

### 3.2 已知缺口与近似处理

| 缺口 | 影响 | 处理 |
|---|---|---|
| `KillEvent` 没有**受害者武器** | 无法做真正的「武器 vs 武器」对位 | 用受害者的 `VictimEquipmentValue` → **装备档位**作为代理；如该回合存在命中该受害者的 `DamageEvent`，可用其 `Weapon` 兜底 |
| `DamageEvent` 没有 `Tick` | 无法重建逐事件的装备/存活状态 | 伤害一律用**回合初始装备**（与 `KillEvent` 的装备字段同源，口径一致） |
| 没有「打空枪/未命中的对枪」 | 存在选择偏差（只见结果，不见尝试） | 明确把 `p_duel` 定义为 **P(该次对枪由我方赢下 ∣ 对枪已发生)**，与 HLTV 的措辞一致；镜像采样保证 50/50 平衡（见 §7.1） |
| 部分来源无经济/存活表 | 装备全为 0 | 降级：用 `RoundInfo.*EconomyType`（`pistol/eco/full`）映射到档位中心值；再退化则用全局均值的对称近似（此时经济调整退化为近似无效，不报错） |
| 无地图名 | 无地图先验 | 地图偏移取 0（即全局模型） |
| **常规/加时回合不可区分** | 无法把加时单独打标或单独标定 | csda 的 `maxRounds`（MR12 为 24）**目前被包丢弃**（`MatchMeta` 只有 `MapName`/`FileSha256`）。若将来需要区分，新增可空字段 `MatchMeta.MaxRounds` + 派生 `RoundInfo.IsOvertime = (RoundNumber > MaxRounds)` 即可（`RoundNumber > 24` 即加时）。**默认行为不变：加时回合照常计入**（见 §3.3） |

### 3.3 基础处理约定（必须与现有一致，避免回归）

- 回合集合 = `RoundInfo` 中的**有效回合**；**加时赛回合正常参与统计**（这是现状，也是目标口径，不允许在重构中改变）。
  - 依据：`CsdaMatchMapper.Map` 对 csda 的 `rounds` 数组**无条件全量**`matchData.Rounds.Add(...)`（`RoundNumber = csda.rounds[].number`），没有任何热身/加时/尾部损坏过滤；热身局/刀局本来就不在 csda 的 `rounds` 里，这是 csda 侧的事实而非我们过滤的结果。
  - 分母：`StatsCalculator.Calculate/Calculate30` 用 `int count = data.Rounds.Count` 作为 `Rounds` 与各项分母；`ComputeEcoMetrics` 的 eKPR/eADR 分母、`RecoverKastRounds(kastPercent, count)` 用的都是它。`SwingCalculator` 也按 `data.Rounds` 逐回合，加时回合一样进 swing。
  - 顺带提醒（与本条无关但同源）：`calculateSurvived` 形的 `SurvivedRounds = max(0, Rounds − DeathCount)` 假设玩家参与了全部回合；加时不影响其正确性，但**中途加入/换人/断线重连**会让它漂移。
- 自杀 / 队杀 / 世界击杀：`AttackerSteamId == ""` → **不给任何人记正分**（kills/points/KAST 都不记）；受害者照旧记死亡与 Swing 的负值（沿用现有 Swing 行为）。
- 玩家在某回合的阵营一律取 `SurvivalInfo.Side`（缺失时回落到该回合其击杀事件里的 `AttackerSide`/`VictimSide`）。
- 敌方队伍平均装备 `oppAvgE(我方, r)` = 该回合对方阵营所有玩家 `EquipmentValue` 的**均值**（缺失玩家按 0 计，但若该队伍所有值皆为 0，则视为缺经济数据，走 §3.2 的降级）。
- 玩家回合数 `R` = 该玩家参与的有效回合数；`R == 0` 时全部输出 `null`（保持现有可空语义）。

---

## 4. 概率模型（三个模型）

所有模型都是**逻辑回归**（广义线性、可解释、小样本稳定）。运行时只做求值；系数来自 `model.json`。

### 4.1 对枪胜率 `p_duel`（Kills / Damage / Survival 共用）

**样本与标签**：每条 `KillEvent`（`AttackerSteamId != ""`）产生两条镜像样本 —— 攻方标记为「赢」，守方标记为「输」，特征互换。

**形式**（特征对 a↔b 互换必须是**反对称**的，以满足 `p(a赢b) + p(b赢a) = 1`）：

```
z = β0
  + β_map(map)                                   // 每地图偏移，对 0 收缩
  + β_T * [side(a) == "T"]
  + β_E * clip(E_a - E_b, -C, C) / 1000
  + Σ_k θ_k * ([tier(E_a) == k] - [tier(E_b) == k])   // 六档，和为零约束（去掉参考档）
  + γ_snip * [a 的枪械类别 == sniper]              // 唯一非对称项：AWP 优势对枪下调
  + γ_hs   * [该击杀为爆头]

p_duel = σ(z),  σ(x) = 1 / (1 + exp(-x))
```

- `β0 = -0.08`、`β_E = 0.5124`、`β_T = -0.0905` 作为**初始值**（沿用现口径），训练后由数据决定。
- `clip(E_a - E_b, -C, C)`，默认 `C = 6000`（= 满装备量级），避免极端值主导。
- `γ_snip` 应为负（AWP 击杀更"容易"，故胜率更高、点数更低）。
- 反对称性由 §9.1 的单元测试锁定（交换 a/b 后 `p` 必须严格互补）。

**精度约束**：`clip(p, 0.02, 0.98)` 后再进点数函数，避免极端权重。

### 4.2 动作概率 `p_action`（KAST / Multi-Kill 共用）

**样本与标签**：每个（玩家 p，回合 r）一条样本；标签 = p 在该回合是否拿到 KAST（或是否达成 ≥2 杀）。

**形式**：

```
z = α0
  + α_map(map)
  + α_T * [side(p) == "T"]
  + α_ownTier * tierIndex(E_p,r)                 // 六档序数（0..5）
  + α_oppAvg  * (oppAvgE(p, r) / 1000)
  + α_econ    * econTypeOneHot(TEconomyType, CTEconomyType)   // pistol/eco/full
  + α_round   * (roundNumber 的归一化阶段：0..1)   // 手枪局/半场前后
```

- `p_action` **不要求反对称**（这不是双人对枪），因此可用序数与档位哑变量。
- 两个模型可共用特征与框架，各自单独拟合（KAST 用全部回合，Multi-Kill 用「该回合是否有 ≥2 杀」）。

### 4.3 回合胜率 `p_round`（Round Swing 用）

**样本与标签**：每个「回合状态」一条样本（回合开始时 + 每次击杀后各一个状态）；标签 = 该回合 **T 方获胜**。

**形式**（固定以 T 为参考方，故不要求反对称）：

```
z = ω0
  + ω_map(map)
  + ω_alive  * (aT - aCT)
  + ω_alive2 * (aT - aCT)^2
  + ω_E      * clip(E_T - E_CT, -C, C) / 1000        // 用该状态的当时经济：回合开始取 RoundInfo.*EquipmentValue，
                                                     // 后续状态沿用回合初始值（无逐事件经济数据）
  + ω_bomb   * [已下包]
  + ω_Tstart * [T 为回合开始时的防守方？]              // 视为可选交互项，由训练决定是否保留

p_round = σ(z)
```

- 现口径的对应常量（`σ(-0.05 + 1.0·Δalive + 0.0689·ΔE/1000 + 0.8672·下包)`）可作为初始值。
- 「满 eco 一方整局最多只能拿到约 +4% Swing」这一行为**不需要硬编码**：只要 `ω_E` 拟合得当，开局 `p_round` 就已 ≈0.96，残值自然被压到极小 —— 见 §9.2 的回归测试。
- 残局（`ClutchInfo`）也可以作为附加特征（`1/OpponentCount`）提高拟合质量，但**不要**让它变成标签泄漏（标签是回合胜负，与残局信息同回合）。

---

## 5. 六项子评分（定义）

记：`R` = 玩家回合数；`M` = 由地图名映射得到的偏移（缺失为 0）；`points(p) = κ·(1 − p)/0.5`，**默认 `κ = 1.05`**（对齐 HLTV 锚点，见 §9.2）。

| 子评分 | 公式 | 说明 |
|---|---|---|
| **eKills** | `Σ_{我赢下的对枪} points(p_duel) / R` | 每次击杀按难度给点 |
| **eDamage** | `Σ_{伤害事件} (Damage/100) · points(p_duel(事件装备)) · λ_weapon / R` | `λ_weapon` 默认 1.0（AWP 已由 §4.1 的 `γ_snip` 处理），保留作为标定旋钮。量纲与 ADR 同阶 |
| **eSurvival** | `Σ_{我存活且本回合所属队伍有 ≥1 人阵亡? } points(p_duel(E_p vs oppAvgE)) / R` | 存活于**劣势经济**时权重更高。括号内条件可选（默认不加，即所有存活回合都记） |
| **eKAST** | `Σ_{我拿到 K/A/S/T} points(p_action_KAST) / R` | 易发生的动作（eco 局）权重更低 |
| **eMultiKill** | `Σ_{我该回合 ≥2 杀} base(n) · points(p_action_MK) / R` | `base(n)` 默认 `{2:1.0, 3:1.6, 4:2.4, 5:3.4}`，标定后可调；`n = min(该回合击杀数, 5)` |
| **eSwing** | 见 §5.1（百分点/回合） | 零和 |

**归一化（关键）**：应用侧展示用的 eKPR/eADR/eKAST/eSurviving 建议**线性重标定到与原始指标相同的均值**，便于同轴对照：

```
EAdr_display  =  eDamage  / mean_corpus(eDamage)  * mean_corpus(rawAdr)
```

`mean_corpus(...)` 取自 `model.json`（**参考语料**的统计量，**不是**用户自己的比赛库 —— 否则任何用户自己的比赛均值恒为原始值，失去信息量）。

### 5.1 Round Swing（沿用现有结构，升级胜率模型）

**击杀分值分摊**（占该次击杀造成的胜率变化的份额）：

```
击杀者 0.60 | 伤害贡献 0.25 | 闪光助攻 0.10 | 被交易者 0.05
```

- 伤害贡献的 0.25 按「同队其他玩家对该受害者的伤害」**按比例**分配（排除击杀者与受害者，`Damage > 0`）。
- 闪光助攻：`IsAssistedFlash`。
- 被交易者：同一阵营、在交易窗口内的上一名受害者（`IsTradeKill`；应用侧另有自己的重导出规则，本仓库保持 csda 原值即可）。
- 余数归击杀者；**受害者获得等量负值**。

**回合末残值**：`残值 = (T 胜 ? 1 : 0) − p_round(结束状态)`，按 **1 份额残局 + 2 份额正向击杀（WPA，按玩家正 Swing 占比）+ 1 份额拆包者（`DefuserSteamId`）或补枪参与者 + 1 份额存活者** 在所有参与者中分摊。

**输出**：`RoundSwing = Round3(Σ该玩家全回合 swing × 100 / R)`（百分点/回合）。

**零和**：每一回合、每一阵营内部各自的 swing 之和必须为 0（允许 ±1e-9 浮点误差）；跨阵营按 `p_round` 的定义自动平衡。

---

## 6. 合成与标定

### 6.1 合成公式

```
Rating30 = Round3( max(0, Σ_k w_k · (T_k / M_k) ) )
```

- `T_k` ∈ {eKills, eDamage, eSurvival, eKAST, eMultiKill, (1 + eSwing/SwingScale)}，其中 `SwingScale` 默认 `0.038818`（沿用现常量，标定后调整）。
- `M_k` = **参考语料**上该项的均值（存于 `model.json`）；`(1 + eSwing/SwingScale)` 的原因与现口径一致：Swing 项围绕 1 波动，因此 Swing 项的 `M` 恒为 1（`SwingScale` 只出现在项内）。
- 若某一项的均值 < 1e-9（语料缺项），该项权重转移给 Kills/Damage（按比例），并在 `model.json` 里标记降级。

### 6.2 权重（初始值，由标定微调）

| 子项 | 权重 | 备注 |
|---|---|---|
| Kills | 0.22 | 输出项 |
| Damage | 0.34 | 输出项 |
| Survival | 0.07 | 成本/影响力项 |
| KAST | 0.20 | 成本/影响力项 |
| Multi-Kill | 0.05 | 替代旧 Impact 的一半 |
| Round Swing | 0.12 | 替代旧 Impact 的另一半；**必须显著低于现口径的 0.1615** |

输出项合计 **0.56**、成本/影响力项合计 **0.44**（目标「输出 ≈60 / 成本 ≈40」）。

### 6.3 标定流程（训练结束时执行一次，结果写进 `model.json`）

1. 在**参考语料**上计算六项原始值 → 得到 `M_k`。
2. 用 `M_k` 归一化 → 语料上的 Rating30 均值必然 = `Σw_k` = 1.00。
3. 调 `κ`、`base(n)`、`λ_weapon`、`SwingScale` 使 §9.2 的锚点全部落在容差内。
4. 检查分位数：语料内单图最佳 ≈1.25~1.45、中位 ≈0.98~1.02、最差 ≈0.55~0.75（与 HLTV 的观感区间一致）。
5. 产出标定报告（`calibration.md`）：锚点复现表、分位数表、各项均值/标准差、地图覆盖度、各档位样本数。

---

## 7. 自拟合流程（离线训练器）

> 训练器与运行时**分离**：训练器可放在本仓库的 `tools/RatingFit`（或独立仓库），运行时只加载 `model.json`。

### 7.1 样本构造

| 模型 | 样本 | 标签 | 备注 |
|---|---|---|---|
| `p_duel` | 每条 `KillEvent`（有攻方）×2（镜像） | 攻方赢 / 守方输 | 镜像后类别天然平衡；特征必须反对称（§4.1） |
| `p_action_KAST` | 每个（玩家，回合） | 该回合是否拿到 K/A/S/T | 正例率约 0.6~0.75，轻微不平衡，可用样本权重校正 |
| `p_action_MK` | 每个（玩家，回合） | 该回合是否 ≥2 杀 | 正例率约 0.15，需类别加权（`w_pos = 1/正例率`）或下采样 |
| `p_round` | 每个回合状态（开局 + 每次击杀后） | 该回合 T 是否获胜 | 同一回合的多个状态高度相关 → **按回合分组切分**，不可随机切分（避免信息泄漏） |

### 7.2 拟合与正则

- 求解器：带 L2 的加权逻辑回归（IRLS 或 L-BFGS，固定迭代次数与初值）。
- 分层结构：`β_map = β_map_raw` 但施加 `N(0, τ²)` 先验（等价于 L2，`τ` 默认 0.05~0.15），实现**向全局收缩**；地图样本数 < 200 回合的地图**直接合并进全局**并在报告中标出。
- 缺失地图名 → 只使用全局项。
- 特征标准化：`ΔE` 统一 `/1000`；回合阶段归一化到 0~1。

### 7.3 切分与评估

- **按比赛**切分（例如 80/20，或 leave-one-match-out），**绝不按样本随机切分**。
- 评估指标：对数损失 + Brier + 校准曲线（分桶后 `预测 vs 实际`），并输出各分档（六档装备 × 地图）的实际样本量。
- 语料量参考：全局项约需 **≥ 200 局**（~3600 回合、~25000 条镜像对枪样本）；每地图偏移建议 ≥ 40 局；不足则加大收缩。

### 7.4 确定性

- 固定随机种子；固定迭代次数；无早停的随机性；训练产物为纯 JSON，可按字节比对。
- 运行时**不做任何训练/随机**，保证同一 `model.json` + 同一 demo = 同一结果。

---

## 8. 制品与版本化

### 8.1 `model.json`（建议 schema）

```jsonc
{
  "modelVersion": "hltv-style-1",
  "trainedAt": "2026-10-20T00:00:00Z",
  "corpus": { "matches": 210, "rounds": 4760, "duels": 31400, "maps": { "de_mirage": 41, "de_inferno": 33 } },
  "duel": {
    "intercept": -0.08,
    "equipPer1000": 0.5124,
    "equipClip": 6000,
    "sideT": -0.0905,
    "tierTheta": [0.00, 0.05, 0.10, 0.14, 0.19, 0.26],   // 六档，参考档为 0
    "sniperGamma": -0.12,
    "headshotGamma": 0.0,
    "mapOffsets": { "de_mirage": 0.021, "de_inferno": -0.014, "": 0.0 },
    "mapTau": 0.08,
    "kappa": 1.05,
    "probClip": [0.02, 0.98]
  },
  "action": {
    "kast": { "intercept": 0.9, "sideT": 0.0, "ownTier": 0.05, "oppAvgPer1000": -0.02, "mapOffsets": {}, "tau": 0.08 },
    "multiKill": { "intercept": -2.1, "sideT": 0.0, "ownTier": 0.12, "oppAvgPer1000": -0.03, "base": { "2": 1.0, "3": 1.6, "4": 2.4, "5": 3.4 }, "mapOffsets": {}, "tau": 0.08 }
  },
  "roundWin": {
    "intercept": -0.05, "alive": 1.0, "aliveSq": 0.0, "equipPer1000": 0.0689, "bomb": 0.8672,
    "equipClip": 6000, "mapOffsets": {}, "tau": 0.10
  },
  "normalizers": {
    "eKills": 0.63, "eDamage": 72.4, "eSurvival": 0.71, "eKAST": 0.42,
    "eMultiKill": 0.19, "swing": 0.038818
  },
  "meanAlignment": { "adr": 82.3, "kast": 71.8, "surviving": 33.1, "kpr": 0.67 },
  "weights": { "kills": 0.22, "damage": 0.34, "survival": 0.07, "kast": 0.20, "multiKill": 0.05, "swing": 0.12 }
}
```

- 所有键都必须有**内置默认值**（本节数值即可作为默认），`model.json` 缺失时用默认值运行（保证包在没有语料时也能工作）。
- 未知地图 → `mapOffsets` 取 `""` 或 0。

### 8.2 版本号与字段

- `Rating30Version` 升为 `"3.1.0"`（算法实质变更，避免与旧值混淆）；`Rating20Version` 保持 `"2.0.0"`。
- 新增字段：`EKast`、`ESurviving`（可空）；`EKpr`/`EAdr` 保留字段名但数值改为新模型输出（应用侧已不读取，见 §2.2.4）。
- 可选新增 `Rating30Breakdown`（六项归一化后的原始值 + 权重），便于调试与将来的明细 UI；可空。
- 保留 `MultiKill`、`Kast`、`Surviving`、`Kpr`、`Adr`、`RoundSwing` 的原口径（§2.2）。
- 发布时提醒调用方 `dotnet clean`（`const` 内联陷阱）。

### 8.3 降级矩阵

| 缺失 | 行为 |
|---|---|
| `model.json` 不存在 | 用内置默认系数 |
| 无地图名 | 地图偏移 = 0 |
| 无经济/存活表 | 用 `EconomyType` 映射档位中心值；再退化则忽略经济项（等价于只按阵营/地图） |
| 无伤害表（`Damages` 为空） | `eDamage = null`；其权重按比例转移给 Kills |
| 回合数 = 0 | 全部字段 `null` |

---

## 9. 验收标准与测试

### 9.1 单元测试（公式级，已知答案）

1. `p_duel(a,b) + p_duel(b,a) == 1`（反对称，逐个特征组合遍历）。
2. `points(p=0.5) == κ`；`points` 单调递减。
3. 装备档位映射：对示例装备值/经济类型断言档位（含 `EconomyType` 兜底路径）。
4. `p_round`：`(aT,aCT)=(5,5) 且 ΔE=0` 时 ≈0.5；已下包时显著上升；`(1,5)` 时 <0.05。
5. Swing 分摊：单次击杀时 0.60/0.25/0.10/0.05 的归属与余数；无伤害贡献者时 0.25 归击杀者。
6. **零和**：随机构造一回合，断言 10 名玩家的 swing 之和 `|Σ| < 1e-9`。
7. 边界：自杀/队杀（`AttackerSteamId == ""`）不给正分；缺伤害表不抛异常；`R = 0` 返回 `null`。

### 9.2 锚点回归测试（对齐 HLTV 公开数字，容差 ±0.05）

| 断言 | 期望 |
|---|---|
| T 方步枪 vs 步枪对枪胜率 | 0.45 ~ 0.52 |
| 同上 `points(p)` | 1.05 ~ 1.15 |
| 打死持初始手枪者（p≈0.75）的 `points` | 0.50 ~ 0.60 |
| 满 eco 一方在整局的 swing 上限 | ≤ +5% |
| 5v5 均势下首杀带来的 swing | +0.15 ~ +0.25 |
| 1v1 残局胜利带来的 swing | +0.40 ~ +0.55 |

### 9.3 标定测试

- 参考语料上 `Rating30` 均值 ∈ [0.99, 1.01]；中位数 ∈ [0.96, 1.04]。
- 单地图最佳选手 ∈ [1.20, 1.50]；最差 ∈ [0.50, 0.80]。
- 六项子评分的语料均值与 `model.json` 的 `normalizers` 一致（±1%）。

### 9.4 一致性测试

- 同一 `model.json` + 同一 demo → 逐字段**字节级一致**（无随机）。
- 跨 `Rating30Version` 升级：`Kpr`/`Adr`/`Kast`/`Surviving`/`MultiKill`/`Rating20`/`Impact` **必须不变**（用一份固定 fixture 断言），只有 `Rating30`/`RoundSwing`/新增字段变化。
- 应用侧有真实 demo 的端到端测试（`doc/perfect-world.dem`，de_nuke、10 人、18 回合）：升级包版本后需保证应用侧「KPR/ADR 两剖面一致」的断言仍然通过。

### 9.5 golden 流程

- 更新 `model.json` 时必须同时更新标定报告与回归 fixture；建议把裁决标准写成脚本（`RatingFit --verify`），让「模型变更 → 指标漂移」可审查。

---

## 10. 实施计划（建议排期）

| 阶段 | 内容 | 交付物 | 估计 |
|---|---|---|---|
| P0 | 语料准备：批量跑 csda 存 `MatchData`（建议 JSON/压缩包），做字段健康检查（经济/伤害/存活缺失率、地图分布） | 语料清单 + 健康报告 | 2~3 d |
| P1 | `p_duel` 模型 + 对枪样本流水线 + 反对称测试 | 训练脚本 + 评估报告 | 3~4 d |
| P2 | `p_action`（KAST/Multi-Kill）与 `p_round`（Swing）模型 | 训练脚本 + 评估报告 | 3~4 d |
| P3 | 六项子评分 + Swing 分摊 + 合成 + 标定（`κ`/`base`/权重/`SwingScale`） | `Calculate30` 新版 + `model.json` | 4~5 d |
| P4 | 降级路径、确定性、单元/锚点/标定测试、golden 流程 | 测试套件 + CI 报告 | 3~4 d |
| P5 | 版本号/字段/文档；应用侧回归（`dotnet clean` + 全量测试） | `Rating30Version = 3.1.0` + 迁移说明 | 1~2 d |

合计 **约 3 周**（含语料）。若先只做 P1+P3（对照射的 Kills/Damage/Survival + 标定）可以在 **1 周内**拿到第一版可对比的结果，KAST/Multi-Kill 的调整放 P2。

---

## 11. 风险与开放问题

| 风险 | 说明 | 缓解 |
|---|---|---|
| 选择偏差 | 只见「已发生的对枪」，未见空枪/未命中；`p_duel` 的语义必须写死在文档与测试里 | 明确定义为「对枪已发生条件下的胜率」；镜像采样；不对外宣称是绝对胜率 |
| 无 ground truth | 无法证明「更像 HLTV」 | 以结构一致 + 公开锚点 + 内部标定（均值 1.00、分位数）为验收标准 |
| 地图样本不均 | 冷门图偏移不可靠 | 向全局收缩（`τ`），样本 <200 回合的图合并；报告覆盖度 |
| 受害者武器缺失 | 无法做真武器对位 | 装备档位代理；有 `DamageEvent` 时用其武器兜底 |
| 伤害无 Tick | 无法做逐事件状态 | 统一用回合初始装备；文档说明该近似 |
| 装备/经济数据缺失的老 demo | 经济调整退化 | 三级降级（§8.3），绝不抛异常 |
| 镜像采样导致 `p_duel` 系统性偏高/偏低 | 影响点数绝对值 | `κ` 已在标定中吸收；点数绝对值不影响合成分（各项都被归一化） |
| 权重不可验证 | HLTV 未公开 | 用「60/40 + Swing 上限」约束结构，标定后固定并在报告中记录 |
| 与旧值混用 | 用户库里新旧 `Rating30` 混存 | 应用侧按 `RatingAlgorithm` 版本号识别过期行并提示重算（应用侧任务，不属本文范围） |

**开放问题（需要 Cs2Demo.Analytics 侧决定）**

1. 训练语料从哪来、是否随包分发 `model.json`，还是只内置默认系数？
2. `eSurvival` 是否对「死亡」也记负分（现设计只对存活记正分）？
3. `p_round` 是否加入残局/血量特征（本文只给了 alive/equip/bomb/alive²）。
4. 是否需要在 `PlayerMatchStats30` 上暴露六项归一化明细（`Rating30Breakdown`）？

---

## 附录 A：现有常量（可直接 diff 的清单）

```
// StatsCalculator.Calculate30 / ComputeEcoMetrics（0.3.0）
DuelBeta0        = -0.08
DuelBeta1        =  0.5124      // per 1000 equipment diff
DuelBeta2        = -0.0905      // T-side penalty
EcoDifficulty(p) = (1 - p) / 0.5
AvgEKpr          =  0.62724
AvgEAdr          = 69.72221
AvgSurviving     =  0.337601
AvgKast          =  0.723368
AvgMultiKill     =  0.152553
SwingScale       =  0.038818
WeightKpr        =  0.148393
WeightAdr        =  0.223011
WeightSurviving  =  0.083448
WeightKast       =  0.322636
WeightMultiKill  =  0.135395
WeightSwing      =  0.161542
Rating30         = Round3(max(0, Σ WeightX * (x / AvgX)))   // Swing 项为 (1 + swing/AvgSwing)

// SwingCalculator.Swing（0.3.0）
RoundWinProb = σ(-0.05 + 1.0*(tAlive-ctAlive) + 0.0689*(evT-evCT)/1000 + (bombPlanted ? 0.8672 : 0))
KillerShare = 0.60, DamageShare = 0.25, FlashShare = 0.10, TradeShare = 0.05
ResidualShares = 1(残局) + 2(WPA) + 1(拆包/补枪) + 1(存活)；RoundSwing = Round3(Σswing/R*100)
```

## 附录 B：装备六档与武器类别映射（建议）

| 档位（HLTV 命名） | 建议判定 | 备注 |
|---|---|---|
| Sniper | 武器类别 `sniper`（AWP / SCAR-20 / G3SG1）；装备值 ≥ 5200 | AWP 全价 4750 + 甲 |
| tier-one rifles | AK-47 / M4A4 / M4A1-S / AUG / SG 553；装备值 4000~5199 | |
| tier-two rifles | Galil AR / FAMAS；装备值 3000~3999 | |
| SMGs & shotguns | MAC-10 / MP9 / MP7 / MP5-SD / UMP-45 / P90 / PP-Bizon / Nova / XM1014 / MAG-7 / Sawed-Off；装备值 1800~2999 | |
| upgraded pistols | P250 / Five-SeveN / Tec-9 / CZ75 / Desert Eagle / Dual Berettas / R8；装备值 1000~1799 | 半起常用 |
| starter pistols | Glock-18 / USP-S / P2000；装备值 < 1000 | 手枪局 |

- 受害者武器不可知时，**只用装备值 → 档位**；`RoundInfo.*EconomyType` 可用于兜底（`pistol`→starter、`eco`→upgraded、`full`→tier-one/tier-two，按装备值细分）。
- 手雷/近战/世界击杀：给专用类别，不参与档位热度（击杀点数按 §5 的 `γ` 处理，建议单独一项 `other` 且系数为 0）。

## 附录 C：特征一览（用于实现与代码评审）

| 模型 | 特征 | 编码 | 反对称? |
|---|---|---|---|
| `p_duel` | `ΔE` | `clip(·,±6000)/1000` | ✅ |
| | 攻方是否 T | 0/1 | ❌（固定项，加在 z 上；镜像样本角色互换后仍然成立） |
| | 双方档位 | 六档哑变量的差 | ✅ |
| | 攻方武器是否 sniper | 0/1 | ❌（刻意非对称） |
| | 是否爆头 | 0/1 | ❌ |
| | 地图 | one-hot + 收缩 | ✅（与角色无关） |
| `p_action` | 地图 / 阵营 / 自身档位 / 对手均值装备 / 经济类型 / 回合阶段 | one-hot + 序数 | 不要求 |
| `p_round` | `Δalive`、`Δalive²`、`ΔE`、`下包`、地图 | 数值/0-1 | 不要求（固定 T 为参考） |

## 附录 D：公开来源

| 内容 | 来源 |
|---|---|
| Rating 3.0 上线公告（结构、六项、经济调整口径、装备六档、44.7%、锚点数字） | HLTV news 42485（2025-08-20）；转载与摘要：`pley.gg`、`community.skin.club`、`dust2.us/news/64901`（逐字引用 changelog） |
| 再平衡（Swing 权重下调、60/40、残值份额、eK-eD/eADR/eKAST 列、frozen vs ultimate 例子） | HLTV news 43047（2025-10） |
| 现有实现常量与公式 | 反编译 `Cs2Demo.Analytics.dll` 0.3.0（`StatsCalculator` / `SwingCalculator`） |
| 应用侧另一套本地近似（武器档位加权，仅供参考） | CSPlayerStatsRadar `src/Radar.Application/Rating3.cs`、`Rating3Calculator.cs` |
