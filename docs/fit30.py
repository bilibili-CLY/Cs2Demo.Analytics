#!/usr/bin/env python3
"""Rating30 拟合分析（纯标准库）。读 docs/demo-dump + docs/rating30-data-template.csv。

用法: python3 docs/fit30.py          # 全流程：校验 + 特征 + Swing拟合 + Rating回归
        python3 docs/fit30.py check   # 只做 demo-vs-官方 校验
"""
import csv
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DUMP = os.path.join(ROOT, "docs", "demo-dump")
OFFICIAL_CSV = os.path.join(ROOT, "docs", "rating30-data-template.csv")

SIDES_T = "T"
SIDES_CT = "CT"
SIDES_UNKNOWN = ""


def read_csv(path):
    with open(path, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def load_official():
    """官方行: (map_tag, player, k, d, adr, kast_frac, swing_pct, rating)."""
    rows = []
    for r in read_csv(OFFICIAL_CSV):
        if not r["player"].strip():
            continue
        tag = r["demo_file"].strip().removesuffix(".dem")
        rows.append({
            "tag": tag,
            "player": r["player"].strip(),
            "k": int(r["k"]), "d": int(r["d"]),
            "adr": float(r["adr"]),
            "kast": float(r["kast_pct"]),
            "swing": float(r["swing_pct"]),
            "rating": float(r["rating30"]),
        })
    return rows


def load_dump(tag):
    d = os.path.join(DUMP, tag)
    players = {r["steamId"]: r["name"] for r in read_csv(os.path.join(d, "players.csv"))}
    kills = read_csv(os.path.join(d, "kills.csv"))
    for k in kills:
        k["round"] = int(k["round"])
        k["tick"] = int(k["tick"])
    rounds = {int(r["round"]): r["winnerSide"] for r in read_csv(os.path.join(d, "rounds.csv"))}
    surv = read_csv(os.path.join(d, "survivals.csv"))
    for s in surv:
        s["round"] = int(s["round"])
        s["survived"] = s["survived"] == "1"
    dmg = read_csv(os.path.join(d, "damages.csv"))
    for x in dmg:
        x["round"] = int(x["round"])
        x["damage"] = int(x["damage"])
    return players, kills, rounds, surv, dmg


def match_names(official_names, demo_names):
    """官方昵称 -> steamId。大小写不敏感精确匹配；失败返回 None 逐个报告。"""
    lower = {}
    for sid, name in demo_names.items():
        lower.setdefault(name.lower(), sid)
    mapping = {}
    for n in official_names:
        mapping[n] = lower.get(n.lower())
    return mapping


def infer_side(attacker_side, victim_side):
    """与 C# ComputeSwing 同口径：受害方未知时反推，两边未知返回 None。"""
    if victim_side in (SIDES_T, SIDES_CT):
        return victim_side
    if attacker_side == SIDES_T:
        return SIDES_CT
    if attacker_side == SIDES_CT:
        return SIDES_T
    return None


def compute_swing(kills, rounds, surv, slope=0.6, offset=-0.05, with_residual=True,
                  residual_mode="contrib"):
    """V0 Swing 的 Python 复刻 + 变体。返回 {steamId: 总swing(胜率小数)}。
    residual_mode: contrib(按击杀贡献分)/equal(胜方存活者平分)/none(不要残值)。"""
    def winprob(t, c):
        x = slope * (t - c) + offset
        return 1.0 / (1.0 + math.exp(-x))

    total = {}
    for s in surv:
        total.setdefault(s["steamId"], 0.0)
    for k in kills:
        total.setdefault(k["attacker"], 0.0)
        total.setdefault(k["victim"], 0.0)

    side_by_rp = {(s["round"], s["steamId"]): s["side"] for s in surv}

    by_round = {}
    for k in kills:
        by_round.setdefault(k["round"], []).append(k)

    for rnd, ks in by_round.items():
        ks = sorted(ks, key=lambda k: k["tick"])
        t, c = 5, 5
        p = winprob(t, c)
        earners = {}
        for k in ks:
            if k["attacker"] == k["victim"]:
                continue
            v = infer_side(k["attackerSide"], k["victimSide"])
            if v is None:
                continue
            if v == SIDES_T:
                t = max(0, t - 1)
            else:
                c = max(0, c - 1)
            after = winprob(t, c)
            d_t = after - p
            victim_sw = d_t if v == SIDES_T else -d_t
            total[k["victim"]] = total.get(k["victim"], 0.0) + victim_sw
            total[k["attacker"]] = total.get(k["attacker"], 0.0) - victim_sw
            if -victim_sw > 0:
                earners[k["attacker"]] = earners.get(k["attacker"], 0.0) + (-victim_sw)
            p = after

        if not with_residual or residual_mode == "none":
            continue
        winner = rounds.get(rnd)
        if winner not in (SIDES_T, SIDES_CT):
            continue
        residual = (1.0 - p) if winner == SIDES_T else p
        if residual <= 1e-9:
            continue
        if residual_mode == "contrib":
            pool = [i for i in earners if side_by_rp.get((rnd, i)) == winner]
            if pool:
                w = sum(earners[i] for i in pool)
                for i in pool:
                    total[i] += residual * earners[i] / w
            else:
                pool = [s["steamId"] for s in surv
                        if s["round"] == rnd and s["side"] == winner and s["survived"]]
                for i in pool:
                    total[i] = total.get(i, 0.0) + residual / len(pool)
        else:  # equal
            pool = list({s["steamId"] for s in surv
                         if s["round"] == rnd and s["side"] == winner and s["survived"]})
            for i in pool:
                total[i] = total.get(i, 0.0) + residual / len(pool)
    return total


def player_features(kills, rounds, surv, dmg):
    """每人原始特征。返回 {steamId: {...}}。"""
    r = len(rounds)
    feats = {}
    for s in surv:
        f = feats.setdefault(s["steamId"], {
            "k": 0, "d": 0, "a": 0, "dmg": 0, "surv": 0,
            "k_rounds": set(), "a_rounds": set(), "t_rounds": set(), "s_rounds": set(),
            "kills_by_round": {},
        })
        if s["survived"]:
            f["surv"] += 1
            f["s_rounds"].add(s["round"])
    for k in kills:
        if k["attacker"] != k["victim"]:
            f = feats.setdefault(k["attacker"], {
                "k": 0, "d": 0, "a": 0, "dmg": 0, "surv": 0,
                "k_rounds": set(), "a_rounds": set(), "t_rounds": set(), "s_rounds": set(),
                "kills_by_round": {},
            })
            f["k"] += 1
            f["k_rounds"].add(k["round"])
            f["kills_by_round"][k["round"]] = f["kills_by_round"].get(k["round"], 0) + 1
        v = feats.setdefault(k["victim"], {
            "k": 0, "d": 0, "a": 0, "dmg": 0, "surv": 0,
            "k_rounds": set(), "a_rounds": set(), "t_rounds": set(), "s_rounds": set(),
            "kills_by_round": {},
        })
        v["d"] += 1
        if k["assister"]:
            a = feats.setdefault(k["assister"], {
                "k": 0, "d": 0, "a": 0, "dmg": 0, "surv": 0,
                "k_rounds": set(), "a_rounds": set(), "t_rounds": set(), "s_rounds": set(),
                "kills_by_round": {},
            })
            a["a"] += 1
            a["a_rounds"].add(k["round"])
        if k["traded"]:
            t = feats.setdefault(k["traded"], {
                "k": 0, "d": 0, "a": 0, "dmg": 0, "surv": 0,
                "k_rounds": set(), "a_rounds": set(), "t_rounds": set(), "s_rounds": set(),
                "kills_by_round": {},
            })
            t["t_rounds"].add(k["round"])
    for x in dmg:
        if x["damage"] > 0:
            f = feats.setdefault(x["attacker"], {
                "k": 0, "d": 0, "a": 0, "dmg": 0, "surv": 0,
                "k_rounds": set(), "a_rounds": set(), "t_rounds": set(), "s_rounds": set(),
                "kills_by_round": {},
            })
            f["dmg"] += x["damage"]
    for sid, f in feats.items():
        kast = len(f["k_rounds"] | f["a_rounds"] | f["t_rounds"] | f["s_rounds"])
        mk = [0] * 6
        for rnd, n in f["kills_by_round"].items():
            mk[min(n, 5)] += 1
        mk_raw = (mk[1] + 4 * mk[2] + 9 * mk[3] + 16 * mk[4] + 25 * mk[5]) / r
        f.update({"rounds": r, "kast_n": kast,
                  "kast": kast / r, "surv_rate": f["surv"] / r,
                  "kpr": f["k"] / r, "dpr": f["d"] / r, "apr": f["a"] / r,
                  "adr": f["dmg"] / r, "mk": mk, "mk_raw": mk_raw})
    return feats


# ---------- 最小二乘（纯标准库） ----------

def solve(A, b):
    """解 Ax=b（高斯消元+部分主元）。A 为 n×n，b 长 n。"""
    n = len(b)
    m = [row[:] + [b[i]] for i, row in enumerate(A)]
    for col in range(n):
        piv = max(range(col, n), key=lambda r: abs(m[r][col]))
        m[col], m[piv] = m[piv], m[col]
        if abs(m[col][col]) < 1e-12:
            raise ValueError("奇异矩阵")
        for r in range(col + 1, n):
            f = m[r][col] / m[col][col]
            for c in range(col, n + 1):
                m[r][c] -= f * m[col][c]
    x = [0.0] * n
    for i in range(n - 1, -1, -1):
        x[i] = (m[i][n] - sum(m[i][j] * x[j] for j in range(i + 1, n))) / m[i][i]
    return x


def ols(X, y):
    """X: n×p（含截距列由调用方加），返回 (coef, r2, rmse, maxerr, preds)。
    内部对非常数列做标准化再解，避免 ADR(~1e2) 与 KAST(~1) 量纲差导致的病态。"""
    n, p = len(X), len(X[0])
    means, stds = [], []
    for j in range(p):
        col = [X[i][j] for i in range(n)]
        mu = sum(col) / n
        va = sum((v - mu) ** 2 for v in col) / n
        means.append(mu)
        stds.append(math.sqrt(va) if va > 0 else 0.0)
    Z = [[(X[i][j] - means[j]) / stds[j] if stds[j] > 0 else X[i][j]
          for j in range(p)] for i in range(n)]
    m, q = len(Z), len(Z[0])
    ZtZ = [[sum(Z[i][a] * Z[i][b] for i in range(m)) for b in range(q)] for a in range(q)]
    Zty = [sum(Z[i][a] * y[i] for i in range(m)) for a in range(q)]
    cz = solve(ZtZ, Zty)
    coef = [0.0] * p
    for j in range(p):
        coef[j] = cz[j] / stds[j] if stds[j] > 0 else cz[j]
    if stds[0] == 0:  # 第 0 列是截距 1，吸收标准化列的均值回代
        coef[0] = cz[0] - sum(cz[j] * means[j] / stds[j] for j in range(1, p) if stds[j] > 0)
    preds = [sum(X[i][j] * coef[j] for j in range(p)) for i in range(n)]
    mean = sum(y) / n
    ss_tot = sum((v - mean) ** 2 for v in y)
    ss_res = sum((y[i] - preds[i]) ** 2 for i in range(n))
    rmse = math.sqrt(ss_res / n)
    maxerr = max(abs(y[i] - preds[i]) for i in range(n))
    return coef, 1 - ss_res / ss_tot if ss_tot else 0.0, rmse, maxerr, preds


def nnls(Z, y, iters=5000, lr=None):
    """Lawson-Hanson 式非负最小二乘（投影梯度实现，纯标准库）。
    Z: n×p，y: n。返回 (w, r2, rmse)。"""
    n, p = len(Z), len(Z[0])
    if lr is None:
        scale = sum(Z[i][j] ** 2 for i in range(n) for j in range(p)) / (n * p)
        lr = 0.5 / scale if scale > 0 else 0.01
    w = [0.0] * p
    for _ in range(iters):
        grad = [0.0] * p
        for i in range(n):
            err = sum(Z[i][j] * w[j] for j in range(p)) - y[i]
            for j in range(p):
                grad[j] += err * Z[i][j]
        step = lr / (1 + _ / 500)
        w = [max(0.0, w[j] - step * grad[j] / n) for j in range(p)]
    preds = [sum(Z[i][j] * w[j] for j in range(p)) for i in range(n)]
    mean = sum(y) / n
    ss_tot = sum((v - mean) ** 2 for v in y)
    ss_res = sum((y[i] - preds[i]) ** 2 for i in range(n))
    return w, 1 - ss_res / ss_tot if ss_tot else 0.0, math.sqrt(ss_res / n)


def detect_clutches(kills, rounds, surv):
    """1vX（X>=2）残局获胜检测：用存活表定每轮阵营，按 tick 模拟存活数；
    获胜方某玩家在队友全死后、面对>=2 存活对手拿下本方本轮剩余全部人头。返回 {steamId: 次数}。"""
    out = {}
    side = {(s["round"], s["steamId"]): s["side"] for s in surv}
    players_in_round = {}
    for (rnd, sid), sd in side.items():
        if sd in (SIDES_T, SIDES_CT):
            players_in_round.setdefault(rnd, {})[sid] = sd
    by_round = {}
    for k in kills:
        by_round.setdefault(k["round"], []).append(k)
    for rnd, ks in by_round.items():
        winner = rounds.get(rnd)
        if winner not in (SIDES_T, SIDES_CT):
            continue
        roster = players_in_round.get(rnd, {})
        if len(roster) < 10:
            continue
        ks = sorted(ks, key=lambda k: k["tick"])
        alive = dict(roster)
        credited = set()
        for i, k in enumerate(ks):
            atk, vic = k["attacker"], k["victim"]
            atk_side = roster.get(atk)
            if not atk or atk_side not in (SIDES_T, SIDES_CT) or vic not in alive:
                if vic in alive:
                    del alive[vic]
                continue
            if atk not in alive:
                if vic in alive:
                    del alive[vic]
                continue
            mates = [p for p, s in alive.items() if s == atk_side and p != atk]
            opp = [p for p, s in alive.items() if s != atk_side]
            del alive[vic]
            if atk_side == winner and len(mates) == 0 and len(opp) >= 2 \
                    and atk not in credited:
                later_opp = [x for x in ks[i + 1:]
                             if x["attacker"] and roster.get(x["attacker"]) is not None
                             and roster.get(x["attacker"]) != winner]
                if not later_opp:
                    out[atk] = out.get(atk, 0) + 1
                    credited.add(atk)
    return out


def main_rating_nnls():
    official = load_official()
    tags = sorted({o["tag"] for o in official})
    data = []
    for tag in tags:
        players, kills, rounds, surv, dmg = load_dump(tag)
        feats = player_features(kills, rounds, surv, dmg)
        # 形状锁定：slope=1.0 offset=-0.05（5v5=48.75% 对得上 HLTV 48.8%，corr 最优）
        sw = compute_swing(kills, rounds, surv, slope=1.0, offset=-0.05,
                           with_residual=False)
        r = len(rounds)
        mapping = match_names([o["player"] for o in official if o["tag"] == tag], players)
        for o in [x for x in official if x["tag"] == tag]:
            sid = mapping[o["player"]]
            if sid is None:
                continue
            f = feats[sid]
            data.append((o, f, sw.get(sid, 0.0) / r))
    y = [o["rating"] for o, _, _ in data]
    cols = ["kpr", "adr", "kast", "surv_rate", "mk_raw"]
    raw = [[f[c] for c in cols] + [s] for _, f, s in data]
    off_sw = [o["swing"] / 100 for o, _, _ in data]
    names = cols + ["swing"]
    avgs = [sum(row[j] for row in raw) / len(raw) for j in range(6)]
    print("样本均值:", {k: round(v, 4) for k, v in zip(names, avgs)})
    Z = [[row[j] / avgs[j] for j in range(5)] for row in raw]
    # swing 零和（均值≈0）不能按均值归一：本地/official 共用一把尺子（本地 swing 均方根）
    sw_loc = [s for _, _, s in data]
    rms = math.sqrt(sum(v * v for v in sw_loc) / len(sw_loc))
    Z = [row + [sw_loc[i] / rms] for i, row in enumerate(Z)]
    Zoff = [row[:5] + [off_sw[i] / rms] for i, row in enumerate(Z)]
    print("\n-- oracle（官方swing）归一化 OLS（带截距）--")
    Xo = [[1.0] + row for row in Zoff]
    coef, r2, rmse, maxe, _ = ols(Xo, y)
    print(f"R²={r2:.4f} RMSE={rmse:.4f} maxerr={maxe:.4f}")
    print("截距:", round(coef[0], 4), "权重:", {k: round(v, 4) for k, v in zip(names, coef[1:])},
          "和:", round(sum(coef[1:]), 4))
    print("\n-- oracle（官方swing）NNLS（无截距，w>=0）--")
    w, r2, rmse = nnls(Zoff, y)
    print(f"R²={r2:.4f} RMSE={rmse:.4f}")
    print("权重:", {k: round(v, 4) for k, v in zip(names, w)}, "和:", round(sum(w), 4))
    print("\n-- 本地swing 归一化 OLS（带截距）--")
    Xl = [[1.0] + row for row in Z]
    coef, r2, rmse, maxe, _ = ols(Xl, y)
    print(f"R²={r2:.4f} RMSE={rmse:.4f} maxerr={maxe:.4f}")
    print("截距:", round(coef[0], 4), "权重:", {k: round(v, 4) for k, v in zip(names, coef[1:])},
          "和:", round(sum(coef[1:]), 4))
    print("\n-- 本地swing NNLS（无截距，w>=0，可部署形态）--")
    w, r2, rmse = nnls(Z, y)
    print(f"R²={r2:.4f} RMSE={rmse:.4f}")
    print("权重:", {k: round(v, 4) for k, v in zip(names, w)}, "和:", round(sum(w), 4))
    pred = [sum(Z[i][j] * w[j] for j in range(6)) for i in range(len(y))]
    errs = sorted(((abs(y[i] - pred[i]), data[i][0]["tag"], data[i][0]["player"],
                    y[i], pred[i]) for i in range(len(y))), reverse=True)[:6]
    for e, t, p, yy, pp in errs:
        print(f"    误差: {t} {p} 官方{yy:.2f} 预测{pp:.2f} 差{e:.3f}")
    # 方差贡献分解：|w|*std(z)
    print("方差贡献份额:", end=" ")
    tot = 0.0
    shares = []
    for j in range(6):
        col = [Z[i][j] for i in range(len(Z))]
        mu = sum(col) / len(col)
        sd = math.sqrt(sum((v - mu) ** 2 for v in col) / len(col))
        shares.append(abs(w[j]) * sd)
        tot += shares[-1]
    print({k: round(v / tot, 3) for k, v in zip(names, shares)})

    # ---- 精简形态对照：4项 / 5项（看 mk/surv 是否多余）----
    for keep, label in (([0, 1, 2, 5], "4项(kpr,adr,kast,swing)"),
                        ([0, 1, 2, 4, 5], "5项(+mk)"),
                        ([0, 1, 2, 3, 5], "5项(+surv)")):
        Zs = [[row[j] for j in keep] for row in Z]
        ws, r2s, rmses = nnls(Zs, y)
        print(f"{label} NNLS: R²={r2s:.4f} RMSE={rmses:.4f} "
              f"权重={[round(v, 3) for v in ws]} 和={round(sum(ws), 3)}")

    # ---- 残局加成诊断：第 7 特征（clutch/回合）能解释多少残差？----
    print("\n-- 诊断：+clutch 特征 --")
    cl = {}
    for tag in tags:
        players, kills, rounds, surv, dmg = load_dump(tag)
        mapping = match_names([o["player"] for o in official if o["tag"] == tag], players)
        inv = {v: k for k, v in mapping.items() if v}
        for sid, n in detect_clutches(kills, rounds, surv).items():
            if sid in inv:
                cl[(tag, inv[sid])] = n / len(rounds)
    ncl = sum(cl.values())
    print(f"10 张图共检测到 1vX 残局胜: {ncl:.0f} 次，涉及 {len(cl)} 人次")
    Xc = [row + [cl.get((o["tag"], o["player"]), 0.0)] for (o, _, _), row in
          zip(data, [[f[c] for c in cols] + [s] for _, f, s in data])]
    try:
        coef, r2, rmse, maxe, _ = ols([[1.0] + row for row in Xc], y)
        print(f"5项+本地swing+clutch OLS: R²={r2:.4f} RMSE={rmse:.4f} "
              f"clutch系数={coef[-1]:.3f}")
    except ValueError as e:
        print("clutch OLS 奇异:", e)

    # ---- 官方锚定形态：Kill 0.25 + Swing 0.33 固定（HLTV 公告值），其余 NNLS ----
    print("\n-- 官方锚定形态（Kill=0.25 Swing=0.33 固定，adr/kast/surv/mk NNLS）--")
    xs = [s for _, _, s in data]
    ys = [o["swing"] / 100 for o, _, _ in data]
    m = sum(a * b for a, b in zip(xs, ys)) / sum(a * a for a in xs)
    S = 0.05 / m
    print(f"swing 过原点斜率 m={m:.4f} → SwingScale S={S:.4f}")
    fixed = [0.25 * (f["kpr"] / avgs[0]) + 0.33 * (1 + s / S)
             for (_, f, s) in data]
    resid = [yy - fx for yy, fx in zip(y, fixed)]
    Z4 = [[f["adr"] / avgs[1], f["kast"] / avgs[2], f["surv_rate"] / avgs[3],
           f["mk_raw"] / avgs[4]] for _, f, _ in data]
    w4, r2, rmse = nnls(Z4, resid)
    pred = [fixed[i] + sum(Z4[i][j] * w4[j] for j in range(4)) for i in range(len(y))]
    mean = sum(y) / len(y)
    ss_tot = sum((v - mean) ** 2 for v in y)
    ss_res = sum((y[i] - pred[i]) ** 2 for i in range(len(y)))
    print(f"R²={1 - ss_res / ss_tot:.4f} RMSE={math.sqrt(ss_res / len(y)):.4f}")
    print("adr/kast/surv/mk 权重:",
          {k: round(v, 4) for k, v in zip(["adr", "kast", "surv", "mk"], w4)},
          "和:", round(0.58 + sum(w4), 4))


def main():
    official = load_official()
    tags = sorted({o["tag"] for o in official})
    print(f"官方行数: {len(official)}, 地图数: {len(tags)}")

    all_rows = []  # (official, feats, swingVariants)
    check_only = len(sys.argv) > 1 and sys.argv[1] == "check"
    mismatches = []
    for tag in tags:
        players, kills, rounds, surv, dmg = load_dump(tag)
        feats = player_features(kills, rounds, surv, dmg)
        names = [o["player"] for o in official if o["tag"] == tag]
        mapping = match_names(names, players)
        for n, sid in mapping.items():
            if sid is None:
                mismatches.append((tag, n, sorted(set(players.values()))))
        sw_v0 = compute_swing(kills, rounds, surv)
        print(f"{tag}: demo回合={len(rounds)} 击杀={len(kills)} "
              f"伤害={len(dmg)} 玩家={len(players)}")
        for o in [x for x in official if x["tag"] == tag]:
            sid = mapping[o["player"]]
            f = feats.get(sid) if sid else None
            all_rows.append((o, f, sw_v0.get(sid, 0.0) if sid else None))

    if mismatches:
        print("\n!! 名字对不上:")
        for tag, n, cands in mismatches:
            print(f"  {tag} 官方'{n}' demo候选={cands}")

    # ---- 校验：demo K/D/ADR/回合 vs 官方 ----
    print("\n== demo-vs-官方校验 ==")
    print("tag player demoK-offK demoD-offD demoADR-offADR demoR rounds_ok")
    bad = 0
    for o, f, _ in all_rows:
        if f is None:
            print(f"{o['tag']} {o['player']} 无demo数据")
            bad += 1
            continue
        dk, dd = f["k"] - o["k"], f["d"] - o["d"]
        da = f["adr"] - o["adr"]
        kast_ok = abs(f["kast"] - o["kast"]) * f["rounds"]
        flag = "" if (dk == 0 and dd == 0 and abs(da) < 0.15) else "  <-- 差"
        if flag:
            bad += 1
        print(f"{o['tag']} {o['player']} {dk:+d} {dd:+d} {da:+.2f} "
              f"kast差{kast_ok:.2f}回合{flag}")
    print(f"校验差异行: {bad}/{len(all_rows)}")
    if check_only:
        return

    # ---- Swing 变体对比 ----
    print("\n== Swing变体: 官方swing均值 vs 本地swing均值 ==")
    for tag in tags:
        players, kills, rounds, surv, dmg = load_dump(tag)
        r = len(rounds)
        variants = {
            "v0(slope.6+残值contrib)": compute_swing(kills, rounds, surv),
            "slope.6无残值": compute_swing(kills, rounds, surv, with_residual=False),
            "slope1.0+残值": compute_swing(kills, rounds, surv, slope=1.0, offset=0.0),
            "slope1.0无残值": compute_swing(kills, rounds, surv, slope=1.0, offset=0.0,
                                            with_residual=False),
            "slope.4+残值": compute_swing(kills, rounds, surv, slope=0.4, offset=0.0),
        }
        off = [o for o in official if o["tag"] == tag]
        names = [o["player"] for o in off]
        mapping = match_names(names, {s["steamId"]: s["steamId"] for s in surv} | players)
        line = f"{tag} 官方swing均值和={sum(o['swing'] for o in off):+.2f} |"
        for vname, sw in variants.items():
            s = sum(sw.get(mapping[n], 0.0) / r * 100 for n in names if mapping[n])
            line += f" {vname}={s:+.2f} |"
        print(line)

    # 全样本：官方swing(~y) 对 本地swing(~x)，过原点斜率 + R²
    print("\n== 全样本 Swing 拟合（y=官方swing%/回合，x=本地swing/回合）==")
    for vname, kw in {
        "v0": {},
        "无残值": {"with_residual": False},
        "slope1.0+残值": {"slope": 1.0, "offset": 0.0},
        "slope1.0无残值": {"slope": 1.0, "offset": 0.0, "with_residual": False},
    }.items():
        xs, ys = [], []
        for tag in tags:
            players, kills, rounds, surv, dmg = load_dump(tag)
            r = len(rounds)
            sw = compute_swing(kills, rounds, surv, **kw)
            mapping = match_names([o["player"] for o in official if o["tag"] == tag], players)
            for o in [x for x in official if x["tag"] == tag]:
                sid = mapping[o["player"]]
                if sid is None:
                    continue
                xs.append(sw.get(sid, 0.0) / r * 100)
                ys.append(o["swing"])
        # 过原点
        slope = sum(a * b for a, b in zip(xs, ys)) / sum(a * a for a in xs)
        pred = [slope * a for a in xs]
        mean = sum(ys) / len(ys)
        r2 = 1 - sum((ys[i] - pred[i]) ** 2 for i in range(len(ys))) / sum(
            (v - mean) ** 2 for v in ys)
        # 带截距
        (c0, c1), r2b, rmse, maxe, _ = ols([[1, a] for a in xs], ys)
        print(f"{vname}: 过原点斜率={slope:.3f} R²={r2:.4f} | "
              f"带截距 y={c1:.3f}x{c0:+.3f} R²={r2b:.4f} RMSE={rmse:.3f} maxerr={maxe:.3f}")

    # ---- Swing 表格形状网格搜索（用相关系数选型，scale 由 SwingScale 吸收）----
    print("\n== Swing形状网格搜索（无残值，y=官方swing，x=本地swing，指标=相关系数）==")
    tags2 = tags

    def load_all():
        out = []
        for tag in tags2:
            players, kills, rounds, surv, dmg = load_dump(tag)
            out.append((tag, players, kills, rounds, surv))
        return out
    cache = load_all()
    best = []
    for slope in [0.3, 0.4, 0.5, 0.6, 0.8, 1.0, 1.2, 1.5]:
        for offset in [-0.2, -0.1, -0.05, 0.0, 0.1]:
            xs, ys = [], []
            for tag, players, kills, rounds, surv in cache:
                r = len(rounds)
                sw = compute_swing(kills, rounds, surv, slope=slope, offset=offset,
                                   with_residual=False)
                mapping = match_names([o["player"] for o in official if o["tag"] == tag],
                                      players)
                for o in [x for x in official if x["tag"] == tag]:
                    sid = mapping[o["player"]]
                    if sid is None:
                        continue
                    xs.append(sw.get(sid, 0.0) / r * 100)
                    ys.append(o["swing"])
            n = len(xs)
            mx, my = sum(xs) / n, sum(ys) / n
            cov = sum((xs[i] - mx) * (ys[i] - my) for i in range(n))
            vx = sum((v - mx) ** 2 for v in xs)
            vy = sum((v - my) ** 2 for v in ys)
            corr = cov / math.sqrt(vx * vy)
            best.append((corr, slope, offset))
    best.sort(reverse=True)
    for corr, slope, offset in best[:8]:
        print(f"  slope={slope} offset={offset:+.2f} corr={corr:.4f}")

    # ---- Rating 回归 ----
    print("\n== Rating30 回归（y=官方rating）==")
    data = [(o, f) for o, f, _ in all_rows if f is not None]
    y = [o["rating"] for o, _ in data]
    feat_sets = {
        "5项(kpr,adr,kast,surv,mk)": lambda o, f: [
            f["kpr"], f["adr"], f["kast"], f["surv_rate"], f["mk_raw"]],
        "5项+swing官方": lambda o, f: [
            f["kpr"], f["adr"], f["kast"], f["surv_rate"], f["mk_raw"], o["swing"] / 100],
    }
    # 本地swing逐图重算（均值/回合小数）
    local_sw = {}
    for tag in tags:
        players, kills, rounds, surv, dmg = load_dump(tag)
        sw = compute_swing(kills, rounds, surv)
        mapping = match_names([o["player"] for o in official if o["tag"] == tag], players)
        for o in [x for x in official if x["tag"] == tag]:
            sid = mapping[o["player"]]
            local_sw[(tag, o["player"])] = sw.get(sid, 0.0) / len(rounds) if sid else 0.0
    feat_sets["5项+swing本地"] = lambda o, f: [
        f["kpr"], f["adr"], f["kast"], f["surv_rate"], f["mk_raw"],
        local_sw[(o["tag"], o["player"])]]

    for fname, fn in feat_sets.items():
        X = [[1.0] + fn(o, f) for o, f in data]
        coef, r2, rmse, maxe, pred = ols(X, y)
        print(f"{fname}: R²={r2:.4f} RMSE={rmse:.4f} maxerr={maxe:.4f}")
        print(f"  coef(截距优先)={[round(c, 5) for c in coef]}")
        errs = sorted(((abs(y[i] - pred[i]), data[i][0]["tag"], data[i][0]["player"],
                        y[i], pred[i]) for i in range(len(y))), reverse=True)[:5]
        for e, t, p, yy, pp in errs:
            print(f"    最大误差: {t} {p} 官方{yy:.2f} 预测{pp:.2f} 差{e:.3f}")


def main_centered():
    """终版可部署形态：swing 列改用居中形态 (1+sw/S)，S 固定为校准值；
    6 项全部按样本均值归一，NNLS 无截距。输出可直接落码的权重+均值。"""
    official = load_official()
    tags = sorted({o["tag"] for o in official})
    data = []
    for tag in tags:
        players, kills, rounds, surv, dmg = load_dump(tag)
        feats = player_features(kills, rounds, surv, dmg)
        sw = compute_swing(kills, rounds, surv, slope=1.0, offset=-0.05,
                           with_residual=False)
        r = len(rounds)
        mapping = match_names([o["player"] for o in official if o["tag"] == tag], players)
        for o in [x for x in official if x["tag"] == tag]:
            sid = mapping[o["player"]]
            if sid is None:
                continue
            f = feats[sid]
            data.append((o, f, sw.get(sid, 0.0) / r))
    y = [o["rating"] for o, _, _ in data]
    # SwingScale 校准：官方 swing = m * 本地 swing（过原点），S = 0.05 / m
    xs = [s for _, _, s in data]
    ys = [o["swing"] / 100 for o, _, _ in data]
    m = sum(a * b for a, b in zip(xs, ys)) / sum(a * a for a in xs)
    S = 0.05 / m
    print(f"swing 校准: m={m:.4f} S={S:.4f}")
    cols = ["kpr", "adr", "kast", "surv_rate", "mk_raw"]
    names = cols + ["swing_c"]
    avgs = [sum(f[c] for _, f, _ in data) / len(data) for c in cols]
    Z = [[f[c] / avgs[j] for j, c in enumerate(cols)] + [1 + s / S]
         for _, f, s in data]
    w, r2, rmse = nnls(Z, y)
    print(f"居中6项 NNLS: R²={r2:.4f} RMSE={rmse:.4f}")
    print("均值:", {k: round(v, 4) for k, v in zip(cols, avgs)}, "S:", round(S, 4))
    print("权重:", {k: round(v, 4) for k, v in zip(names, w)},
          "和:", round(sum(w), 4))
    pred = [sum(Z[i][j] * w[j] for j in range(6)) for i in range(len(y))]
    mean = sum(y) / len(y)
    errs = sorted(((abs(y[i] - pred[i]), data[i][0]["tag"], data[i][0]["player"],
                   y[i], pred[i]) for i in range(len(y))), reverse=True)[:6]
    for e, t, p, yy, pp in errs:
        print(f"    误差: {t} {p} 官方{yy:.2f} 预测{pp:.2f} 差{e:.3f}")
    # 方差贡献份额
    tot, shares = 0.0, []
    for j in range(6):
        col = [Z[i][j] for i in range(len(Z))]
        mu = sum(col) / len(col)
        sd = math.sqrt(sum((v - mu) ** 2 for v in col) / len(col))
        shares.append(abs(w[j]) * sd)
        tot += shares[-1]
    print("方差贡献份额:", {k: round(v / tot, 3) for k, v in zip(names, shares)})


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "nnls":
        main_rating_nnls()
    elif len(sys.argv) > 1 and sys.argv[1] == "centered":
        main_centered()
    else:
        main()
