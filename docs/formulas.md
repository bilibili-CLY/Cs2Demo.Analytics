# 统计口径（V1）

V1 共 6 项：KPR / ADR / KAST / Surviving / Impact / Rating2.0。
实现见 `src/Cs2Demo.Analytics/StatsCalculator.cs`，对应测试见
`tests/Cs2Demo.Analytics.Tests/StatsCalculatorTests.cs`（手算期望值）。

## 基础量

- `Rounds`：有效回合数（不含热身，见 `docs/demo-format.md`）。
- `Kills / Deaths / Assists`：有效击杀数 / 死亡数 / 助攻数（自杀、队伤已在解析时丢掉）。
- `TotalDamage`：真实扣血总和（溢出伤害已折掉）。**缺伤害数据时为 null**。
- `SurvivedRounds`：存活到回合结束的回合数。
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

Rating 3.0、Swing（胜率波动）需要逐 tick 经济与胜率模型，V1 不做，
公式确定后再补本文件并走“公式改动写明差异”的流程。
