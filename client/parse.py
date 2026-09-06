"""Turn MajorMUD text into events."""

from __future__ import annotations

import re
from typing import Literal

from . import paths

EventKind = Literal[
    "prompt",
    "exits",
    "also_here",
    "you_see",
    "killed",
    "experience",
    "rest",
    "shop",
    "bought",
    "room",
    "dark",
    "torch_lit",
    "torch_out",
    "cannot",
    "combat",
    "combat_off",
    "arrive",
    "leave",
    "drop",
    "inventory",
    "said",
    "heal_ask",
    "hits",
    "mana",
    "cast_fail",
    "buff",
    "invited",
    "following",
    "followed",
    "backrank",
    "party_fail",
    "sneak_try",
    "sneak_ok",
    "sneak_fail",
    "mortal",
    "aided",
    "drag_fail",
    "dragging",
    "afraid",
    "flee",
    "stand",
    "wounded",
    "left",
    "trained",
    "level",
    "flood",
    "shop_vague",
    "already_worn",
    "sold",
    "stats",
    "death",
]

PROMPT_RE = re.compile(
    r"\[HP=(?P<hp>-?\d+)"
    r"(?:/(?P<max>\d+))?"
    r"(?:/MA=(?P<ma>\d+)(?:/(?P<max_ma>\d+))?)?"
    r"(?:[^\]]*)\]:",
    re.IGNORECASE,
)
EXITS_RE = re.compile(r"Obvious exits:\s*(.+)", re.IGNORECASE)
ALSO_RE = re.compile(r"Also here:\s*(.+)", re.IGNORECASE)
SEE_RE = re.compile(r"You see:\s*(.+)", re.IGNORECASE)
NOTICE_RE = re.compile(r"^You notice\s+(.+?)\s+here\.?$", re.IGNORECASE)
KILLED_RE = re.compile(r"You have killed (.+?)!", re.IGNORECASE)
DEAD_RE = re.compile(r"^(.+?) is dead\.$", re.IGNORECASE)
DIE_RE = re.compile(
    r"^(.+?) (?:falls(?: down)? dead|drops dead|falls to the ground|"
    r"dissolves into|collapses to the ground|"
    r"collapses,|crumbles into|bursts into|vanishes in)",
    re.IGNORECASE,
)
DROP_RE = re.compile(
    r"(\d+\s+)?(copper|silver|gold|platinum)s?\s+drop to the ground",
    re.IGNORECASE,
)
EXP_RE = re.compile(r"You (?:gain|receive) (\d+) experience", re.IGNORECASE)
EXP_STAT_RE = re.compile(
    r"Exp:\s*(?P<exp>\d+)\s+Level:\s*(?P<level>\d+)"
    r".*?Exp needed for next level:\s*(?P<needed>\d+)\s*"
    r"\((?P<next>\d+)\)\s*\[(?P<pct>\d+)%\]",
    re.IGNORECASE,
)
YOU_HIT_RE = re.compile(
    r"^You (?:critically )?\w+ .+ for (?P<dmg>\d+) damage",
    re.IGNORECASE,
)
SELF_SWING_RE = re.compile(
    r"^([A-Z][A-Za-z]+) moves to attack (.+?)[.!]?$",
    re.IGNORECASE,
)
THIRD_HIT_RE = re.compile(
    r"^([A-Z][a-z]{1,14}) (?:critically )?(?:whaps|hits|slashes|pierces|bashes) "
    r".+ for \d+ damage",
    re.IGNORECASE,
)
THIRD_SIT_RE = re.compile(
    r"^([A-Z][a-z]{1,14}) sits down and (?:meditates|begins to rest)",
    re.IGNORECASE,
)
THIRD_STAND_RE = re.compile(
    r"^([A-Z][a-z]{1,14}) stands up\b",
    re.IGNORECASE,
)
ALLY_FLEE_RE = re.compile(
    r"^([A-Z][A-Za-z]{1,14}) (?:panics and )?(?:flees|runs)(?: to the)? "
    r"(north|south|east|west|up|down|northeast|northwest|southeast|southwest)\b",
    re.IGNORECASE,
)
YOU_FLEE_RE = re.compile(
    r"^You (?:panic and )?flee(?: to the)? "
    r"(north|south|east|west|up|down|northeast|northwest|southeast|southwest)\b",
    re.IGNORECASE,
)
WOUNDED_LOOK_RE = re.compile(
    r"^([A-Z][A-Za-z]{1,14}) gasps for breath(?:, looking severely wounded)?",
    re.IGNORECASE,
)
DIR_RE = re.compile(r"\b(north|south|east|west|up|down|northeast|northwest|southeast|southwest)\b", re.I)
DIR_SHORT = {
    "north": "n",
    "south": "s",
    "east": "e",
    "west": "w",
    "up": "u",
    "down": "d",
    "northeast": "ne",
    "northwest": "nw",
    "southeast": "se",
    "southwest": "sw",
}
COMBAT_RE = re.compile(
    r"You (?:swing at|swipe at|hit|miss|attack|slash|pierce|bash|whap) |"
    r"moves to attack|"
    r"swings at|"
    r"lunges at|"
    r"dodges your attack|"
    r"glances off|"
    r"but misses|"
    r"hits you|"
    r"strikes you|"
    r"just attacked|"
    r"flails at|"
    r"whips you|"
    r"burns you|"
    r"claws at|"
    r"swipes at|"
    r"scuttles |"
    r"for \d+ damage",
    re.IGNORECASE,
)
ARRIVE_RE = re.compile(
    r"^(?:A|An|The) (.+?) (?:just arrived|"
    r"(?:walks|creeps|crawls|comes|steps|wanders|runs|appears|sneaks|"
    r"scuttles|scurries|oozes|slithers)"
    r"(?: +(?:into|in|from)\b.*)?)(?:\.|$)",
    re.IGNORECASE,
)
LEAVE_RE = re.compile(
    r"^(?:A|An|The) (.+?) walks out",
    re.IGNORECASE,
)
PC_ARRIVE_RE = re.compile(
    r"^([A-Z][A-Za-z]{1,14}) (?:just arrived|walks into the room|walks in|"
    r"has just arrived|just entered the Realm)\b",
    re.IGNORECASE,
)
PC_LEAVE_RE = re.compile(
    r"^([A-Z][A-Za-z]{1,14}) (?:just left|walks out|has just left)\b",
    re.IGNORECASE,
)
_PC_MOVE_SKIP = frozenset(
    {"you", "he", "she", "it", "someone", "the", "a", "an", "this", "that"}
)
HIT_YOU_RE = re.compile(
    r"^(.+?) (?:swings at|swipes at|hits|whaps|slashes|pierces|bashes|"
    r"attacks|moves to attack|"
    r"lunges at|leaps at|snaps at|lashes at|flails at|"
    r"darts (?:forward )?and bites|"
    r"(?:whips|bites|claws|kicks)(?: at)?) (?P<victim>you|[A-Z][A-Za-z]+)\b"
    r"(?:.*? for (?P<dmg>\d+) damage)?",
    re.IGNORECASE,
)
SAY_RE = re.compile(
    r"^(?P<who>You|[A-Z][A-Za-z]{1,19}) says?,?\s+[\"']?(?P<msg>.+?)[\"']?\s*$",
    re.IGNORECASE,
)
_HEAL_ASK = frozenset({"heal", "heals", "healing", "mihe"})
_HEAL_ASK_SKIP = frozenset({"health", "hea"})
SALE_RE = re.compile(r"for sale|shopkeeper|what would you like to buy", re.IGNORECASE)
SOLD_RE = re.compile(r"^you sold (?P<item>.+?) for ", re.IGNORECASE)
ALREADY_WORN_RE = re.compile(
    r"you do not have (?P<item>.+?) left unequipped",
    re.IGNORECASE,
)
_INV_END_RE = re.compile(
    r"^(?:you have no keys|you have .+ keys?|wealth:|encumbrance:|\[hp=)",
    re.IGNORECASE,
)
INVITE_YOU_RE = re.compile(
    r"^(.+?) has invited you to follow",
    re.IGNORECASE,
)
YOU_INVITE_RE = re.compile(
    r"^You have invited (.+?) to follow",
    re.IGNORECASE,
)
NOW_FOLLOW_RE = re.compile(
    r"^You are(?: now)? following (.+?)\.?$",
    re.IGNORECASE,
)
THEY_FOLLOW_RE = re.compile(
    r"^(.+?) started to follow you",
    re.IGNORECASE,
)
PARTY_LEAD_RE = re.compile(
    r"Following your Party leader (.+?)(?:\s|--|$)",
    re.IGNORECASE,
)
BACKRANK_YOU_RE = re.compile(
    r"You have moved to the back ranks",
    re.IGNORECASE,
)
HITS_RE = re.compile(
    r"(?:health|hits?|hit ?points?):\s*(-?\d+)\s*/\s*(\d+)",
    re.IGNORECASE,
)
HITS_OF_RE = re.compile(r"(-?\d+)\s+of\s+(\d+)\s+hit points", re.IGNORECASE)
MANA_RE = re.compile(r"mana:\s*(\d+)\s*/\s*(\d+)", re.IGNORECASE)
LEVEL_STAT_RE = re.compile(r"\bLevel:\s*(\d+)\b", re.IGNORECASE)
STAT_FIELD_RE = re.compile(
    r"\b(Strength|Intellect|Willpower|Agility|Charm|Health|"
    r"Attack|Accuracy|Defense|Armour Class|AC)\s*:\s*(-?\d+)",
    re.IGNORECASE,
)
_STAT_KEYS = {
    "strength": "strength",
    "intellect": "intellect",
    "willpower": "willpower",
    "agility": "agility",
    "charm": "charm",
    "health": "health_stat",
    "attack": "attack",
    "accuracy": "accuracy",
    "defense": "ac",
    "armour class": "ac",
    "ac": "ac",
}
TRAINED_RE = re.compile(
    r"(?:you (?:have )?(?:gained a level|gain a level|just trained|trained to level)"
    r"|you are now level \d+"
    r"|your training is complete"
    r"|you train for a while)",
    re.IGNORECASE,
)
MORTAL_YOU_RE = re.compile(
    r"^You (?:are|have been|fall|drop).{0,48}mortally wounded",
    re.IGNORECASE,
)
MORTAL_THEY_RE = re.compile(
    r"^(.+?) (?:is|has been|falls|drops).{0,48}mortally wounded",
    re.IGNORECASE,
)
BLEED_YOU_RE = re.compile(r"^You (?:are bleeding|bleed)\b", re.IGNORECASE)
BLEED_THEY_RE = re.compile(r"^(.+?) is bleeding\b", re.IGNORECASE)
AID_YOU_RE = re.compile(r"^You have aided (.+?)(?:,|\.|$)", re.IGNORECASE)
AIDED_YOU_RE = re.compile(r"^(.+?) has aided you\b", re.IGNORECASE)
WOUNDS_HEAL_RE = re.compile(
    r"(?:(\w+)'s )?wounds are now healing",
    re.IGNORECASE,
)
DRAG_FAIL_RE = re.compile(
    r"may not drag|cannot drag|can't drag|can not drag",
    re.IGNORECASE,
)
DRAGGING_RE = re.compile(
    r"^You (?:are now dragging|start dragging)\s+(.+?)(?:\.|$)",
    re.IGNORECASE,
)
DRAG_STOP_RE = re.compile(
    r"no longer dragging|stop dragging|you are not dragging",
    re.IGNORECASE,
)
AFRAID_RE = re.compile(r"too afraid", re.IGNORECASE)
LEFT_PARTY_RE = re.compile(
    r"^You (?:are no longer following|leave (?:the )?party|have left(?: the party)?)\b",
    re.IGNORECASE,
)


