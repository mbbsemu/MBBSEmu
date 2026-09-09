"""Party join / follow / rank / heal. One session per character — never shared.

Brain asks; it does not invent who fronts or who to follow. Rank defaults come
from modules.roles (warrior/paladin front, ninja/thief back). Names only
identify a toon — never behavior flags.
"""

from __future__ import annotations

from dataclasses import dataclass

from .roles import (
    BACK_CLASSES,
    CLASS_BIAS,
    FRONT_CLASSES,
    MID_CLASSES,
    always_front as role_always_front,
    class_bias as role_class_bias,
    class_rank as role_class_rank,
)
from .runtime import Offer

# Spoken party protocol (not the `health` command).
HEAL_ASK = "!heal"
HEALED_SAY = "!healed"
REST_CALL = "!rest"
RESTED_SAY = "!rested"
JOIN_CALL = "!join"
INVITE_RETRY = 8.0
HEAL_RATIO = 0.80

# Named campaign leader. Config may override; json does not have to.
DEFAULT_LEADER = "Matt"

# Profiles that launch with --auto-play (hunt/kit). Everyone else waits for F7.
# Login clients only pass the flag — they do not invent hunt rules.
AUTO_PLAY_PROFILES = frozenset({"klymacks", "sysop", "matt", "ryan", "robald"})

# profile, bbs user, given, class, race — identity, not behavior flags.
CHARS: tuple[tuple[str, str, str, str, str], ...] = (
    ("klymacks", "sysop", "klymacks", "ninja", "dark-elf"),
    ("matt", "matt", "Matthew", "paladin", "human"),
    ("ryan", "ryan", "Ryan", "thief", "halfling"),
    ("sarah", "sarah", "Sarah", "gypsy", "goblin"),
    ("alex", "alex", "Alex", "mage", "gaunt one"),
    ("robald", "robald", "Robald", "mystic", "gaunt one"),
    ("kevin", "kevin", "Kevin", "warrior", "half-ogre"),
    ("sherry", "sherry", "Sherry", "druid", "human"),
    ("emily", "emily", "Emily", "bard", "elf"),
    ("rita", "rita", "Rita", "witchunter", "half-ogre"),
    ("audrey", "audrey", "Audrey", "warlock", "elf"),
    ("curtis", "curtis", "Curtis", "ranger", "human"),
    ("betty", "betty", "Betty", "missionary", "gnome"),
    ("rose", "rose", "Rose", "priest", "dwarf"),
    ("ron", "ron", "Ron", "cleric", "dwarf"),
    ("rhiannon", "rhiannon", "Rhiannon", "mystic", "human"),
)


def wants_auto_play(profile: str = "") -> bool:
    """True when play-wg / desktop should pass --auto-play for this profile."""
    return (profile or "").strip().lower() in AUTO_PLAY_PROFILES


def campaign_leader(player: dict[str, object] | None = None) -> str:
    """Party lead from thin login json, else DEFAULT_LEADER.

    Missing key → campaign default. Explicit empty string → solo (tests / rare).
    """
    if not isinstance(player, dict) or "party_leader" not in player:
        return DEFAULT_LEADER
    return str(player.get("party_leader") or "").strip()

RANK_CMD = {
    "front": "frontr",
    "mid": "midr",
    "back": "backr",
}
_FRONT_CFG = frozenset({"front", "fore", "frontr", "frontrank"})
_MID_CFG = frozenset({"mid", "midr", "midrank", "middle"})
_BACK_CFG = frozenset({"back", "backr", "backrank"})
_OFF_CFG = frozenset({"off", "none"})


def _roster_map(index: int) -> dict[str, str]:
    found: dict[str, str] = {}
    for profile, user, given, klass, race in CHARS:
        value = (klass, race)[index]
        for token in (profile, user, given):
            key = token.strip().lower()
            if key:
                found[key] = value
    found.setdefault("matthew", found.get("matt", "paladin" if index == 0 else "human"))
    return found


ROSTER_CLASS = _roster_map(0)
ROSTER_RACE = _roster_map(1)


