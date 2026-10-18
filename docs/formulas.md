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

公式版本 `StatsCalculator.Rating30Version`（当前 `"3.2.0"`，见返回数据的 `Version` 字段）。
状态：**已拟合（六档经济口径）**。

HLTV 从未公开 Rating 3.0 精确公式：2025-08-20 首发（`hltv.org/news/42485`）、
2025-10-29 调整（`news/43047`）、2026-10-01 精度更新（`news/45596`）都只给结构，不给系数与胜率表。
本库按官方公开的结构做了**结构对齐的本地近似**：

- Rating 3.0 = 经济调整版 Rating 2.1 + Round Swing，6 个子项：
  **击杀 / 伤害 / 存活 / KAST / 多杀 / Round Swing**（2.0 的 Impact 被 RS + MultiKill 取代）。
- 击杀 / 伤害按**对枪胜率难度**做经济加权；KAST / 存活 / 多杀的情境加权值已计算并输出，
  但现有 100 条单图样本上验证不如原始指标稳定，因此 3.2 的最终合成仍使用原始三项。
- 击杀分按**双方经济 + 是否下包 + 存活人数**算回合胜率变化；回合胜利的 Swing 按
  1 份残局 / 2 份 WPA（建立优势的击杀）/ 1 份拆包或补枪 / 1 份“赢下且存活”分配。
- 官方只公开过锚点：同装备对枪约 48%、一线步枪打初始手枪约 75%、AWP 打步枪 T 56% / CT 60%；
  其余数值（eKPR/eADR 系数、Swing 权重）官方未公开，**本库用锚点 + 10 张 EPL demo 自行标定**。
- 官方 2025-10 把“最后一滴伤害”的权重下调、更重视击杀本身（回到 60/40 输出 vs 成本），
  本库的分摊比（击杀 0.60 / 伤害 0.25 / 闪光 0.10 / 补枪 0.05）是按其描述的**近似**。

入口：`StatsCalculator.Calculate30 / CalculateAll30` → `PlayerMatchStats30`
（12 项：KPR / ADR / eKPR / eADR / eKAST / eSurviving / eMultiKill / KAST / Surviving / MultiKill / RoundSwing / Rating30，
与 Rating20 的类型和方法完全隔开）。零回合 12 项全 null。

## 3.2 公式

设 `r = Rounds`；KPR / ADR / KAST / Surviving / MultiKill 与 V1 同定义（见上），另有：

| 指标 | 公式 | 缺数时 |
|---|---|---|
| eKPR | 经济加权 KPR：`Σᵢ 难度ᵢ / r`（对每次敌方击杀 i） | r=0 时 null |
| eADR | 经济加权 ADR：`Σᵢ 伤害ᵢ · 难度ᵢ / r`（对每次敌方伤害 i） | r=0 时 null |
| RoundSwing | 全回合 Swing 总和 `/ r * 100`（百分点/回合，可为负） | r=0 时 null |
| Rating30 | `0.130933*(eKPR/0.695300) + 0.235356*(eADR/75.566920) + 0.086975*(存活率/0.337601)`<br>`+ 0.320494*((KAST/100)/0.723368) + 0.143041*(多杀比率/0.152553) + 0.158369*(1 + Swing均值/0.039305)`，负数按 0 计 | r=0 时 null |

- `存活率` = `SurvivedRounds / r`（0~1）；`多杀比率` = `(2K+3K+4K+5K) / r`（0~1，与对外 `MultiKill%` 同一量）；
  `Swing均值` = 全回合 Swing 总和 / r（胜率小数/回合）。
- 与 Rating20 不同：Rating30 用 **eKPR / eADR**（经济加权）替代原始 KPR / ADR；原始值仍照常输出。
- `eKast`、`eSurviving`、`eMultiKill` 是诊断字段，3.2 暂不进入最终合成；原因是 10 张 demo 的动作概率拟合在现有目标集上降低了拟合质量。

## 经济/武器权重（eKPR / eADR）

3.2 起按 HLTV 公开的**六档装备**定档，不再用连续装备价值差：

| 档位 | 代表武器 | 档位偏移 |
|---|---|---|
| 初始手枪 | Glock-18 / P2000 / USP-S | 0.00 |
| 升级手枪 | Desert Eagle / R8 / CZ75 / Five-SeveN / Tec-9 / Dual Berettas / P250 | +0.30 |
| SMG / 霰弹 | MAC-10 / UMP-45 / P90 / MP7 / MP5-SD / MP9 / PP-Bizon / Nova / XM1014 / MAG-7 / Sawed-Off | +0.55 |
| 二线步枪 | Galil AR / FAMAS / SSG 08 | +0.85 |
| 一线步枪 | AK-47 / M4A4 / M4A1 / AUG / SG 553 | +1.18 |
| 狙击枪 | AWP / SCAR-20 / G3SG1 | +1.63 |

```
p(自己赢) = sigmoid(-0.08 + offset(自己档) - offset(对方档) - 0.0905*(自己是 T ? 1 : 0))
难度 d(p)  = (1 - p) / 0.5      # 五五开 = 1；优势对枪 < 1；劣势对枪 > 1
```

- 偏移量按公开锚点标定：同档 CT ≈ 48%；AK 打 Glock ≈ 75%；
  AWP 打 AK：T ≈ 57% / CT ≈ 59%（官方口径 56% / 60%）。