def _pc_mover(name: str) -> str:
    """A walking PC, not You / a lop that slipped past the article regex."""
    raw = name.strip()
    if not raw or raw.lower() in _PC_MOVE_SKIP:
        return ""
    if paths.lop_in([raw]):
        return ""
    if paths.is_given_name(raw) or paths.is_player(raw) or paths.is_home_account(raw):
        return raw
    return ""


def _split_list(blob: str) -> list[str]:
    cleaned = blob.strip().rstrip(".")
    if not cleaned:
        return []
    parts = re.split(r",|\band\b", cleaned)
    names: list[str] = []
    for part in parts:
        piece = part.strip()
        if not piece:
            continue
        names.extend(paths.peel_presence(piece))
    return names


def _exit_dirs(blob: str) -> list[str]:
    found: list[str] = []
    for part in re.split(r",|\band\b", blob):
        piece = part.strip().lower()
        if not piece or "closed" in piece:
            continue
        for d in DIR_RE.findall(piece):
            short = DIR_SHORT.get(d.lower(), d.lower()[:2])
            if short not in found:
                found.append(short)
    return found


def _closed_exit_dirs(blob: str) -> list[str]:
    found: list[str] = []
    for part in re.split(r",|\band\b", blob):
        piece = part.strip().lower()
        if "closed" not in piece:
            continue
        for d in DIR_RE.findall(piece):
            short = DIR_SHORT.get(d.lower(), d.lower()[:2])
            if short not in found:
                found.append(short)
    return found