def configured_rank(raw: str = "") -> str:
    """front / mid / back / off / '' (auto from class)."""
    key = (raw or "").strip().lower()
    if key in _OFF_CFG:
        return "off"
    if key in _FRONT_CFG:
        return "front"
    if key in _MID_CFG:
        return "mid"
    if key in _BACK_CFG:
        return "back"
    return ""


def roster_class(name: str = "") -> str:
    for token in str(name or "").replace(",", " ").split():
        got = ROSTER_CLASS.get(token.strip(".,!;:").lower())
        if got:
            return got
    return ""


def roster_race(name: str = "") -> str:
    for token in str(name or "").replace(",", " ").split():
        got = ROSTER_RACE.get(token.strip(".,!;:").lower())
        if got:
            return got
    return ""


def class_bias(klass: str = "") -> int:
    return role_class_bias(klass)


def rank_slots(n: int) -> tuple[int, int, int]:
    """(front, mid, back) from party size. Geometry only — we do not auto-mid."""
    if n <= 1:
        return (max(n, 0), 0, 0)
    if n == 2:
        return (1, 0, 1)
    base = n // 3
    extra = n % 3
    front = mid = back = base
    if extra >= 1:
        front += 1
    if extra >= 2:
        back += 1
    return (front, mid, back)


def same_toon(left: str, right: str) -> bool:
    """Matt / Matthew / matt — one person. klymacks stays a distinct token."""
    a = left.strip().lower()
    b = right.strip().lower()
    if not a or not b:
        return False
    if a == b:
        return True
    if a in b.split() or b in a.split():
        return True
    short, long = (a, b) if len(a) <= len(b) else (b, a)
    return len(short) >= 4 and long.startswith(short)


def on_roster(who: str = "") -> bool:
    """True for a campaign / WG toon, not a stranger or shopkeeper."""
    return bool(roster_class(who))


def in_room(who: str, room_pcs: list[str] | tuple[str, ...] = ()) -> bool:
    return any(same_toon(who, seen) for seen in room_pcs if str(seen).strip())


def given_case(name: str) -> str:
    raw = name.strip()
    if not raw:
        return ""
    if raw.lower() == "klymacks":
        return "klymacks"
    if len(raw) == 1:
        return raw.upper()
    return raw[:1].upper() + raw[1:].lower()


def call_name(
    who: str,
    me: str = "",
    leader: str = "",
    aka: list[str] | tuple[str, ...] = (),
) -> str:
    """In-game given name: Matthew, not the BBS login Matt. klymacks stays lower."""
    raw = who.strip()
    if not raw:
        return ""
    best = raw
    names = [leader, *str(me).replace(",", " ").split(), *[str(part) for part in aka]]
    for name in names:
        if not name or not same_toon(raw, name):
            continue
        if len(name.strip()) > len(best.strip()):
            best = name
    for _profile, _user, given, _klass, _race in CHARS:
        if given and same_toon(raw, given) and len(given) > len(best.strip()):
            best = given
    if best.strip().lower() == raw.lower():
        return raw
    return given_case(best)


def is_self_name(
    who: str,
    me: str = "",
    aka: list[str] | tuple[str, ...] = (),
) -> bool:
    if not who.strip():
        return False
    if same_toon(who, me):
        return True
    for token in str(me).replace(",", " ").split():
        if token and same_toon(who, token):
            return True
    return any(same_toon(who, part) for part in aka if str(part).strip())


def identity_key(me: str = "") -> str:
    """Stable session key: given name, never BBS sysop. klymacks stays lower."""
    parts = [p for p in str(me).replace(",", " ").split() if p]
    for part in reversed(parts):
        token = part.strip(".,!;:").lower()
        if token not in {"sysop", "guest"}:
            return "klymacks" if token == "klymacks" else token
    return (parts[0].strip().lower() if parts else "") or ""


def class_rank(klass: str = "", name: str = "") -> str:
    """Default row from class: warrior/paladin front, ninja/thief/gypsy back."""
    job = (klass or roster_class(name) or "").strip().lower()
    return role_class_rank(job)


def always_front(klass: str = "", name: str = "") -> bool:
    job = (klass or roster_class(name) or "").strip().lower()
    return role_always_front(job)


