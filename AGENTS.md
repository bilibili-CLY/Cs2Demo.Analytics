# AGENTS.md — Cs2Demo.Analytics

> 项目已落地；0.1.0 / 0.2.0 / 0.3.0（当前）已发布到 nuget.org。
> 0.3.0 起解析后端改为 csda，并把 Rating30 升级为经济加权版（见「技术栈」）。构建/测试/打包命令均已验证。

## 目标与交付物

- 解析 CS2 Demo 文件，输出玩家数据并计算 Rating、Swing、ADR 等指标。
- 交付物是 NuGet 库：其他项目安装包后调用公开方法；公开 API 视为稳定契约，新增/改名/删改即 breaking change，打包配置（`PackageId`、版本、README）改动需谨慎。
- NuGet 发布不可逆：`push` 前必须跟用户确认版本号；API Key 等密钥绝不写进仓库。
- 统计口径可验证：Rating、Swing、ADR 公式改动必须写明公式与参考仓库的差异；不编造数据，测试用固定 demo 或可复现用例。
- Breaking change 先确认：公开方法签名、公开类型增删改前先确认。

## 参考实现（只读）

- 解析与计算逻辑参考 `https://github.com/bilibili-CLY/CSStatsRadar.git` 中的代码，按需对照算法口径。
- 仓库已公开：`https://github.com/bilibili-CLY/Cs2Demo.Analytics`，许可证 Apache-2.0（见 `LICENSE`）。
  参考仓库代码遵守其自身许可，不要整段复制，算法理解后自己实现。
- 公开也不乱传：`Mapping.txt`、API Key、token、私源口令绝不进仓库；
  golden `.dem`（193MB）不进 git，CI 从 Release 附件 `dem-data` 下载。

## 技术栈

- C# / .NET：库 `src/Cs2Demo.Analytics` 目标 `net8.0`（net8/9/10 调用方都能引用）；
  测试 `tests/Cs2Demo.Analytics.Tests` 目标 `net10.0`（本机只有 .NET 10 runtime，只影响本地跑测试，不影响发布包）。
- SDK 用 `global.json` 锁定（10.0.106，rollForward latestMajor）。
- 解析用 csda（akiver/cs-demo-analyzer，MIT），**不是 NuGet 依赖**：随包内置 5 平台二进制
  （`src/Cs2Demo.Analytics/csda/<rid>/csda[.exe]`，约 56MB，**进 git**）；`build/Cs2Demo.Analytics.targets`
  会把它复制到调用方输出目录 `csda/<rid>/`。因此包体积约 58MB，只支持 win-x64 / osx-x64 / osx-arm64 /
  linux-x64 / linux-arm64。上游许可证见 `csda/LICENSE-csda.txt`。

## 入口点与项目边界

- `DemoAnalyzer.ParseFile(path)` → `MatchData`（只解析，不计算；`ParseStream` 已删，csda 只吃本地文件）。
  可用 `DemoAnalyzer.CsdaExecutablePath` 指定自带的 csda。
- `StatsCalculator.CalculateAll(data) / Calculate(data, steamId)` → V1 6 项
  KPR / ADR / KAST / Surviving / Impact / Rating2.0；`CalculateAll30 / Calculate30` → Rating30 12 项
  （KPR / ADR / eKPR / eADR / eKAST / eSurviving / eMultiKill / KAST / Surviving / MultiKill / RoundSwing / Rating30）。只计算，不碰 demo 文件。
  口径版本 `Rating20Version` = `"2.0.0"`，`Rating30Version` = `"3.2.0"`。
- 口径来源：KPR / ADR / KAST / Rating2.0 直接取 csda；Surviving / Impact / MultiKill 由 csda 原料派生；
  eKPR / eADR（六档装备经济加权）、RoundSwing、Rating30 由本库计算（Rating30 用 eKPR / eADR 替代 KPR / ADR）。
  KAST 单位是 0~100。
- 口径文档：`docs/formulas.md`（公式 + 与参考仓库的差异），`docs/demo-format.md`（csda→MatchData 映射），
  `docs/csda-integration.md`（csda 参考手册）。