def _pc_status_name(name: str) -> str:
    raw = name.strip().rstrip(".,!;:")
    if not raw:
        return ""
    first = raw.split(",", 1)[0].strip()
    words = first.split()
    if not words:
        return ""
    head = words[0]
    if head.lower() in _PC_MOVE_SKIP:
        return ""
    if paths.lop_in([first]):
        return ""
    if paths.is_given_name(head) or paths.is_player(head) or paths.is_home_account(head):
        return head
    return ""


def _mortal_event(raw: str) -> dict[str, object] | None:
    if MORTAL_YOU_RE.search(raw) or BLEED_YOU_RE.search(raw):
        return {"kind": "mortal", "name": "you"}
    m = MORTAL_THEY_RE.search(raw) or BLEED_THEY_RE.search(raw)
    if not m:
        return None
    who = _pc_status_name(m.group(1))
    if not who:
        return None
    return {"kind": "mortal", "name": who}


def _aided_event(raw: str) -> dict[str, object] | None:
    m = AID_YOU_RE.search(raw)
    if m:
        who = _pc_status_name(m.group(1))
        if who:
            return {"kind": "aided", "name": who}
    m = AIDED_YOU_RE.search(raw)
    if m:
        return {"kind": "aided", "name": "you"}
    m = WOUNDS_HEAL_RE.search(raw)
    if not m:
        return None
    who = _pc_status_name(m.group(1) or "you")
    if not who:
        return {"kind": "aided", "name": "you"}
    return {"kind": "aided", "name": who}


_SAID_SWING_RE = re.compile(
    r"^(?:attack|ttack|tack|kill|bash|att|aa|at|bs|a|k)\s+(.+)$",
    re.IGNORECASE,
)


def _said_aim(msg: str) -> str:
    """Target from a spoken swing — `a carrion beast` / `attack tack rat`."""
    raw = msg.strip().strip("\"'")
    matched = _SAID_SWING_RE.match(raw)
    if not matched:
        return ""
    name = matched.group(1).strip()
    return paths.attack_name(name) or name


def _is_heal_ask(msg: str) -> bool:
    """Spoken `heal me` only (and old `heal` / `say heal`). Never `please` / `health`."""
    word = msg.lower().strip().strip("\"'.,!;:")
    if not word:
        return False
    if word.startswith("say "):
        word = word[4:].strip()
    first = word.split()[0] if word else ""
    if first in _HEAL_ASK_SKIP or word.startswith("health"):
        return False
    if word == "heal me":
        return True
    return word in _HEAL_ASK


