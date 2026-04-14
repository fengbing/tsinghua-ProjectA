# -*- coding: utf-8 -*-
"""One-off: remove Panel under Wx_Intro/Wx_Details in Level 2, keep ActionButton only."""
from __future__ import annotations

import re
import sys
from pathlib import Path


def split_scene_blocks(text: str) -> tuple[str, list[tuple[int, int, str]]]:
    """Return (preamble, [(file_id, type_id, block_text), ...]) in file order."""
    pat = re.compile(r"^--- !u!(\d+) &(\d+)\r?$", re.MULTILINE)
    matches = list(pat.finditer(text))
    if not matches:
        raise RuntimeError("no Unity object blocks found")
    preamble = text[: matches[0].start()]
    blocks: list[tuple[int, int, str]] = []
    for i, m in enumerate(matches):
        start = m.start()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        type_id = int(m.group(1))
        fid = int(m.group(2))
        blocks.append((fid, type_id, text[start:end]))
    return preamble, blocks


def build_index(blocks: list[tuple[int, int, str]]) -> dict[int, tuple[int, str]]:
    return {fid: (typ, body) for fid, typ, body in blocks}


def parse_rect_children(body: str) -> list[int]:
    ids: list[int] = []
    in_children = False
    for line in body.splitlines():
        if line.startswith("  m_Children:"):
            in_children = True
            continue
        if in_children:
            s = line.strip()
            if s.startswith("- {fileID:"):
                mm = re.search(r"fileID: (-?\d+)", s)
                if mm and int(mm.group(1)) > 0:
                    ids.append(int(mm.group(1)))
            elif s and not s.startswith("-"):
                break
    return ids


def parse_rect_gameobject(body: str) -> int | None:
    mm = re.search(r"^  m_GameObject: \{fileID: (\d+)\}", body, re.MULTILINE)
    return int(mm.group(1)) if mm else None


def parse_gameobject_components(body: str) -> list[int]:
    ids: list[int] = []
    in_list = False
    for line in body.splitlines():
        if line.startswith("  m_Component:"):
            in_list = True
            continue
        if in_list:
            s = line.strip()
            if s.startswith("- component:"):
                mm = re.search(r"fileID: (\d+)", s)
                if mm:
                    ids.append(int(mm.group(1)))
            elif s and not s.startswith("-"):
                break
    return ids


def collect_rect_subtree(index: dict[int, tuple[int, str]], root_rect: int, out: set[int]) -> None:
    if root_rect in out or root_rect not in index:
        return
    typ, body = index[root_rect]
    if typ != 224:
        return
    out.add(root_rect)
    for ch in parse_rect_children(body):
        collect_rect_subtree(index, ch, out)
    gid = parse_rect_gameobject(body)
    if gid is None or gid not in index:
        return
    out.add(gid)
    _, gbody = index[gid]
    for cid in parse_gameobject_components(gbody):
        out.add(cid)


def main() -> int:
    scene = Path(__file__).resolve().parents[1] / "Assets" / "Scenes" / "Level 2.unity"
    text = scene.read_text(encoding="utf-8")
    preamble, blocks = split_scene_blocks(text)
    index = build_index(blocks)

    # (section_rect, panel_rect, keep_button_rect, extra_child_rects to delete subtree)
    edits = [
        (136209454, 1517071740, 1823094714, [1510791282, 2009887343]),
        (1577610331, 207888704, 320950736, [731258216, 1150792389]),
        (2102490925, 1026641258, 393194439, [1154783031, 651813768]),
        (152828774, 1741727718, 614187952, [1395414386, 573906848]),
        (329734913, 261141820, 567621855, [759154907, 306471052]),
        (1399126874, 24630904, 74713315, [1472146, 661944482]),
    ]

    panel_go_and_parts: list[tuple[int, int, int, int]] = [
        (1517071739, 1517071740, 1517071741, 1517071742),
        (207888703, 207888704, 207888705, 207888706),
        (1026641257, 1026641258, 1026641259, 1026641260),
        (1741727717, 1741727718, 1741727719, 1741727720),
        (261141819, 261141820, 261141821, 261141822),
        (24630903, 24630904, 24630905, 24630906),
    ]

    remove_ids: set[int] = set()
    for go, rt, img, cr in panel_go_and_parts:
        remove_ids.update([go, rt, img, cr])

    for _sec, _panel, _btn, extras in edits:
        for er in extras:
            collect_rect_subtree(index, er, remove_ids)

    # Rewire: section lists only button; button's father = section
    new_blocks: list[tuple[int, int, str]] = []
    for fid, typ, body in blocks:
        if fid in remove_ids:
            continue
        if fid in {e[0] for e in edits}:
            btn = next(br for sr, pr, br, _ in edits if sr == fid)
            body = re.sub(
                r"(^  m_Children:\s*\n)\s*- \{fileID: \d+\}\s*\n",
                rf"\1  - {{fileID: {btn}}}\n",
                body,
                count=1,
                flags=re.MULTILINE,
            )
        for sec_rect, _p, btn_rect, _ in edits:
            if fid == btn_rect:
                body = re.sub(
                    r"^  m_Father: \{fileID: \d+\}$",
                    f"  m_Father: {{fileID: {sec_rect}}}",
                    body,
                    count=1,
                    flags=re.MULTILINE,
                )
        new_blocks.append((fid, typ, body))

    out_text = preamble + "".join(b for _f, _t, b in new_blocks)
    scene.write_text(out_text, encoding="utf-8", newline="\n")
    print(f"Wrote {scene}; removed {len(remove_ids)} object ids from scene.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
