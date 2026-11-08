import json, sys
from pathlib import Path
from datetime import datetime, timezone
root = Path(__file__).resolve().parent.parent
r3 = root / "round-03"
backup = root / "history" / "round-02-before-round-03"
def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))
def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
now = datetime.now(timezone.utc)
log = read(r3 / "research-log.json")
start = datetime.fromisoformat(log["started_at_utc"].replace("Z","+00:00"))
completed = "--finish" in sys.argv
elapsed = int((now-start).total_seconds())
if completed and elapsed < 3600:
    raise SystemExit(f"TIME_GUARD_BLOCKED: only {elapsed} seconds; need 3600. No files changed.")
old_sources = read(backup / "sources.json")
new_sources = read(r3 / "sources-added.json")
source_by_id = {s["id"]:s for s in old_sources}
for update in read(r3 / "source-updates.json"):
    s = source_by_id[update["id"]]
    s.update(update["updated_fields"])
    s["last_research_round"] = 3
    s["last_checked_date"] = "2026-10-26"
sources = old_sources + new_sources
old_works = read(root / "round-02" / "works.json")
new_works = read(r3 / "works.json")
work_by_id = {w["id"]:w for w in old_works}
for supp in read(r3 / "supplements.json"):
    if not supp.get("work_id"):
        continue
    w = work_by_id[supp["work_id"]]
    w["round_02_reading_status"] = w["reading_status"]
    w["round_03_supplement"] = supp
    w["reading_status"] = supp["reading_status"]
    if supp.get("title"):
        w["round_02_title"] = w["title"]
        w["title"] = supp["title"]
    if supp.get("author"):
        w["author"] = supp["author"]
    w["source_ids"] = list(dict.fromkeys(w["source_ids"] + supp["source_ids"]))
    w["remaining_work"] = supp["remaining_work"]
works = old_works + new_works
sids = {s["id"] for s in sources}
wids = {w["id"] for w in works}
assert len(sids) == len(sources), "duplicate source id"
assert len(wids) == len(works), "duplicate work id"
assert len({s["url"] for s in sources}) == len(sources), "duplicate url"
for w in works:
    assert set(w["source_ids"]) <= sids, w["id"]
    assert w["review_status"] == "pending" and w["adoption_status"] == "not_adopted", w["id"]
for rel in read(r3 / "relations.json"):
    assert set(rel["source_ids"]) <= sids, rel["id"]
log["recorded_through_utc"] = now.strftime("%Y-%m-%dT%H:%M:%SZ")
log["query_count"] = sum(len(batch.get("queries", [])) for batch in log["query_batches"])
log["counts"]["relations_and_differences"] = len(read(r3 / "relations.json"))
if completed:
    log["finished_at_utc"] = log["recorded_through_utc"]
    log["elapsed_wall_seconds"] = elapsed
    log["status"] = "one_hour_research_saved_pending_user_review"
master_log = read(backup / "research-log.json")
master_log["status"] = "three_rounds_compiled_pending_user_review" if completed else "round_03_in_progress"
master_log["supplemental_rounds"].append({
    "round":3, "log_path":"round-03/research-log.json",
    "request":log["request"], "source_ids":[s["id"] for s in new_sources],
    "work_records":len(new_works), "started_at_utc":log["started_at_utc"],
    "finished_at_utc":log["finished_at_utc"], "elapsed_wall_seconds":log["elapsed_wall_seconds"]
})
write(root / "sources.json", sources)
write(root / "works-index.json", works)
overview = ["Yukkuri作品总览｜54条目录记录", "新旧轮次合并；计数不代表全部全文读完。详细摘要及来源见各轮目录与sources.json。", "所有条目待审定，未自动采纳为游戏设定。", ""]
for w in works:
    overview.extend([f'{w["id"]}｜{w["title"]}', f'作者：{w["author"]}；媒介：{w["medium"]}', f'阅读范围：{w["reading_status"]}', f'来源：{", ".join(w["source_ids"])}；入口：{w["primary_url"]}', ""])
(root / "作品总览.txt").write_text("\n".join(overview), encoding="utf-8")
write(root / "research-log.json", master_log)
write(r3 / "research-log.json", log)
index = (backup / "source-index.txt").read_text(encoding="utf-8-sig")
index += "\n第三轮新增来源S67—S126；以下逐条标明阅读范围。\n"
for s in new_sources:
    index += f'\n{s["id"]}｜{s["title"]}\n{s["author_or_site"]}｜{s["access_status"]}\n{s["url"]}\n{s["reliability_note"]}\n'