def _prompt_event(m: re.Match[str]) -> dict[str, object]:
    ev: dict[str, object] = {
        "kind": "prompt",
        "hp": int(m.group("hp")),
        "max_hp": int(m.group("max")) if m.group("max") else None,
    }
    if m.group("ma"):
        ev["ma"] = int(m.group("ma"))
    if m.group("max_ma"):
        ev["max_ma"] = int(m.group("max_ma"))
    return ev


def parse_line(line: str) -> dict[str, object] | None:
    raw = line.strip()
    if not raw:
        return None
    low0 = raw.lower()
    m = EXITS_RE.search(raw)
    if m:
        blob = m.group(1)
        dirs = _exit_dirs(blob)
        shut = _closed_exit_dirs(blob)
        if dirs or shut:
            ev: dict[str, object] = {"kind": "exits", "exits": dirs}
            if shut:
                ev["closed"] = shut
            return ev

    if (
        low0.startswith("mud internal error")
        or low0.startswith("monbadroom")
        or low0.startswith("please tell your sysop")
    ):
        return None

    m = PROMPT_RE.search(raw)
    if m:
        return _prompt_event(m)

    m = ALSO_RE.search(raw)
    if m:
        blob = m.group(1)
        cut = re.split(
            r"MUD Internal Error|monbadroom|Please tell your sysop",
            blob,
            maxsplit=1,
            flags=re.I,
        )[0]
        return {"kind": "also_here", "mobs": _split_list(cut)}

    m = SEE_RE.search(raw)
    if m:
        return {"kind": "you_see", "things": _split_list(m.group(1))}
    m = NOTICE_RE.search(raw)
    if m:
        return {"kind": "you_see", "things": _split_list(m.group(1))}

    if "you have been killed" in raw.lower():
        return {"kind": "death"}
    mortal = _mortal_event(raw)
    if mortal:
        return mortal
    m = KILLED_RE.search(raw)
    if m:
        return {"kind": "killed", "name": m.group(1).strip()}
    m = DEAD_RE.search(raw)
    if m:
        name = m.group(1).strip()
        if name.lower() not in ("he", "she", "it", "someone"):
            return {"kind": "killed", "name": name}
    m = DIE_RE.search(raw)
    if m:
        name = m.group(1).strip()
        if name.lower() not in ("you", "he", "she", "it", "someone", "this", "that"):
            return {"kind": "killed", "name": name}
    m = DROP_RE.search(raw)
    if m:
        return {"kind": "drop", "name": m.group(2).lower()}

    m = EXP_RE.search(raw)
    if m:
        return {"kind": "experience", "amount": int(m.group(1))}
    m = EXP_STAT_RE.search(raw)
    if m:
        return {
            "kind": "level",
            "level": int(m.group("level")),
            "exp": int(m.group("exp")),
            "needed": int(m.group("needed")),
            "next": int(m.group("next")),
            "pct": int(m.group("pct")),
        }
    low = raw.lower()
    m = INVITE_YOU_RE.search(raw)
    if m:
        return {"kind": "invited", "name": m.group(1).strip()}
    m = YOU_INVITE_RE.search(raw)
    if m:
        return {"kind": "invited", "name": m.group(1).strip(), "by_me": True}
    m = NOW_FOLLOW_RE.search(raw)
    if m:
        return {"kind": "following", "name": m.group(1).strip()}
    m = THEY_FOLLOW_RE.search(raw)
    if m:
        return {"kind": "followed", "name": m.group(1).strip()}
    m = PARTY_LEAD_RE.search(raw)
    if m:
        return {"kind": "following", "name": m.group(1).strip()}
    if BACKRANK_YOU_RE.search(raw):
        return {"kind": "backrank"}
    if "you don't think you're sneaking" in low:
        return {"kind": "sneak_fail"}
    if "you may not sneak" in low:
        return {"kind": "sneak_fail", "reason": "busy"}
    if "you make a sound when entering" in low or "you make a sound as you enter" in low:
        return {"kind": "sneak_fail"}
    if "not hidden" in low or "aren't sneaking" in low or "are not sneaking" in low:
        return {"kind": "sneak_fail"}
    if "no longer hidden" in low or "no longer sneaking" in low:
        return {"kind": "sneak_fail"}
    if re.match(r"^sneaking\.+", low):
        return {"kind": "sneak_ok"}
    if "attempting to sneak" in low:
        return {"kind": "sneak_try"}
    if "must be invited first" in low:
        return {"kind": "party_fail", "reason": "invite"}
    if "not in a party" in low:
        return {"kind": "party_fail", "reason": "party"}
    if LEFT_PARTY_RE.search(raw):
        return {"kind": "left"}
    if DRAG_FAIL_RE.search(raw):
        return {"kind": "drag_fail", "text": raw}
    if AFRAID_RE.search(raw):
        return {"kind": "afraid"}
    if DRAG_STOP_RE.search(raw):
        return {"kind": "dragging", "name": ""}
    m = DRAGGING_RE.search(raw)
    if m:
        return {"kind": "dragging", "name": m.group(1).strip()}
    aided = _aided_event(raw)
    if aided:
        return aided
    said_line = SAY_RE.match(raw)
    if said_line:
        who = said_line.group("who").strip()
        msg = said_line.group("msg").strip().strip("\"'")
        if _is_heal_ask(msg):
            return {"kind": "heal_ask", "name": who}
        if who.lower() == "you":
            ev: dict[str, object] = {"kind": "said", "text": raw}
            aimed = _said_aim(msg)
            if aimed:
                ev["aimed"] = aimed
            return ev
        return None
    m = ARRIVE_RE.search(raw)
    if m:
        return {"kind": "arrive", "name": m.group(1).strip()}
    m = LEAVE_RE.search(raw)
    if m:
        return {"kind": "leave", "name": m.group(1).strip()}
    m = PC_ARRIVE_RE.search(raw)
    if m:
        who = _pc_mover(m.group(1))
        if who:
            return {"kind": "arrive", "name": who}
    m = PC_LEAVE_RE.search(raw)
    if m:
        who = _pc_mover(m.group(1))
        if who:
            return {"kind": "leave", "name": who}
    if low.startswith("*combat") or re.match(r"^\*?combat\s+off", low):
        if "off" in low:
            return {"kind": "combat_off"}
        return {"kind": "combat"}
    hit = YOU_HIT_RE.search(raw)
    if hit:
        ev: dict[str, object] = {"kind": "combat"}
        dmg = hit.group("dmg")
        if dmg:
            ev["dealt"] = int(dmg)
        return ev
    m = THIRD_HIT_RE.search(raw)
    if m:
        return {"kind": "combat", "actor": m.group(1).strip()}
    m = THIRD_SIT_RE.search(raw)
    if m:
        return {"kind": "rest", "actor": m.group(1).strip()}
    m = THIRD_STAND_RE.search(raw)
    if m:
        return {"kind": "stand", "actor": m.group(1).strip()}
    if re.match(r"^you stand(?:s)? up\b", low):
        return {"kind": "stand"}
    m = ALLY_FLEE_RE.search(raw)
    if m:
        who = m.group(1).strip()
        step = DIR_SHORT.get(m.group(2).strip().lower(), "")
        ev: dict[str, object] = {"kind": "flee", "name": who}
        if step:
            ev["dir"] = step
        return ev
    m = YOU_FLEE_RE.search(raw)
    if m:
        step = DIR_SHORT.get(m.group(1).strip().lower(), "")
        ev = {"kind": "flee"}
        if step:
            ev["dir"] = step
        return ev
    m = WOUNDED_LOOK_RE.search(raw)
    if m:
        return {"kind": "wounded", "name": m.group(1).strip()}
    m = SELF_SWING_RE.search(raw)
    if m:
        # Party echo of a swing. Actor is enough (invite / last_actor).
        # The target is not a room listing — do not re-add that name.
        return {"kind": "combat", "actor": m.group(1).strip()}
    m = HIT_YOU_RE.search(raw)
    if m and not raw.lower().startswith("you "):
        attacker = m.group(1).strip()
        if paths.is_home_account(attacker):
            return {"kind": "combat"}
        ev: dict[str, object] = {"kind": "combat", "name": attacker}
        victim = (m.group("victim") or "").strip()
        dmg = m.group("dmg")
        if victim and victim.lower() != "you":
            if (
                paths.is_given_name(victim)
                or paths.is_player(victim)
                or paths.is_home_account(victim)
            ):
                ev["victim"] = victim
                if dmg and int(dmg) > 0:
                    ev["damage"] = int(dmg)
        return ev
    if COMBAT_RE.search(raw):
        return {"kind": "combat"}
    if _is_inventory_line(raw):
        entries = paths.inventory_entries(raw)
        return {
            "kind": "inventory",
            "text": raw,
            "items": [name for name, _worn in entries],
            "extras": [name for name, worn in entries if not worn],
            "worn": [name for name, worn in entries if worn],
        }
    worn = ALREADY_WORN_RE.search(raw)
    if worn or "already worn" in low or "already wearing" in low:
        item = worn.group("item").strip().lower() if worn else ""
        return {"kind": "already_worn", "item": item}

    if "you feel lucky" in low:
        return {"kind": "buff", "name": "bless", "on": True}
    if "you cast bless on" in low:
        return {"kind": "buff", "name": "bless", "on": True}
    if "effects of bless wear off" in low:
        return {"kind": "buff", "name": "bless", "on": False}
    if "enough mana" in low:
        return {"kind": "cast_fail", "reason": "mana"}
    if (
        "don't know that spell" in low
        or "do not know that spell" in low
        or ("have not learned" in low and "spell" in low)
    ):
        return {"kind": "cast_fail", "reason": "unknown"}
    if "already know" in low and "spell" in low:
        return {"kind": "learned", "already": True}
    if (
        "have learned" in low
        or "you memorize" in low
        or ("memorize" in low and "spell" in low)
        or ("now know" in low and "spell" in low)
    ):
        return {"kind": "learned"}
    if (
        "cannot cast" in low
        or "can't cast" in low
        or "can not cast" in low
        or ("not high enough" in low and "learn" not in low)
        or (
            "too low" in low
            and any(word in low for word in ("level", "cast", "spell"))
        )
        or (
            "not yet" in low
            and any(word in low for word in ("cast", "spell", "bless", "level"))
        )
        or ("fail" in low and "bless" in low)
    ):
        return {"kind": "cast_fail", "reason": "level"}
    if (
        "not high enough" in low
        or "cannot learn" in low
        or "can't learn" in low
        or "can not learn" in low
        or "can't afford" in low
        or "cannot afford" in low
        or "can not afford" in low
        or (
            ("you can't" in low or "you cannot" in low)
            and any(word in low for word in ("scroll", "afford", "learn", "buy"))
        )
    ):
        return {"kind": "spell_skip"}
    if "you rest" in low or "you sit down" in low or "you are now resting" in low or "feeling refreshed" in low:
        return {"kind": "rest"}
    if "typing too quickly" in low or "slow down for a few seconds" in low:
        return {"kind": "flood"}
    if "more specific" in low:
        return {"kind": "shop_vague"}
    sold = SOLD_RE.search(raw)
    if sold:
        return {"kind": "sold", "item": sold.group("item").strip().lower()}
    if SALE_RE.search(raw) or "the shop sells" in low:
        return {"kind": "shop"}
    if low.startswith("you buy") or "you just bought" in low or "sold to you" in low:
        return {"kind": "bought"}
    if "too dark" in low or "it is dark" in low or "pitch black" in low:
        return {"kind": "dark"}
    if re.search(r"\byou light (?:a |an |the )?torch\b", low):
        return {"kind": "torch_lit"}
    if "already have something lit" in low or "already have a light" in low:
        return {"kind": "torch_lit"}
    if re.search(
        r"\b(?:your |the )?torch (?:goes|burns) out\b"
        r"|\byour light (?:goes|burns) out\b"
        r"|\bthe torch burns out\b",
        low,
    ):
        return {"kind": "torch_out"}
    if (
        (
            "arena" in low
            and any(
                mark in low
                for mark in (
                    "too high",
                    "too experienced",
                    "may not",
                    "cannot",
                    "can't",
                    "not permitted",
                    "no longer",
                )
            )
        )
        or ("too experienced" in low and "enter" in low)
        or (
            "too high" in low
            and "level" in low
            and "cast" not in low
            and "spell" not in low
            and "learn" not in low
        )
    ):
        return {"kind": "cannot", "text": raw, "arena": True}
    if (
        "you can't" in low
        or "you cannot" in low
        or "you may not go" in low
        or "you may not enter" in low
        or "there is no" in low
        or "no exit" in low
        or "there is a closed door" in low
        or "door is closed" in low
        or "gate is closed" in low
    ):
        return {"kind": "cannot", "text": raw}

    hits_m = HITS_RE.search(raw) or HITS_OF_RE.search(raw)
    mana_m = MANA_RE.search(raw)
    if hits_m:
        hits_ev: dict[str, object] = {
            "kind": "hits",
            "hp": int(hits_m.group(1)),
            "max_hp": int(hits_m.group(2)),
        }
        if mana_m:
            hits_ev["ma"] = int(mana_m.group(1))
            hits_ev["max_ma"] = int(mana_m.group(2))
        return hits_ev
    if mana_m:
        return {"kind": "mana", "ma": int(mana_m.group(1)), "max_ma": int(mana_m.group(2))}

    if TRAINED_RE.search(raw):
        trained: dict[str, object] = {"kind": "trained"}
        lvl_m = re.search(r"level\s+(\d+)", raw, re.IGNORECASE)
        if lvl_m:
            trained["level"] = int(lvl_m.group(1))
        return trained
    lvl_m = LEVEL_STAT_RE.search(raw)
    if lvl_m:
        return {"kind": "level", "level": int(lvl_m.group(1))}

    fields = STAT_FIELD_RE.findall(raw)
    if fields:
        stats: dict[str, object] = {"kind": "stats"}
        for name, value in fields:
            key = _STAT_KEYS.get(name.lower())
            if key:
                stats[key] = int(value)
        if len(stats) > 1:
            return stats

    if _looks_like_room_title(raw):
        return {"kind": "room", "title": raw}

    return None


