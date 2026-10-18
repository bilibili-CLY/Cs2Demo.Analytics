#!/usr/bin/env python3
"""Rating30（HLTV Rating 3.0 本地近似）回归拟合，纯标准库，无 numpy。

模型（与 StatsCalculator.cs 一一对应）：
    Rating30 = Σ wᵢ · zᵢ
    z₁ = eKPR / AvgEKpr        z₄ = KAST比率 / AvgKast
    z₂ = eADR / AvgEAdr        z₅ = 多杀回合比率 / AvgMultiKill
    z₃ = 存活比率 / AvgSurviving   z₆ = 1 + swing比率 / SwingScale
其中 Avg* 是各特征的样本均值（SwingScale 取 swing 比率的总体标准差），
w 用带轻微 L2 正则（λ=0.02）的非负最小二乘（NNLS）拟合，避免子项出现负数。

输入：
  1) docs/rating30-data-template.csv —— 100 行 HLTV 官方 Rating 3.0 目标值。
  2) 特征 CSV —— 本库在 10 张 EPL demo 上跑出的每名玩家特征，列：
       demo,player,eKpr,eAdr,eSurviving,eKast,eMultiKill,kastPct,survivingPct,multiKillPct,swingPct,kpr,adr,rounds,k,d,a
     生成方式见 docs/formulas.md「Rating30 拟合」小节（临时控制台调
     DemoAnalyzer.ParseFile + StatsCalculator.CalculateAll30）。

用法：
    python3 docs/fit30.py <features.csv>
    CSDA_FEATURES=<features.csv> python3 docs/fit30.py

输出可直接粘回 StatsCalculator.cs 的常量与拟合质量（R² / RMSE / MAE / maxerr）。
"""
import csv
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OFFICIAL_CSV = os.path.join(ROOT, "docs", "rating30-data-template.csv")
RIDGE_LAMBDA = 0.02
FEATURE_COLUMNS = ("eKpr", "eAdr", "survivingPct", "kastPct", "multiKillPct", "swingPct")


def read_csv(path):
    with open(path, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def normalize_tag(tag):
    """把官方模板里的 demo 短名与真实文件名归一到可比形式。"""
    tag = tag.replace(".dem", "").replace("_", "-").replace("-vs-", "-")
    return re.sub(r"-m\d+-", "-", tag)


def normalize_name(name):
    return re.sub(r"[^0-9a-z\u4e00-\u9fff]+", "", name.lower())


def load_features(path):
    feats = {}
    for row in read_csv(path):
        key = (normalize_tag(row["demo"]), normalize_name(row["player"]))
        feats[key] = row
    return feats


def join_rows(feats):
    """返回 [(player, rating30, feature_dict), ...]，顺序与官方模板一致。"""
    joined = []
    missing = []
    for row in read_csv(OFFICIAL_CSV):
        if not row["player"].strip():
            continue
        key = (normalize_tag(row["demo_file"]), normalize_name(row["player"]))
        fd = feats.get(key)
        if fd is None:
            missing.append(key)
            continue
        joined.append((row["player"], float(row["rating30"]), fd))
    if missing:
        for key in missing[:10]:
            print(f"  缺失匹配: {key}", file=sys.stderr)
        raise SystemExit(f"有 {len(missing)} 行没匹配上特征，请检查 demo/玩家名归一。")
    return joined


def nnls_ridge(z, y, lam, iters=200000):
    """投影梯度 + L2 正则的非负最小二乘（numpy 不在时用）。"""
    n, p = len(z), len(z[0])
    scale = sum(z[i][j] ** 2 for i in range(n) for j in range(p)) / (n * p)
    lr = 0.9 / scale
    w = [0.0] * p
    for it in range(iters):
        grad = [0.0] * p
        for i in range(n):
            err = sum(z[i][j] * w[j] for j in range(p)) - y[i]
            for j in range(p):
                grad[j] += err * z[i][j]
        for j in range(p):
            grad[j] = grad[j] / n + lam * w[j]
        step = lr / (1 + it / 20000)
        w = [max(0.0, w[j] - step * grad[j]) for j in range(p)]
    return w


def main():
    if len(sys.argv) > 1:
        features_path = sys.argv[1]
    else:
        features_path = os.environ.get("CSDA_FEATURES", "")
    if not features_path or not os.path.exists(features_path):
        raise SystemExit(
            "用法: python3 docs/fit30.py <features.csv>（或用 CSDA_FEATURES 环境变量指定）。\n"
            "特征 CSV 生成方式见 docs/formulas.md「Rating30 拟合」。"
        )

    rows = join_rows(load_features(features_path))
    n, p = len(rows), len(FEATURE_COLUMNS)
    f = [[float(fd[col]) for col in FEATURE_COLUMNS] for _, _, fd in rows]
    f = [[f[i][0], f[i][1], f[i][2] / 100.0, f[i][3] / 100.0, f[i][4] / 100.0, f[i][5] / 100.0]
         for i in range(n)]
    y = [rating for _, rating, _ in rows]

    means = [sum(f[i][j] for i in range(n)) / n for j in range(p)]
    swing_scale = math.sqrt(sum((f[i][5] - means[5]) ** 2 for i in range(n)) / n)
    z = [[f[i][0] / means[0], f[i][1] / means[1], f[i][2] / means[2],
          f[i][3] / means[3], f[i][4] / means[4], 1 + f[i][5] / swing_scale] for i in range(n)]

    w = nnls_ridge(z, y, RIDGE_LAMBDA)
    preds = [sum(z[i][j] * w[j] for j in range(p)) for i in range(n)]
    mean_y = sum(y) / n
    sst = sum((v - mean_y) ** 2 for v in y)
    ssr = sum((y[i] - preds[i]) ** 2 for i in range(n))

    print(f"样本 {n} 行 / 特征 {p} 项 / ridge λ={RIDGE_LAMBDA}")
    print("consts:")
    for name, value in zip(("AvgEKpr", "AvgEAdr", "AvgSurviving", "AvgKast", "AvgMultiKill"), means[:5]):
        print(f"    {name} = {value:.6f};")
    print(f"    SwingScale = {swing_scale:.6f};")
    for name, value in zip(("WeightKpr", "WeightAdr", "WeightSurviving", "WeightKast", "WeightMultiKill", "WeightSwing"), w):
        print(f"    {name} = {value:.6f};")
    print(f"R2={1 - ssr / sst:.4f} RMSE={math.sqrt(ssr / n):.4f} "
          f"MAE={sum(abs(y[i] - preds[i]) for i in range(n)) / n:.4f} "
          f"maxerr={max(abs(y[i] - preds[i]) for i in range(n)):.4f}")


if __name__ == "__main__":
    main()
