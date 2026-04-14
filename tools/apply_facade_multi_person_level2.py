"""
One-shot patch for Assets/Scenes/Level 2.unity:
- Intro is shared per window (W{i}_Intro only); no w{i}_p{j}_intro.
- Clone W{i}_Details only as w{i}_p{j}_details (j>=1), inactive.
- Append new details root RectTransforms under FacadeRescueFullscreenRoot (1719071148).
- Clone PortraitSlot0 four times as PortraitSlot3..6; extend PortraitRow children + controller portraitSlots (7).
- Idempotent: skips if w0_p1_details already exists.

Run from repo root: python tools/apply_facade_multi_person_level2.py
"""

from __future__ import annotations

import logging
import re
import shutil
import sys
from pathlib import Path

logger = logging.getLogger(__name__)

SCENE = Path("Assets/Scenes/Level 2.unity")
FULLSCREEN_ROOT_RT = "1719071148"
PORTRAIT_ROW_RT = "1983686342"
CONTROLLER_MB = "1522480015"
PORTRAIT_TEMPLATE_GO = "234394715"  # PortraitSlot0
PEOPLE_PER_WINDOW = (2, 3, 2)
WINDOW_DETAILS_NAMES = ("W0_Details", "W1_Details", "W2_Details")
IMAGE_SCRIPT_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"


def parse_blocks(text: str) -> dict[str, str]:
    blocks: dict[str, str] = {}
    pat = re.compile(r"^--- !u!\d+ &(\d+)\r?\n", re.M)
    ms = list(pat.finditer(text))
    for i, m in enumerate(ms):
        fid = m.group(1)
        start = m.start()
        end = ms[i + 1].start() if i + 1 < len(ms) else len(text)
        blocks[fid] = text[start:end]
    return blocks


def max_numeric_file_ids(text: str, cap: int = (2**31) - 1) -> int:
    """Ignore Unity sentinel / huge pseudo-ids (e.g. &9223372036854775807)."""
    nums: list[int] = []
    for m in re.finditer(r"^--- !u!\d+ &(\d+)\r?$", text, re.M):
        v = int(m.group(1))
        if v <= cap:
            nums.append(v)
    for m in re.finditer(r"\{fileID: (\d+)\}", text):
        v = int(m.group(1))
        if v <= cap:
            nums.append(v)
    return max(nums) if nums else 0


def gameobject_names(blocks: dict[str, str]) -> dict[str, str]:
    """fileID of GameObject -> m_Name."""
    out: dict[str, str] = {}
    for fid, blk in blocks.items():
        if not blk.startswith("--- !u!1 &"):
            continue
        m = re.search(r"^  m_Name: (.+)\r?$", blk, re.M)
        if m:
            out[fid] = m.group(1).strip()
    return out


def find_go_id_by_name(names: dict[str, str], target: str) -> str | None:
    t = target.casefold()
    for fid, n in names.items():
        if n.casefold() == t:
            return fid
    return None


def collect_closure_from_go(blocks: dict[str, str], root_go_id: str) -> set[str]:
    """All YAML blocks (GameObjects, Transforms, components) under root GO."""
    stack = [root_go_id]
    seen_go: set[str] = set()
    ids: set[str] = set()

    def child_transform_ids(rt_blk: str) -> list[str]:
        if "m_Children:" not in rt_blk:
            return []
        tail = rt_blk.split("m_Children:", 1)[1]
        if "m_Father:" not in tail:
            return []
        sec = tail.split("m_Father:", 1)[0]
        return re.findall(r"- \{fileID: (\d+)\}", sec)

    while stack:
        go_id = stack.pop()
        if go_id in seen_go or go_id not in blocks:
            continue
        seen_go.add(go_id)
        ids.add(go_id)
        go_blk = blocks[go_id]
        comp_ids = re.findall(r"^\s+- component: \{fileID: (\d+)\}", go_blk, re.M)
        for cid in comp_ids:
            if cid not in blocks:
                continue
            ids.add(cid)
            cblk = blocks[cid]
            if "m_Children:" not in cblk or "m_GameObject:" not in cblk:
                continue
            for tid in child_transform_ids(cblk):
                if tid not in blocks:
                    continue
                tblk = blocks[tid]
                gm = re.search(r"m_GameObject: \{fileID: (\d+)\}", tblk)
                if gm:
                    stack.append(gm.group(1))
    return ids


