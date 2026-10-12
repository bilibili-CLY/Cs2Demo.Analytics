# 统计口径（V1）

V1 共 6 项：KPR / ADR / KAST / Surviving / Impact / Rating2.0。
实现见 `src/Cs2Demo.Analytics/StatsCalculator.cs`，对应测试见
`tests/Cs2Demo.Analytics.Tests/StatsCalculatorTests.cs`（手算期望值）。

## 基础量

- `Rounds`：有效回合数（不含热身，见 `docs/demo-format.md`）。
- `Kills / Deaths / Assists`：有效击杀数 / 死亡数 / 助攻数（自杀丢掉；
  **队杀与 C4 炸死记死亡**，与官方记分板一致；凶手为空，不记 K）。
- `TotalDamage`：敌方伤害的真实扣血总和（溢出伤害已折掉；自伤/队伤/世界伤害
  只进血条账本，**不记入本表**）。**缺伤害数据时为 null**。
- `SurvivedRounds`：存活到回合结束的回合数（**被杀=未存活**强制纠正，
  不信任 GOTV 同 tick 快照）。
- `KastRounds`：有击杀 / 助攻 / 补枪 / 存活的回合数（同一回合去重，只计一次）。

## 6 项指标

设 `r = Rounds`，`kpr = Kills / r`，`dpr = Deaths / r`，`apr = Assists / r`：

| 指标 | 公式 | 缺数时 |
|---|---|---|
| KPR | `Kills / r` | r=0 时 null |
| ADR | `TotalDamage / r` | 无伤害数据时 null |
| KAST | `KastRounds / r`（0~1） | r=0 时 null |
| Surviving | `SurvivedRounds / r`（0~1） | r=0 时 null |
| Impact | `2.13 * kpr + 0.42 * apr - 0.41`，负数按 0 计 | r=0 时 null |
| Rating2.0 | `0.0073 * KAST% + 0.3591 * kpr - 0.5329 * dpr + 0.2372 * Impact + 0.0032 * ADR + 0.1587`，其中 `KAST% = Kast * 100`，负数按 0 计 | r=0 时 null；缺 ADR 时按 `ADR = 0` 继续算（Adr 本项仍为 null） |

所有指标保留 3 位小数。

## Rating2.0 的来源（必读）

这是**社区逆向拟合的近似公式，不是 HLTV 官方值**，调用处必须按“本地近似”理解：

- HLTV 在 2017 年夏天上线 Rating 2.0 时只公开了 5 个输入变量
  （KPR、DPR、Impact、ADR、KAST）， exact 公式保密，且官方按 CT/T 分边计算。
- flashed.gg（Dave，2021-01-09，《Reverse engineering HLTV rating》）抓取
  hltv.org 球员数据做线性回归（R²=0.995，RMSE=0.0037），得到上述系数；
  同一套系数也被开源项目 awpy（`awpy/stats/rating.py`）采用。
- Impact 中的 `2.13 * KPR + 0.42 * APR` 同样是社区拟合的“脏公式”
  （`-0.41` 为后续补的截距项）。

## 与参考仓库的差异

对照 `https://github.com/bilibili-CLY/CSStatsRadar`（只读参考，未复制代码）：

1. **公式本身无差异**：Impact、Rating2.0 的系数与截距项与参考完全一致。
2. **负数截断一致**：参考经 `OkMetric` 做 `max(0, value)` 再保留 3 位小数；
   本库同样先 `max(0, …)` 再保留 3 位小数。
3. **缺 ADR 口径一致**：参考 `Damages` 为 null 时 ADR 记 null、Rating 按 `ADR = 0`
   继续算；本库 `Damages` 为空列表时同理。
4. **回合计数口径一致**：都是有效回合总数；零回合时 6 项全为 null。
5. **KAST 补枪口径一致**：补枪回溯窗口 5 秒（320 tick），判定条件
   “上次击杀的攻击者 == 本次受害者 且 上次受害方 == 本次攻击方”。

## 不在 V1 的东西

~~Rating 3.0、Swing（胜率波动）需要逐 tick 经济与胜率模型，V1 不做，
公式确定后再补本文件并走“公式改动写明差异”的流程。~~
V1 时这句话成立；现已加 Rating30 V0（实验性本地近似，见下），V1 的 6 项口径不变。

# Rating30（拟合版，本地近似，非 HLTV 官方值）

状态：**已拟合**。HLTV 从未公开 3.0 精确公式（2025-08 首发、
2025-10 hotfix、2026-10 精度更新三篇官方公告都只给结构，不给系数与胜率表），
本节公式是用 10 张 EPL S24 地图（100 个 player-map，官方记分板转录
`docs/rating30-data-template.csv` + 本地 demo 解析）做 NNLS 回归反推出来的，
脚本见 `docs/fit30.py`（纯标准库，可重跑）。