def desired_rank(klass: str = "", name: str = "", cfg: str = "") -> str:
    """Config wins (including explicit mid). Else class default. Never auto-mid."""
    row = configured_rank(cfg)
    if row == "off":
        return "front"
    if row:
        return row
    return class_rank(klass, name)


def rank_for(
    me: str,
    klass: str = "",
    members: list[tuple[str, str]] | None = None,
) -> str:
    """Which row this toon occupies. Class/role only — size does not shuffle mid."""
    del members
    return class_rank(klass, me)


def rank_cmd(row: str = "") -> str:
    return RANK_CMD.get((row or "").strip().lower(), "")


def is_named_leader(me: str = "", leader: str = "", followed: bool = False) -> bool:
    if followed or not leader.strip():
        return False
    return is_self_name(leader, me)


def accept_inviter(
    who: str,
    me: str = "",
    leader: str = "",
    aka: list[str] | tuple[str, ...] = (),
    *,
    join_call: bool = False,
) -> bool:
    # Self check uses `me` only. Party alts (matt on a klymacks window) must
    # never look like self — that blocks the leader's invite.
    del aka
    if not who or is_self_name(who, me):
        return False
    if join_call:
        # Rally shout: follow the speaker (Sherry), not only the named Matt.
        return on_roster(who)
    if leader and same_toon(leader, who):
        return True
    return on_roster(who)


def inviter_here(
    who: str,
    room_pcs: list[str] | tuple[str, ...] = (),
    *,
    scanned: bool = False,
) -> bool:
    """Same-room only. Unscanned (no Also here yet) may still honor the line."""
    if in_room(who, room_pcs):
        return True
    if room_pcs:
        return False
    # Empty listing after a look means they are not here (realm-enter ≠ presence).
    if scanned:
        return False
    return True


def park_rest_room(room: str, *, pit: bool = False) -> bool:
    """GY / shack / crypt / sewer — walk to the bridge. Owned by modules.maps."""
    from .maps import park_rest_room as _park

    return _park(room, pit=pit)


@dataclass
class PartySession:
    """One live client's party view. Construct one per Brain — never share.

    Keys by character identity so Matt's hunt/follow/rank cannot leak onto Ryan.
    """

    identity: str
    klass: str = ""
    race: str = ""
    area: str = ""
    following: str = ""
    followers: tuple[str, ...] = ()
    rank: str = ""
    mates: tuple[tuple[str, str], ...] = ()

    def key(self) -> str:
        return identity_key(self.identity)

    def note_area(self, room: str) -> None:
        self.area = (room or "").strip()

    def note_follow(
        self,
        who: str = "",
        followers: list[str] | tuple[str, ...] | None = None,
    ) -> None:
        self.following = (who or "").strip()
        if followers is not None:
            self.followers = tuple(str(name).strip() for name in followers if str(name).strip())

    def note_rank(self, row: str) -> None:
        self.rank = (row or "").strip().lower()

    def note_mates(self, members: list[tuple[str, str]] | tuple[tuple[str, str], ...]) -> None:
        self.mates = tuple(
            (name.strip(), (klass or roster_class(name) or "").strip().lower())
            for name, klass in members
            if str(name).strip()
        )


@dataclass(frozen=True)
class PartyFacts:
    me: str
    klass: str = ""
    leader: str = ""
    auto_join: bool = True
    in_realm: bool = False
    following: str = ""
    invited_by: str = ""
    join_call_by: str = ""
    room_pcs: tuple[str, ...] = ()
    room_scanned: bool = False
    follow_sent_to: str = ""
    rank_sent: str = ""
    party_rank: str = ""
    cfg_rank: str = ""
    named_leader: bool = False
    followed: bool = False
    ranked: bool = False
    mode: str = ""
    aka: tuple[str, ...] = ()
    area: str = ""
    can_cast_heal: bool = False
    with_leader: bool = False
    asked_heal: bool = False
    healed_shouted: bool = False
    rested_up: bool = False
    hurt_ally: bool = False
    hp_ratio: float | None = None
    needs_heal: bool = False
    session_key: str = ""


def _who(facts: PartyFacts) -> tuple[str, bool]:
    invited = (facts.invited_by or "").strip()
    if invited:
        return invited, False
    called = (facts.join_call_by or "").strip()
    if called:
        return called, True
    return "", False