def remap_block(block: str, id_map: dict[str, str]) -> str:
    def map_fid(m: re.Match[str]) -> str:
        old = m.group(1)
        if old in id_map:
            return "{fileID: " + id_map[old] + "}"
        return m.group(0)

    out = re.sub(r"\{fileID: (\d+)\}", map_fid, block)
    m0 = re.match(r"^(--- !u!\d+ &)(\d+)(\r?\n)", out)
    if m0:
        old_hdr = m0.group(2)
        if old_hdr in id_map:
            out = m0.group(1) + id_map[old_hdr] + m0.group(3) + out[m0.end() :]
    return out


def set_gameobject_name_and_active(block: str, new_name: str, active: int) -> str:
    out = re.sub(r"(^  m_Name: ).+$", r"\g<1>" + new_name, block, count=1, flags=re.M)
    out = re.sub(r"(^  m_IsActive: )\d+$", r"\g<1>" + str(active), out, count=1, flags=re.M)
    return out


def root_rect_id_for_go(blocks: dict[str, str], go_id: str) -> str | None:
    go_blk = blocks[go_id]
    comp_ids = re.findall(r"^\s+- component: \{fileID: (\d+)\}", go_blk, re.M)
    for cid in comp_ids:
        b = blocks.get(cid, "")
        if re.match(r"^--- !u!224 &", b):
            return cid
    return None


def image_mono_id_for_portrait_go(blocks: dict[str, str], go_id: str) -> str | None:
    go_blk = blocks[go_id]
    comp_ids = re.findall(r"^\s+- component: \{fileID: (\d+)\}", go_blk, re.M)
    for cid in comp_ids:
        b = blocks.get(cid, "")
        if "MonoBehaviour:" in b and IMAGE_SCRIPT_GUID in b:
            return cid
    return None


def append_children_list(block: str, new_child_rect_ids: list[str]) -> str:
    m = re.search(r"(^  m_Children:\s*\n)((?:  - \{fileID: \d+\}\s*\n)+)", block, re.M)
    if not m:
        raise RuntimeError("m_Children block not found")
    existing = m.group(2)
    extra = "".join(f"  - {{fileID: {rid}}}\n" for rid in new_child_rect_ids)
    return block[: m.start(2)] + existing + extra + block[m.end(2) :]


def replace_portrait_slots_in_controller(block: str, image_ids: list[str]) -> str:
    lines = block.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    replaced = False
    while i < len(lines):
        line = lines[i]
        out.append(line)
        if line.strip() == "portraitSlots:":
            i += 1
            while i < len(lines) and re.match(r"^\s+- \{fileID: \d+\}\s*$", lines[i]):
                i += 1
            for img in image_ids:
                out.append(f"  - {{fileID: {img}}}\n")
            replaced = True
            continue
        i += 1
    if not replaced:
        raise RuntimeError("portraitSlots not found in controller block")
    return "".join(out)


def inject_people_per_window(block: str) -> str:
    if re.search(r"^\s*peoplePerWindow:", block, re.M):
        return block
    needle = "  portraitSlots:\n"
    idx = block.find(needle)
    if idx == -1:
        raise RuntimeError("portraitSlots anchor not found for peoplePerWindow inject")
    insert_at = idx + len(needle)
    # Skip existing portrait slot lines
    rest = block[insert_at:]
    m = re.match(r"((?:  - \{fileID: \d+\}\r?\n)+)", rest)
    if not m:
        raise RuntimeError("portraitSlots list malformed")
    after_slots = insert_at + m.end(1)
    ppl = "  peoplePerWindow:\n" + "".join(f"  - {n}\n" for n in PEOPLE_PER_WINDOW)
    return block[:after_slots] + ppl + block[after_slots:]