入口：`StatsCalculator.Calculate30 / CalculateAll30` → `PlayerMatchStats30`
（7 项：KPR / ADR / KAST / Surviving / MultiKill / RoundSwing / Rating30，
与 Rating20 的类型和方法完全隔开）。基础计数与 V1 同口径（见上），
零回合 7 项全 null，缺伤害时 Adr 为 null 但 Rating30 按 ADR=0 照算。

## 7 项公式

设 `r = Rounds`，`kpr / kast / surviving / adr` 定义同 V1：

| 指标 | 公式 | 缺数时 |
|---|---|---|
| KPR / ADR / KAST / Surviving | 与 V1 完全一致 | 同 V1 |
| MultiKill | 同回合击杀数分组（5 杀封顶）：`(1K + 4*2K + 9*3K + 16*4K + 25*5K) / r / 1.1693` | r=0 时 null |
| RoundSwing | 全回合 Swing 总和 `/ r * 100`（百分点/回合，可为负） | r=0 时 null |
| Rating30 | `0.0576*(kpr/0.6606) + 0.4018*(adr/72.195) + 0.0238*(surviving/0.3376)`<br>`+ 0.2552*(kast/0.7134) + 0.1337*MultiKill + 0.2089*(1 + Swing均值/0.076)`，负数按 0 计 | r=0 时 null；缺 ADR 按 0 继续算 |

其中 `MultiKill` 即连杀归一值（均值 1.1693 由样本定，不再沿用 HLTV 1.0 的 1.277），
`Swing均值` = 全回合 Swing 总和 / r（胜率小数/回合）。

## Swing 简化口径（无经济，零和）

- 每回合从 5v5 起算，按击杀 tick 排序，每杀减对面一个存活，查胜率函数
  `P(t,c) = 1/(1+e^-(1.0*(t-c)-0.05))`（5v5 = 0.4875，与 HLTV 公开的 48.8% 对得上；
  slope=1.0 是网格搜索最优，corr 0.788，其余格点是 logistic 外推，不是 HLTV 表）。
- 击杀者得 `+ΔP`（自己队伍视角），受害者得 `-ΔP`，**纯零和，无残值分红**
  （官方 Swing 每图 10 人之和≈0，旧 V0 的“剩余胜率分红”是错的，已删）。
  阵营未知时按“队伤已过滤、凶手必为对方”反推，两边都未知则该杀不计 Swing。
- **已知缺口（与 HLTV 的差异）**：无经济加权（eco 杀与枪局杀同权）、
  无安包/拆包/残局/闪光分账。拿官方 Swing 替换本地 Swing，R² 从 0.87 涨到 0.96，
  差的 0.08 基本就是经济加权等的价值。全量 eco 需要解析器订阅经济事件，
  是下一步，不在本版。

## 拟合质量与取舍

- 6 项 NNLS（无截距、权重≥0，`docs/fit30.py centered`）：**R²=0.874 / RMSE=0.123 /
  最大误差 0.37**（爆种局如 2.06 的 tN1R 误差最大，平时够用）。
- 方差份额：Swing 37% / ADR 29% / 连杀 18% / KAST 9% / KPR 5% / 存活 2%——
  Swing≈1/3、KPR+连杀≈1/4，自发复现了 HLTV 官宣的 Swing 33% / Kill 25% 结构。
- DPR 与存活率数学恒等（无人掉线时 `dpr + surviving = 1`），只留存活一项。
- 残局 clutch 不加：10 图里真正的 1vX 只检出约 1 次，加上 R² 只涨 0.0005。
- 试过把 Kill 0.25 / Swing 0.33 写死（官方公告值）再拟合其余项，R²=0.848 更差
  且连杀/存活权重被压到 0，不采用。
- 样本只有 100 行，权重是小样本近似；后续新图入库后重跑 `fit30.py` 即可更新。

## 与参考仓库的差异

对照 `https://github.com/bilibili-CLY/CSStatsRadar`（只读参考，未复制代码）：
参考仓库目前只有 Rating20 口径，无 Rating30；本节公式为本库自拟，
与参考无冲突也无一致性要求。
解析口径三处修正（存活“被杀=未存活”强制纠正、血条账本全伤害跟血但只记敌方伤害、
队杀/C4 炸死记死亡）均为“对官方记分板”而做，参考仓库对应行为未逐项核对。

## 拟合数据（已归档）

- 官方转录：`docs/rating30-data-template.csv`（100 行：Vitality-MOUZ 3 图、
  Aurora-Spirit 2 图、MOUZ-Spirit 3 图、Falcons-Spirit 2 图；rounds 列由 demo 回填）。
- 对应 demo：`docs/demo/` 下 4 个系列目录共 10 个 `.dem`（未进 git；CI 从 Release 取）。
- 方法脚本：`docs/fit30.py`（`check` 做 demo-vs-官方校验，`nnls` 做回归对照，
  `centered` 跑终版可部署形态）。