GLUE_RE = re.compile(
    r"(?=Also here:|You notice |You see:|Obvious exits:|\*Combat|\[HP=)"
    r"|(?=Mana:)|(?=Hits:)|(?=Health:)"
    r"|(?=Attempting to sneak)|(?=You don't think you're sneaking)|(?=Sneaking\.\.)"
    r"|(?=You may not sneak)"
    r"|(?=You make a sound when entering)|(?=You make a sound as you enter)"
    r"|(?=You have aided )|(?=You may not drag)|(?=You are mortally)"
    r"|(?=[A-Z][a-z]{1,14} is mortally wounded)|(?=too afraid)"
    r"|(?=You are bleeding)|(?=[A-Z][a-z]{1,14} is bleeding)"
    r"|(?=You are now dragging)|(?=You are no longer following)"
    r"|(?=You have invited )|(?=You are now following )|(?=You are following )"
    r"|(?=You say )|(?=[A-Z][a-z]{1,14} says?,? )"
    r"|(?=You feel lucky)|(?=You cast bless)|(?=The effects of bless wear off)"
    r"|(?=You swipe at )|(?=The [a-z].{0,48}dissolves into)"
    r"|(?=You have moved to the back ranks)"
    r"|(?<=[^A-Za-z])(?=[A-Z][a-z]{1,14} started to follow you)"
    r"|(?<=[^A-Za-z])(?=[A-Z][a-z]{1,14} has invited you to follow)"
    r"|(?=You have gained a level)|(?=You gain a level)|(?=You are now level )"
    r"|(?=You train for a while)|(?=Your training is complete)"
    r"|(?=Exp:)|(?=You gain )|(?=You receive )"
    r"|(?=\* ?Combat)"
    r"|(?=(?:A|An|The) [A-Za-z].{0,48}(?:creeps |walks |crawls |comes |steps |"
    r"sneaks |scuttles |scurries |oozes |slithers |from nowhere|"
    r"falls(?: down)? dead|drops dead|falls to the ground|"
    r"dissolves into|collapses,|lunges at |snaps at |lashes at |flails at |"
    r"claws at |swipes at |whips |walks out))"
    r"|(?=[A-Z][a-z]{1,14} just arrived)|(?=[A-Z][a-z]{1,14} just left)"
    r"|(?=[A-Z][a-z]{1,14} just entered the Realm)"
    r"|(?=[A-Z][a-z]{1,14} sits down)|(?=[A-Z][a-z]{1,14} stands up)"
    r"|(?=[A-Z][a-z]{1,14} (?:panics and )?flees)|(?=You flee )"
    r"|(?=[A-Z][a-z]{1,14} gasps for breath)"
)
_CSI = re.compile(rb"\x1b\[[0-9;?]*[ -/]*[@-~]|\x1b[@-_]")
_FLUSH_KINDS = frozenset(
    {
        "arrive",
        "leave",
        "killed",
        "death",
        "combat",
        "combat_off",
        "drop",
        "said",
        "heal_ask",
        "also_here",
        "you_see",
        "exits",
        "hits",
        "mana",
        "cast_fail",
        "buff",
        "invited",
        "following",
        "followed",
        "backrank",
        "party_fail",
        "sneak_try",
        "sneak_ok",
        "sneak_fail",
        "dark",
        "torch_lit",
        "torch_out",
        "mortal",
        "aided",
        "drag_fail",
        "dragging",
        "afraid",
        "flee",
        "stand",
        "wounded",
        "rest",
        "left",
        "trained",
        "level",
        "flood",
        "shop_vague",
        "already_worn",
        "sold",
        "learned",
        "spell_skip",
        "stats",
    }
)
_SCREEN_KINDS = frozenset(
    {
        "room",
        "also_here",
        "you_see",
        "exits",
        "drop",
        "killed",
        "death",
        "combat_off",
        "invited",
        "following",
        "followed",
        "backrank",
        "party_fail",
        "sneak_try",
        "sneak_ok",
        "sneak_fail",
        "hits",
        "mana",
        "mortal",
        "aided",
        "drag_fail",
        "dragging",
        "afraid",
        "flee",
        "stand",
        "wounded",
        "rest",
        "left",
        "trained",
        "level",
        "flood",
        "shop_vague",
        "already_worn",
        "inventory",
        "sold",
        "learned",
        "spell_skip",
        "stats",
    }
)


