# 统计口径

本库的原始数据来自 csda（[akiver/cs-demo-analyzer](https://github.com/akiver/cs-demo-analyzer)）。
**能在 csda 拿到的指标直接用 csda 的值**（KPR / ADR / KAST / Rating2.0 / 连杀计数 / 经济等），
本库只负责把 csda 的原料拼成统一模型，并补算 csda 没有的指标
（Surviving / Impact / MultiKill / eKPR / eADR / Rating30 / RoundSwing）。
实现见 `src/Cs2Demo.Analytics/Stats/StatsCalculator.cs` 与 `Stats/SwingCalculator.cs`。

## 基础量（来源：csda）

- `Rounds`：有效回合数（csda 已剔除热身、未打完的回合）。
- `Kills / Deaths / Assists`：直接取 csda 的 `killCount / deathCount / assistCount`
  （自杀不计；**队杀与 C4 炸死记死亡**，与官方记分板一致；凶手为空，不记 K）。
- `TotalDamage`：csda 的 `healthDamage`——**原始扣血，不折叠溢出**；
  自伤/队伤/世界伤害 csda 已过滤。有回合时恒有值。
- `SurvivedRounds`：`Rounds - Deaths`（csda 死亡数口径）。
- `KastRounds`：由 csda 的 KAST 百分数回推：`round(KAST% / 100 * Rounds)`。

## V1 六项指标

公式版本 `StatsCalculator.Rating20Version`（当前 `"2.0.0"`，见返回数据的 `Version` 字段）。

| 指标 | 公式 | 来源 |
|---|---|---|
| KPR | 直接取 csda `averageKillPerRound`（= `Kills / r`） | csda |
| ADR | 直接取 csda `averageDamagePerRound`（= `HealthDamage / r`） | csda |
| KAST | 直接取 csda `kast`（**0~100 百分数**） | csda |
| Surviving | `SurvivedRounds / r * 100`（0~100，百分数） | 本库派生 |
| Impact | `2.13 * KPR + 0.42 * APR - 0.41`，负数按 0 计（`APR = Assists / r`） | 本库派生 |
| Rating2.0 | 直接取 csda `hltvRating2`（负数按 0 计） | csda |

所有指标保留 3 位小数；`r = Rounds`。零回合时 6 项全 null。

## Rating2.0 的来源（必读）

这是**社区逆向拟合的近似公式，不是 HLTV 官方值**：

- HLTV 在 2017 年上线 Rating 2.0 时只公开了 5 个输入变量
  （KPR、DPR、Impact、ADR、KAST），exact 公式保密，且官方按 CT/T 分边计算。
- flashed.gg（Dave，2021，《Reverse engineering HLTV rating》）抓取 hltv.org 数据做线性回归，
  得到系数：`0.0073*KAST% + 0.3591*KPR - 0.5329*DPR + 0.2372*Impact + 0.0032*ADR + 0.1587`。
  csda 内置的就是这套系数，本库直接取用。
- Impact 中的 `2.13 * KPR + 0.42 * APR` 同样是社区拟合的公式（`-0.41` 为截距项）。

## 与参考仓库的差异

对照 `https://github.com/bilibili-CLY/CSStatsRadar`（只读参考，未复制代码）：

1. **数值口径的来源不同**：本库的 KPR / ADR / KAST / Rating2.0 取自 csda；
   参考仓库用 DemoFile 自算。系数一致，但底层解析口径（伤害是否折叠溢出的处理、
   KAST 回合判定、团队/世界伤害的过滤）以各自实现为准，数值可能有小差异。
2. **Additive 差异（口径变化）**：本库 0.3.0 起 ADR 取 csda 原始扣血（**不折叠溢出**），
   KAST 为 0~100 百分数，与旧版（`"1.0.0"` 口径）不可直接比较。
3. **负数截断一致**：Rating 类指标都做 `max(0, value)` 再保留 3 位小数。
4. **零回合口径一致**：零回合时 6 项全为 null；有回合时全部有值（不再有“缺伤害 → ADR 为 null”）。

# Rating 3.0（本地近似，非 HLTV 官方值）

公式版本 `StatsCalculator.Rating30Version`（当前 `"3.0.0"`，见返回数据的 `Version` 字段）。
状态：**已拟合（经济加权版）**。

HLTV 从未公开 Rating 3.0 精确公式：2025-08-20 首发（`hltv.org/news/42485`）、
2025-10-29 调整（`news/43047`）、2026-10-01 精度更新（`news/45596`）都只给结构，不给系数与胜率表。
本库按官方公开的结构做了**结构对齐的本地近似**：

- Rating 3.0 = 经济调整版 Rating 2.1 + Round Swing，6 个子项：
  **击杀 / 伤害 / 存活 / KAST / 多杀 / Round Swing**（2.0 的 Impact 被 RS + MultiKill 取代）。
- 击杀 / 伤害 / 存活按**对枪胜率难度**做经济加权；KAST / 多杀按“给定地图/阵营/装备下该动作的概率”加权。
- 击杀分按**双方经济 + 是否下包 + 存活人数**算回合胜率变化；回合胜利的 Swing 按
  1 份残局 / 2 份 WPA（建立优势的击杀）/ 1 份拆包或补枪 / 1 份“赢下且存活”分配。
- 官方只公开过锚点：同装备对枪约 48%、一线步枪打初始手枪约 75%、AWP 打步枪 T 56% / CT 60%；
  其余数值（eKPR/eADR 系数、Swing 权重）官方未公开，**本库用锚点 + 10 张 EPL demo 自行标定**。
- 官方 2025-10 把“最后一滴伤害”的权重下调、更重视击杀本身（回到 60/40 输出 vs 成本），
  本库的分摊比（击杀 0.60 / 伤害 0.25 / 闪光 0.10 / 补枪 0.05）是按其描述的**近似**。

入口：`StatsCalculator.Calculate30 / CalculateAll30` → `PlayerMatchStats30`
（9 项：KPR / ADR / eKPR / eADR / KAST / Surviving / MultiKill / RoundSwing / Rating30，
与 Rating20 的类型和方法完全隔开）。零回合 9 项全 null。

## 9 项公式

设 `r = Rounds`；KPR / ADR / KAST / Surviving / MultiKill 与 V1 同定义（见上），另有：

| 指标 | 公式 | 缺数时 |
|---|---|---|
| eKPR | 经济加权 KPR：`Σᵢ 难度ᵢ / r`（对每次敌方击杀 i） | r=0 时 null |
| eADR | 经济加权 ADR：`Σᵢ 伤害ᵢ · 难度ᵢ / r`（对每次敌方伤害 i） | r=0 时 null |
| RoundSwing | 全回合 Swing 总和 `/ r * 100`（百分点/回合，可为负） | r=0 时 null |
| Rating30 | `0.148393*(eKPR/0.627240) + 0.223011*(eADR/69.722210) + 0.083448*(存活率/0.337601)`<br>`+ 0.322636*((KAST/100)/0.723368) + 0.135395*(多杀比率/0.152553) + 0.161542*(1 + Swing均值/0.038818)`，负数按 0 计 | r=0 时 null |

- `存活率` = `SurvivedRounds / r`（0~1）；`多杀比率` = `(2K+3K+4K+5K) / r`（0~1，与对外 `MultiKill%` 同一量）；
  `Swing均值` = 全回合 Swing 总和 / r（胜率小数/回合）。
- 与 Rating20 不同：Rating30 用 **eKPR / eADR**（经济加权）替代原始 KPR / ADR；原始值仍照常输出。

## 经济/武器权重（eKPR / eADR）

csda 提供每回合每名玩家的 `equipmentValue`（开局买入价值），本库用它算对枪难度：

```
p(自己赢) = sigmoid(-0.0800 + 0.5124*(我方装备 - 对方装备)/1000 - 0.0905*(自己是 T ? 1 : 0))
难度 d(p)  = (1 - p) / 0.5      # 五五开 = 1；优势对枪 < 1；劣势对枪 > 1
```

系数按 HLTV 公开锚点标定：同装备 ≈ 48%（CT）、一线步枪(2700) 打初始手枪(400) ≈ 75%（CT）。
击杀计 `d(p)`，伤害计 `伤害 × d(p)`。装备差是 HLTV 六档装备体系的**连续近似**；
样本自拟合的斜率更平（拿 `docs/fit30.py` 的原始回归可见），这里以官方锚点为准。

## Round Swing v2（经济 + 安包 + 贡献分摊 + 残值零和）

每回合从 5v5 起算，按击杀 tick 排序：

```
P_T(tAlive, ctAlive) = sigmoid(-0.05 + (tAlive - ctAlive) + 0.0689*(evT - evCT)/1000 + 0.8672*已下包)
```

- 存活数系数锚定 1.0、偏置 -0.05（保持 5v5 ≈ 48.8%、5v4 ≈ 72.1% 公开示例）；
  经济系数 0.0689、安包系数 0.8672 由 10 张 EPL demo 逻辑回归拟合，前两项固定不变。
  `evT / evCT` 是双方**全队**装备总值（如 4250）。
- 每杀算 `ΔP`（T 视角），击杀方得 `+gain`（gain = 自己队伍视角的胜率增量），
  在自己队伍内按 **击杀 0.60 / 伤害 0.25（按对受害者的伤害占比）/ 闪光助攻 0.10 / 补枪 0.05** 分摊，
  剩余归击杀者；受害者承担等量负值。
- 回合末把 `实际结果 - 模型胜率`（残值）在双方之间**零和**分摊：1 份残局（在 csda `clutches` 里）
  + 2 份正向击杀 swing 占比（WPA）+ 1 份拆包（`defuserSteamId`）或补枪 + 1 份存活，归一化；
  负方拿负份额。**全玩家 swing 之和恒为 0**。
- 阵营未知时按“队伤已过滤、凶手必为对方”反推；两边都未知则该杀不计 Swing。
- 无经济/安包数据时退化为纯存活数模型。

## 拟合质量（100 个官方 player-map）

- 6 项 NNLS（无截距、权重≥0、轻微 L2 正则 λ=0.02）：
  **R²=0.8933 / RMSE=0.1127 / MAE=0.0923 / maxerr=0.3676**。
  λ=0 会把“存活”权重压到 0；λ=0.02 让 6 个子项都为正、R² 几乎不掉。
- 重跑：`python3 docs/fit30.py <特征.csv>`（读特征 CSV + `docs/rating30-data-template.csv`，
  输出上面这套常量与拟合质量）。
- 特征 CSV 生成：临时控制台引用本库，对 10 张 EPL demo 逐个 `DemoAnalyzer.ParseFile` →
  `StatsCalculator.CalculateAll30`，导出 `demo,player,eKpr,eAdr,kastPct,survivingPct,multiKillPct,swingPct,kpr,adr,rounds,k,d,a`
  共 110 行（11 图 × 10 人；含归档那张附加图）。
- 样本只有 100 行，权重是小样本近似；新图入库后重跑即可更新（`Version` 再上跳）。

## 与参考仓库的差异

对照 `https://github.com/bilibili-CLY/CSStatsRadar`（只读参考，未复制代码）：
参考仓库只有 Rating20 口径、无 Rating 3.0；本节为本库自拟，与参考无一致性要求。

## 拟合数据（已归档）

- 官方转录：`docs/rating30-data-template.csv`（100 行：Vitality-MOUZ 3 图、Aurora-Spirit 2 图、
  MOUZ-Spirit 3 图、Falcons-Spirit 2 图；`rounds` 列由 demo 回填）。
- 对应 demo：`docs/demo/` 下 4 个系列目录共 10 个 `.dem`（未进 git；CI 从 Release 取）。
- 方法脚本：`docs/fit30.py`（纯标准库，读特征 CSV 跑 NNLS 回归）。
