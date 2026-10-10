# TestData

固定 `.dem`  golden 文件：

- `perfect-world.dem`（约 193 MB，de_nuke，18 回合 / 135 杀 / 10 人）
- SHA256：`87e7057cffd7257e7111e184acb324cb6acab7006e88f83f46d03f94146045ed`
- `GoldenDemoTests` 按此 SHA + 全员 10 行数据断言；**不要替换/改名此文件**，
  否则 golden 会失败。换新 demo 必须同步更新测试里的期望表（先人工核对，不编造数值）。
- 文件会被复制到测试输出目录（见测试 csproj 的 `TestData` 复制配置）。