def _is_inv_start(raw: str) -> bool:
    return raw.lower().startswith("you are carrying")


def _inv_block_end(raw: str) -> bool:
    return bool(_INV_END_RE.match(raw.strip()))


def _is_inventory_line(raw: str) -> bool:
    if _is_inv_start(raw) or "(weapon hand)" in raw.lower():
        return True
    return paths.has_inv_slot(raw)


def hold_inventory(held: list[str], lines: list[str]) -> tuple[list[str], list[str]]:
    """Join wrapped `i` rows. Return (emit now, still held across feeds)."""
    out: list[str] = []
    buf = [part.strip() for part in held if part.strip()]
    for line in lines:
        raw = line.strip()
        if buf:
            if _inv_block_end(raw):
                out.append(" ".join(buf))
                buf = []
                out.append(line)
            else:
                buf.append(raw)
            continue
        if _is_inv_start(raw):
            buf.append(raw)
            continue
        out.append(line)
    return out, buf


def stitch_inventory_lines(lines: list[str]) -> list[str]:
    """Join wrapped `You are carrying` / `i` rows into one carrying line."""
    out, leftover = hold_inventory([], lines)
    if leftover:
        out.append(" ".join(leftover))
    return stitch_exit_lines(out)


def _is_exit_continuation(raw: str) -> bool:
    """True when a wrapped line is only compass doors (sysop dump split)."""
    if EXITS_RE.search(raw) or ALSO_RE.search(raw):
        return False
    if not _exit_dirs(raw):
        return False
    leftover = DIR_RE.sub(" ", raw)
    leftover = re.sub(r"\b(and|closed|door|gate)\b", " ", leftover, flags=re.I)
    leftover = re.sub(r"[,.]", " ", leftover)
    return not leftover.strip()