def join_action(facts: PartyFacts) -> Offer | None:
    """One `follow Name` for a roster invite from someone in this room."""
    if not facts.auto_join or facts.named_leader or not facts.in_realm:
        return None
    if facts.mode == "goto":
        return None
    who, join_call = _who(facts)
    if not who or who.lower() == "you":
        return None
    if not accept_inviter(
        who, facts.me, facts.leader, facts.aka, join_call=join_call
    ):
        return None
    if facts.following and same_toon(facts.following, who):
        return None
    if facts.follow_sent_to and same_toon(facts.follow_sent_to, who):
        return None
    if not inviter_here(who, facts.room_pcs, scanned=facts.room_scanned):
        return None
    named = call_name(who, me=facts.me, leader=facts.leader, aka=facts.aka)
    return Offer(
        kind="follow",
        name=who,
        command=f"follow {named}",
        ask=False,
        reason="join_call" if join_call else "invite",
    )


def follow_pending(facts: PartyFacts) -> bool:
    """True while a follow we honor is in flight or still to send."""
    if not facts.auto_join or facts.named_leader or not facts.in_realm:
        return False
    if facts.following:
        return False
    if facts.follow_sent_to:
        return True
    return join_action(facts) is not None


def follow_sent_status(
    *,
    follow_sent_to: str,
    following: str,
    room_scanned: bool,
    inviter_present: bool,
    sent_at: float,
    now: float,
    retry_after: float = INVITE_RETRY,
) -> str:
    """Keep or clear a pending `follow X` so the footer cannot park forever.

    Returns: ``keep`` | ``forget_confirmed`` | ``forget_gone`` | ``forget_retry``.
    """
    who = (follow_sent_to or "").strip()
    if not who:
        return "keep"
    if following:
        if same_toon(following, who):
            return "forget_confirmed"
        return "keep"
    if room_scanned and not inviter_present:
        return "forget_gone"
    if sent_at > 0 and now - sent_at >= retry_after:
        return "forget_retry"
    return "keep"


def rank_action(facts: PartyFacts) -> Offer | None:
    """One `frontr` / `midr` / `backr` from class (or explicit config). Never spam.

    Front roles (warrior, paladin, …) must claim `frontr`. The mud often parks
    a new joiner Midrank when another tank is already front — never assume we
    are already front, and never invent mid from party size or mate count.
    """
    if facts.named_leader or facts.mode == "goto":
        return None
    if not (facts.followed or facts.following):
        return None
    want = desired_rank(facts.klass, facts.me, facts.cfg_rank)
    cmd = rank_cmd(want)
    if not cmd:
        return None
    have = (facts.party_rank or "").strip().lower()
    front = always_front(facts.klass, facts.me) or want == "front"
    if not have:
        if want == "front":
            # Unknown ≠ front. Game default for joiners is often mid.
            have = "mid"
        elif facts.ranked:
            return None
        else:
            # Back/mid wanters: assume front so we send the move once.
            have = "front"
    if want == have:
        return None
    if facts.rank_sent == cmd and not (want == "front" and have == "mid" and front):
        return None
    return Offer(
        kind="rank",
        name=want,
        command=cmd,
        ask=False,
        reason="config" if configured_rank(facts.cfg_rank) in {"front", "mid", "back"} else "class",
    )


def heal_ask_action(facts: PartyFacts) -> Offer | None:
    """Follower with no heal spell: one `!heal` at HEAL_RATIO or below."""
    if facts.can_cast_heal or not facts.with_leader or facts.asked_heal:
        return None
    if not facts.needs_heal:
        if facts.hp_ratio is None or facts.hp_ratio > HEAL_RATIO:
            return None
    return Offer(
        kind="say",
        name="heal",
        command=HEAL_ASK,
        ask=False,
        reason="hurt",
    )


def heal_ack_action(facts: PartyFacts) -> Offer | None:
    """One `!healed` when the asker is actually full. Never a second shout."""
    if facts.healed_shouted or not facts.asked_heal or not facts.rested_up:
        return None
    if facts.can_cast_heal and facts.hurt_ally:
        return None
    return Offer(
        kind="say",
        name="healed",
        command=HEALED_SAY,
        ask=False,
        reason="full",
    )