- 击杀计 `d(p)`，伤害计 `伤害 × d(p)`；拿不到武器时按装备价值回退：
  <1000 初始手枪、<1800 升级手枪、<2600 SMG、<3400 二线步枪、<5000 一线步枪、否则狙击枪。
- 伤害事件没有武器字段，受害者武器用其本回合作为击杀者时的武器推断。
- 3.1 的连续装备差公式已删除（`DuelBeta1` 不再使用）；六档是官方描述的结构，比价格连续近似更接近口径。

## Round Swing v3（经济 + 安包 + 贡献分摊 + 终局规则）

每回合优先使用 `SurvivalInfo` 得到实际双方人数；缺失时回退 5v5，按击杀 tick 排序：

```
P_T(tAlive, ctAlive) = sigmoid(-0.05 + (tAlive - ctAlive) + 0.0689*(evT - evCT)/1000 + 0.8672*已下包)
```

- 存活数系数锚定 1.0、偏置 -0.05（保持 5v5 ≈ 48.8%、5v4 ≈ 72.1% 公开示例）；
  经济系数 0.0689、安包系数 0.8672 沿用公开锚点近似。
  `evT / evCT` 是双方**全队**装备总值（如 4250）。
- 当前训练集中每张地图只有 20~61 个回合，地图先验不足，因此按 2026 规则回退全局胜率；未知地图也回退全局。
- 每杀算 `ΔP`（T 视角），击杀方得 `+gain`（gain = 自己队伍视角的胜率增量），
  在自己队伍内按 **击杀 0.60 / 伤害 0.25（按对受害者的伤害占比）/ 闪光助攻 0.10 / 补枪 0.05** 分摊，
  剩余归击杀者；受害者承担等量负值。
- 补枪份额不再用 csda 的 `isTradeKill`（窗口语义不透明），改为自算：本次击杀的受害者若在
  **5 秒**内先杀了击杀者的队友，则该队友拿 0.05（窗口按 `Meta.TickRate` 换算）。
- 炸弹距离爆炸不足 10 秒时，不再为击杀产生 Swing；当前解析结果没有 kit 状态，因此 3.2 使用无 kit 的保守阈值。
- 如果模型计算出的击杀胜率增量为负，按 0 处理。
- 回合末把 `实际结果 - 模型胜率`（残值）在双方之间**零和**分摊：1 份残局（在 csda `clutches` 里）
  + 2 份正向击杀 swing 占比（安包回合只使用安包后的击杀）+ 1 份拆包（`defuserSteamId`）或补枪 + 1 份存活，归一化；
  负方拿负份额。**全玩家 swing 之和恒为 0**。
- 阵营未知时按“队伤已过滤、凶手必为对方”反推；两边都未知则该杀不计 Swing。
- 无经济/安包数据时退化为纯存活数模型。

## 拟合质量（100 个官方 player-map）

- 3.2 **沿用 3.1 的 6 项 NNLS 权重**（无截距、权重≥0、轻微 L2 正则 λ=0.02），没有在 100 行上重新拟合；
  只把 eKPR / eADR 的归一化均值按 3.2 新特征重算：`AvgEKpr` 0.627240 → 0.695300、
  `AvgEAdr` 69.722210 → 75.566920（其余 4 个均值本来就等于样本均值，未动）。
- 新口径下同一套权重的 100 行评估：**bias -0.0036 / RMSE 0.1066 / MAE 0.0854 / maxerr 0.335 /
  corr 0.9512 / R² 0.9046**；3.1 为 RMSE 0.1142 / MAE 0.0926 / maxerr 0.390 / R² 0.8906。
- 残差仍主要来自 Swing：本库 swing 与官方 swing 的 corr 只有 0.871，
  OLS `官方 ≈ -0.018 + 0.895 × 本库`；把官方 swing 直接换进来可把 RMSE 压到约 0.084。
  根因是 HLTV 没公开地图/阵营/装备的胜率表，不是再调权重能补的；等更多标注数据再动。
- 重跑：`python3 docs/fit30.py <特征.csv>`（读特征 CSV + `docs/rating30-data-template.csv`，
  输出一套**重新拟合**的权重与质量；在 100 行上重拟合容易过拟合，重跑结果只作对照，
  不要直接覆盖生产常量，除非同时确认样本量足够）。
- 特征 CSV 生成：临时控制台引用本库，对 10 张 EPL demo 逐个 `DemoAnalyzer.ParseFile` →
  `StatsCalculator.CalculateAll30`，导出 100 行（10 图 × 10 人），包含 eKPR/eADR、三个诊断情境特征、原始 KAST/存活/多杀和 Swing。
- 样本只有 100 行，权重是小样本近似；新图入库后重跑即可更新（`Version` 再上跳）。

## 与参考仓库的差异

对照 `https://github.com/bilibili-CLY/CSStatsRadar`（只读参考，未复制代码）：
参考仓库只有 Rating20 口径、无 Rating 3.0；本节为本库自拟，与参考无一致性要求。

## 拟合数据（已归档）

- 官方转录：`docs/rating30-data-template.csv`（100 行：Vitality-MOUZ 3 图、Aurora-Spirit 2 图、
  MOUZ-Spirit 3 图、Falcons-Spirit 2 图；`rounds` 列由 demo 回填）。
- 对应 demo：`docs/demo/` 下 4 个系列目录共 10 个 `.dem`（未进 git；CI 从 Release 取）。
- 方法脚本：`docs/fit30.py`（纯标准库，读特征 CSV 跑 NNLS 回归）。
