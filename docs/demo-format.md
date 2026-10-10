# Demo 格式笔记（V1）

解析入口：`src/Cs2Demo.Analytics/DemoAnalyzer.cs`，依赖 `DemoFile.Game.Cs 0.43.1`。
本文件记录“GOTV 事件 → 本库模型”的映射关系，与代码互相印证。

## 订阅的事件

| GOTV 事件 | 用途 |
|---|---|
| `RoundStart` | 回合数 +1，记录开始 tick，收集玩家表 |
| `RoundFreezeEnd` | 兜底建回合（中途加入、开局事件缺失时），收集玩家表 |
| `PlayerDeath` | 击杀（过滤后）+ 补枪判定 |
| `PlayerHurt` | 伤害（过滤并折成真实扣血后） |
| `RoundEnd` | 胜方、结束 tick、当回合存活表 |

`BombPlanted / BombDefused` 等经济相关事件 V1 不用（Swing 才需要），故未订阅；
以后加 Swing 时再补订阅（公开 API 不变）。

## 过滤规则

1. **热身**：`GameRules.WarmupPeriod` 为 true 的事件全部丢掉。
2. **自杀**：击杀者 == 受害者，丢掉（击杀与伤害都是）。
3. **队伤**：击杀者与受害者同阵营（且阵营已知），丢掉。
4. **BOT**：`SteamID == 0` 的不建档（击杀/伤害事件里出现则整条丢掉）。
5. **零伤害**：`DmgHealth <= 0` 的伤害事件丢掉；折成真实扣血为 0 的也不记录。

## Tick 与补枪窗口

- Tick 取 `demo.CurrentDemoTick`，按 64 tick/秒理解。
- 补枪回溯窗口 `5 * 64 = 320` tick（见 `TradeResolver.WindowTicks`）。
- 判定（见 `TradeResolver.Resolve`）：同回合内往回找，窗口内的上次击杀若满足
  “上次攻击者 == 本次受害者 且 上次受害方 == 本次攻击方”，
  则本次击杀的 `TradedSteamId` 记为上次击杀的受害者。

## 真实伤害折算

GOTV 上报的是毛伤害（含溢出），本库按“每（回合，受害者）100 血起步”折（见 `HealthLedger`）：

```text
真实伤害 = 上次剩余 - clamp(上报剩余血量, 0, 上次剩余)
```

例如满血 100 被报 75（剩 25 血）记 25；残血 30 被打出 100 只记 30；
回血不产生负伤害。不同回合互相隔离。

## 阵营与武器

- 阵营由 `TeamNum` 映射：`Terrorist → "T"`，`CounterTerrorist → "CT"`，其他记 `""`（未知）。
- 武器名归一化（见 `WeaponNames`）：去 `weapon_` 前缀，刀具统一为 `Knife`，
  未知非空名字记 `UNKNOWN`。V1 计算用不到武器，记下来是为以后免 breaking change。

## 异常与兜底

- 中途加入（击杀/伤害先到、回合事件还没来）：自动补建当前回合。
- 尾部坏包（`Google.Protobuf.InvalidProtocolBufferException` 链）：丢掉最后一个
  没打完的回合（连带它的击杀/伤害/存活），保留之前已解析的数据。
- 解析完玩家数为 0 或有效回合数为 0：抛 `InvalidOperationException`（demo 本身不可用）。
- `Meta.MapName` 取 `ServerInfo.MapName`；`Meta.FileSha256` 只在 `ParseFile` 计算，
  `ParseStream` 留空。