def main() -> int:
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
    root = Path(__file__).resolve().parents[1]
    scene_path = root / SCENE
    if not scene_path.is_file():
        logger.error("Scene not found: %s", scene_path)
        return 1

    text = scene_path.read_text(encoding="utf-8")
    if "w0_p1_details" in text:
        logger.info("Already patched (w0_p1_details present). Nothing to do.")
        return 0

    blocks = parse_blocks(text)
    names = gameobject_names(blocks)
    if find_go_id_by_name(names, "w0_p1_details"):
        logger.info("Duplicate details UI already present. Nothing to do.")
        return 0

    bak = scene_path.with_suffix(".unity.bak_facade_multi")
    shutil.copy2(scene_path, bak)
    logger.info("Backup: %s", bak)

    next_id = max_numeric_file_ids(text) + 1
    append_blocks: list[str] = []
    new_facade_child_rects: list[str] = []
    new_portrait_rects: list[str] = []
    portrait_image_ids: list[str] = []

    # --- Duplicate details only per extra person (intro 同窗共用 w{i}_intro) ---
    for wi, n_people in enumerate(PEOPLE_PER_WINDOW):
        det_go = find_go_id_by_name(names, WINDOW_DETAILS_NAMES[wi])
        if not det_go:
            logger.error("Missing template for window %s details=%s", wi, det_go)
            return 1
        for pj in range(1, n_people):
            det_name = f"w{wi}_p{pj}_details"
            template_go = det_go
            new_name = det_name
            old_ids = collect_closure_from_go(blocks, template_go)
            sorted_old = sorted(old_ids, key=int)
            id_map: dict[str, str] = {}
            cur = next_id
            for oid in sorted_old:
                id_map[oid] = str(cur)
                cur += 1
            next_id = cur

            emitted: list[str] = []
            for oid in sorted_old:
                raw = blocks[oid]
                remapped = remap_block(raw, id_map)
                if oid == template_go:
                    remapped = set_gameobject_name_and_active(remapped, new_name, 0)
                emitted.append(remapped.rstrip("\n") + "\n")
            append_blocks.append("".join(emitted))

            root_rt_old = root_rect_id_for_go(blocks, template_go)
            if not root_rt_old:
                logger.error("No root RectTransform for template GO %s (%s)", template_go, new_name)
                return 1
            new_facade_child_rects.append(id_map[root_rt_old])

    # Refresh blocks map including appended (for portrait template lookup still uses original blocks)
    portrait_go = PORTRAIT_TEMPLATE_GO
    for slot_idx in range(3, 7):
        old_ids = collect_closure_from_go(blocks, portrait_go)
        sorted_old = sorted(old_ids, key=int)
        id_map = {}
        cur = next_id
        for oid in sorted_old:
            id_map[oid] = str(cur)
            cur += 1
        next_id = cur
        emitted = []
        for oid in sorted_old:
            raw = blocks[oid]
            remapped = remap_block(raw, id_map)
            if oid == portrait_go:
                remapped = set_gameobject_name_and_active(remapped, f"PortraitSlot{slot_idx}", 1)
            emitted.append(remapped.rstrip("\n") + "\n")
        append_blocks.append("".join(emitted))
        pr = id_map[root_rect_id_for_go(blocks, portrait_go) or ""]
        new_portrait_rects.append(pr)
        img_id = id_map[image_mono_id_for_portrait_go(blocks, portrait_go) or ""]
        portrait_image_ids.append(img_id)

    # Original three portrait Image component ids (from scene)
    p0 = image_mono_id_for_portrait_go(blocks, portrait_go)
    p1_go = find_go_id_by_name(names, "PortraitSlot1")
    p2_go = find_go_id_by_name(names, "PortraitSlot2")
    if not p0 or not p1_go or not p2_go:
        logger.error("Portrait slots 0..2 not found")
        return 1
    p1 = image_mono_id_for_portrait_go(blocks, p1_go)
    p2 = image_mono_id_for_portrait_go(blocks, p2_go)
    if not p1 or not p2:
        logger.error("Portrait Image components missing")
        return 1
    all_portrait_images = [p0, p1, p2] + portrait_image_ids

    # Patch existing blocks in text (replace by fileID anchor)
    def replace_block(full: str, fid: str, new_body: str) -> str:
        blk = blocks[fid]
        return full.replace(blk, new_body, 1)

    fs_blk = blocks[FULLSCREEN_ROOT_RT]
    fs_new = append_children_list(fs_blk, new_facade_child_rects)
    text = replace_block(text, FULLSCREEN_ROOT_RT, fs_new)

    pr_blk = blocks[PORTRAIT_ROW_RT]
    pr_new = append_children_list(pr_blk, new_portrait_rects)
    text = replace_block(text, PORTRAIT_ROW_RT, pr_new)

    ctrl_blk = blocks[CONTROLLER_MB]
    ctrl_new = replace_portrait_slots_in_controller(ctrl_blk, all_portrait_images)
    ctrl_new = inject_people_per_window(ctrl_new)
    text = replace_block(text, CONTROLLER_MB, ctrl_new)

    if append_blocks:
        if not text.endswith("\n"):
            text += "\n"
        text += "".join(append_blocks)

    scene_path.write_text(text, encoding="utf-8", newline="\n")
    logger.info(
        "Done: added %d facade roots, %d portrait slots, portraitSlots=%s",
        len(new_facade_child_rects),
        len(new_portrait_rects),
        len(all_portrait_images),
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