- 固定 `.dem`（`tests/Cs2Demo.Analytics.Tests/TestData/perfect-world.dem`，de_nuke，
  SHA256 见 `GoldenDemoTests`）已锁 golden：18 回合 / 135 杀 / 10 人全员断言；
  不要替换该文件，换 demo 必须同步更新期望表。

## 命令（已验证）

- `dotnet build`——构建解决方案。
- `dotnet test`——运行全部测试。
- `dotnet test --filter <Name>`——运行单个/聚焦测试。
- `dotnet pack src/Cs2Demo.Analytics/Cs2Demo.Analytics.csproj -c Release`——打干净包
  （`bin/Release/Cs2Demo.Analytics.<版本>.nupkg`，发 nuget 用；**push 前必须确认版本号，Key 不进仓库**）。
- 混淆版（手动分发用，对方项目直接 dll 引用）：前置 `dotnet tool install -g Obfuscar.GlobalTool`
 （本机已装 2.2.50；运行时需 `DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.401_1/libexec`，
  以 Cellar 当前版本为准，旧的 10.0.106 已被 brew 升级掉，否则工具会找到旧的 /usr/local/share/dotnet 而启动失败）。
  流程：先 `dotnet pack -c Release`，再 `obfuscar.console src/Cs2Demo.Analytics/obfuscar.xml`，
  产物 `bin/Release/obfuscated/Cs2Demo.Analytics.dll`（已验证 golden 数一致）。
  配置默认保留公开 API、只改内部名；`Mapping.txt` 是反混淆钥匙，绝不外发。
- 调用说明写在 `docs/usage.md`（两种引用写法 + 字段速查），给对方看。
- 发版（GitHub Actions 自动推 nuget）：先改 csproj `Version`，再
  `git tag v<版本> && git push origin v<版本>`；tag 必须等于 `v+Version`，
  云端自动 test → pack → OIDC 推包。`.github/` 是唯一的顶层目录例外。
  首次用之前：nuget.org 建 Trusted Publishing 策略
 （Owner=你的 nuget 用户名，仓库 `bilibili-CLY/Cs2Demo.Analytics`，workflow 文件 `publish.yml`），
  再在 GitHub 仓库 Secrets 加 `NUGET_USER`（nuget.org 用户名，不是邮箱）。
- 已发布到 nuget.org：`0.1.0` / `0.2.0` / **`0.3.0`**（`dotnet add package Cs2Demo.Analytics`）。
  0.1.0 最早也发过本地私源 `~/nuget-feed`（源名 `Local`）。
  0.3.0 发布前已用外部控制台项目端到端验证（装包 → 输出目录自动带 `csda/<rid>/` → 跑 golden demo，V1/V30 数一致）。
  包 README 已随包（`README.md` + `PackageReadmeFile`）。
  多机共享再考虑 GitHub/Azure 这类带 token 的私源；`dotnet format` 未配置，暂不用。
- 故障排查：CI 若挂在「下载 golden demo」这步（日志 `release not found`），说明 `dem-data`
  Release 丢了（tag 可能还在，但 Release 内容没了）。重建即可：
  `gh release create dem-data --verify-tag --latest=false` →
  `gh release upload dem-data tests/Cs2Demo.Analytics.Tests/TestData/perfect-world.dem` →
  `gh run rerun <run-id>`。

## 与用户协作的方式

- 用户说法仅作参考：独立判断，错了就直说纠正，不要迎合。
- 默认按非专业用户沟通：先判断需求合理性与逻辑，再给专业方案；少用术语，必须用时用大白话解释。
- 每完成一个阶段性任务必须输出总结（做了什么、结果、下一步）；卡住立即说明（卡在哪、试过什么、需要用户做什么），不闷头重试。

## 后续会话约定

- 保持 解决方案 + `src/` / `tests/` 布局；不要新增其他顶层目录。
- 逆向出的 demo 格式笔记放在 `docs/`，与依赖它的代码放在一起，不要只留在聊天记录里。
- 脚手架落地后，用真实入口点、项目边界和已验证的精确命令替换本 stub。