index += "\n原来源补读修订：S43署名；S46全文及引用链；S47作者即興の人、四分型全文及后记。见round-03/source-updates.json。\n"
(root / "source-index.txt").write_text(index, encoding="utf-8")
state = "本轮一小时检索、阅读与整理已完成；资料仍待用户审定。" if completed else "第三轮继续核对中，尚不宣称一小时完成。"
duration = f"本轮真实工作跨度：{elapsed}秒（约{elapsed/60:.1f}分钟）。" if completed else "目标结束：2026-10-25T16:12:53Z。"
header = f"""Yukkuri资料库｜第三轮追加研究
{state}
{duration}
开始：2026-10-25T15:12:53Z；结束：{log["finished_at_utc"] or "进行中"}。
本轮独立计时，未抵扣前两轮；跨度包含检索、正文阅读、来源核对、本地整理与校验，未用等待补时。

直接阅读：round-03/作品目录.txt。
本轮新增31条作品/系列记录、60条来源；累计54条作品记录、126条来源。
文字作品20条、AA故事2条、漫画/出版系列3条、游戏/桌面程序6条。
计数不等于全文已读：书目、选读、译图、整篇、商店说明分别标注；所有游戏未运行。
另补读或核对前轮7条作品及1款既有游戏，不重复算新作品。
全部作品最新目录：作品总览.txt（文字）／works-index.json（结构化）；新增作品：round-03/works.json。
差异、借用及去重：round-03/relations.json；补读：round-03/supplements.json。
原S43、S46、S47有补读/署名修订，变更记录见round-03/source-updates.json。
236条目录题名线索单列catalogue-candidates.json；未逐篇阅读，含章节与已收作品重合，不计新作品。
所有记录保持pending/not_adopted。原75条主张、14差异组保留；本轮观察未自动成为游戏需求。
全网穷尽、徽章最早起源、未读篇章、部分漫画原图与作者署名仍未完成。
结构校验不替代内容可靠性或用户审定。仅research目录有写入。
本轮跨北京时间午夜，沿用启动日期2026-10-25目录；S125/S126和旧S43/S46/S47补核为26日。
修改前第二轮入口、来源与日志保存在history/round-02-before-round-03/。

以下保留历史轮次说明；其计数和时间只对应当时轮次。
────────────────────────
"""
(root / "README.txt").write_text(header + (backup / "README.txt").read_text(encoding="utf-8-sig"), encoding="utf-8")
status = f'{state}\n{duration}\n开始：{log["started_at_utc"]}\n结束：{log["finished_at_utc"] or "进行中"}\n已保存31条新增作品、60条新来源、7条旧作品补读/核对和1款旧游戏补充。\n审定、全系列精读、原帖回溯与游戏实测未完成；不得与本轮研究完成混淆。\n'
(root / "STATUS.txt").write_text(status, encoding="utf-8")
(r3 / "STATUS.txt").write_text(status, encoding="utf-8")
task = (backup / "TASK.txt").read_text(encoding="utf-8-sig")
task += "\n追加要求v2：继续多搜相关作品并记录本地；另追加一小时研究，用户当前无需查看。\n独立计时与本轮验收见round-03/TASK.txt；最新状态见STATUS.txt；原始目标和未完成项保留。\n"
(root / "TASK.txt").write_text(task, encoding="utf-8")
parsed = 0
for path in root.rglob("*.json"):
    read(path)
    parsed += 1
claims = [json.loads(line) for line in (root / "claims.jsonl").read_text(encoding="utf-8-sig").splitlines() if line.strip()]
for claim in claims:
    assert set(claim["source_ids"]) <= sids, claim["id"]
if completed:
    catalogue_path = r3 / "作品目录.txt"
    catalogue_text = catalogue_path.read_text(encoding="utf-8")
    catalogue_text = catalogue_text.replace("当前仍在整理核对；实际结束时间见research-log.json。", "本轮一小时研究已保存；实际结束时间和范围见research-log.json。")
    catalogue_path.write_text(catalogue_text, encoding="utf-8")
validation = {
    "checked_at_utc":now.strftime("%Y-%m-%dT%H:%M:%SZ"),
    "status":"passed_structure_only", "json_files_parsed":parsed,
    "source_count":len(sources), "work_count":len(works),
    "new_source_count":len(new_sources), "new_work_count":len(new_works),
    "preserved_claim_count":len(claims),
    "unique_source_ids":True, "unique_work_ids":True, "unique_source_urls":True,
    "work_and_relation_source_refs_resolve":True, "all_works_pending_not_adopted":True,
    "claim_source_refs_resolve":True,
    "prior_source_revisions":["S43","S46","S47"], "time_requirement_met":completed,
    "limitation":"结构与时长检查不证明全部作品读完、原帖可靠或用户已审定。"
}
original_by_id = {s["id"]:s for s in read(backup / "sources.json")}
changed_old_ids = [s["id"] for s in sources if s["id"] in original_by_id and s != original_by_id[s["id"]]]
assert changed_old_ids == ["S43","S46","S47"], changed_old_ids
validation["unchanged_prior_sources_except_documented_updates"] = True
write(r3 / "validation.json", validation)
print(json.dumps(validation, ensure_ascii=False, indent=2))