def _exits_need_more(raw: str) -> bool:
    if "obvious exits:" not in raw.lower():
        return False
    m = EXITS_RE.search(raw)
    if not m:
        return True
    blob = m.group(1)
    return not _exit_dirs(blob) and not _closed_exit_dirs(blob)


def stitch_exit_lines(lines: list[str]) -> list[str]:
    """Join `Obvious exits:` + the next row when a sysop dump splits them."""
    out: list[str] = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if (
            _exits_need_more(line)
            and i + 1 < len(lines)
            and _is_exit_continuation(lines[i + 1])
        ):
            out.append(line.rstrip() + " " + lines[i + 1].strip())
            i += 2
            continue
        out.append(line)
        i += 1
    return out


def unglue(text: str) -> str:
    """Pull room/combat lines apart when CSI left them on one buffer."""
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    return GLUE_RE.sub("\n", text)


def parse_events(text: str) -> list[dict[str, object]]:
    events: list[dict[str, object]] = []
    chunks = stitch_inventory_lines(
        [chunk.strip() for chunk in unglue(text).split("\n") if chunk.strip()]
    )
    for piece in chunks:
        if not piece:
            continue
        m = PROMPT_RE.search(piece)
        if m and m.start() > 0:
            prefix = piece[: m.start()].strip()
            if prefix:
                events.extend(parse_events(prefix))
            events.append(_prompt_event(m))
            tail = piece[m.end() :].strip()
            if tail:
                events.extend(parse_events(tail))
            continue
        ev = parse_line(piece)
        if ev:
            events.append(ev)
    return events


def harvest_screen(text: str, seen: set[str]) -> list[dict[str, object]]:
    """Parse newly visible rows, top to bottom. Forget a line once it scrolls off."""
    rows = [raw.strip() for raw in text.splitlines() if raw.strip()]
    on_screen = set(rows)
    for old in list(seen):
        if old not in on_screen:
            seen.discard(old)
    events: list[dict[str, object]] = []
    for line in stitch_inventory_lines(rows):
        if line in seen:
            continue
        fresh = []
        for ev in parse_events(line):
            if ev.get("kind") not in _SCREEN_KINDS:
                continue
            if ev.get("kind") == "combat" and not ev.get("name"):
                continue
            fresh.append(ev)
        if not fresh:
            continue
        seen.add(line)
        events.extend(fresh)
    return events


def strip_csi(data: bytes) -> str:
    return _CSI.sub(b"", data).decode("cp437", "replace")


def flushable(text: str) -> bool:
    """True when a CSI line with no newline is already a full game sentence."""
    raw = text.strip()
    if not raw:
        return False
    if "[HP=" in raw and "]:" in raw:
        return True
    ev = parse_line(raw)
    if ev and ev.get("kind") in ("hits", "mana"):
        return True
    if not raw.endswith((".", "!", "*", "]")):
        return False
    return bool(ev and ev.get("kind") in _FLUSH_KINDS)


def events_from_payload(data: bytes) -> list[dict[str, object]]:
    keep = frozenset(
        {
            "arrive",
            "also_here",
            "combat",
            "killed",
            "combat_off",
            "drop",
            "hits",
            "mana",
            "buff",
            "room",
            "invited",
            "following",
            "followed",
            "backrank",
            "party_fail",
            "mortal",
            "aided",
            "drag_fail",
            "dragging",
            "afraid",
            "flee",
            "stand",
            "wounded",
            "rest",
            "left",
            "trained",
            "level",
            "flood",
            "shop_vague",
            "inventory",
            "already_worn",
            "sold",
            "stats",
            "death",
        }
    )
    return [e for e in parse_events(strip_csi(data)) if e.get("kind") in keep]


_TITLE_SKIP = (
    "welcome",
    "character",
    "validating",
    "please",
    "sorry",
    "make your",
    "also here",
    "mud internal",
    "monbadroom",
    "you see",
    "obvious",
    "encumbrance",
    "wealth",
    "keys",
    "worn",
)
_TITLE_NOISE = (
    "says",
    "attack",
    "swing",
    "miss",
    "dodge",
    "glance",
    "damage",
    "door",
    "gate",
    "hits",
    "dead",
    "combat",
    "deflect",
    "lunge",
    "whap",
    "experience",
    "internal error",
    "sysop",
    "squeak",
    "falls",
    "lunges",
    "mortal",
    "wound",
    "bleed",
    "aided",
    "drag",
    "afraid",
)


def _looks_like_room_title(raw: str) -> bool:
    if len(raw) > 80:
        return False
    street = raw.endswith("St.") or " St. " in raw or " St. &" in raw
    if not street:
        if raw.endswith(":") or raw.endswith(".") or raw.endswith("]") or raw.endswith("!"):
            return False
    if raw.startswith("[") or raw.startswith("You "):
        return False
    if raw.startswith(("A ", "An ")):
        rest = raw.split(" ", 1)[-1]
        if not rest or not rest[0].isupper():
            return False
    if "%" in raw or "/" in raw:
        return False
    low = raw.lower()
    if any(low.startswith(p) for p in _TITLE_SKIP):
        return False
    if any(word in low for word in _TITLE_NOISE):
        return False
    if any(ch.isdigit() for ch in raw[:3]):
        return False
    words = raw.split()
    if low.startswith("the ") and len(words) > 4:
        return False
    limit = 10 if street else 8
    return 1 <= len(words) <= limit and raw[0].isupper()
