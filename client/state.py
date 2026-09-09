"""Live snapshot of the MajorMUD session."""

from __future__ import annotations

import time
from collections import deque
from dataclasses import dataclass, field

from . import combat, parse, paths

# One combat round. Long DPS is damage-per-round over the hunt loop
# (fights + walks). Short is the last few rounds against that norm.
HIT_WINDOW = 8.0
SHORT_WINDOW = 48.0
SESSION_KEEP = 3600.0
SESSION_IDLE = 180.0
TREND_WARMUP = 48.0
TREND_BAND = 0.10


def pool_label(
    name: str,
    cur: int | None,
    mx: int | None = None,
    *,
    pct: int | None = None,
    stale: bool = False,
    ready: bool = False,
    ready_name: str = "TRAIN",
) -> str:
    """HP, MA, and EXP footer text. Flags pick the shape; one builder."""
    if ready:
        shown = pct if pct is not None else 100
        return f"{ready_name} {shown}%"
    if cur is None and pct is None:
        return ""
    mark = "?" if stale else ""
    if cur is not None and mx:
        core = f"{name} {cur}/{mx}"
        if pct is not None:
            return f"{core} {pct}%{mark}"
        return core
    if cur is not None:
        return f"{name} {cur}"
    if pct is not None:
        return f"{name} {pct}%{mark}"
    return ""


@dataclass
class WorldState:
    hp: int | None = None
    max_hp: int | None = None
    max_hp_known: bool = False
    ma: int | None = None
    max_ma: int | None = None
    cast_fail: str = ""
    blessed: bool = False
    willed: bool = False
    room: str = ""
    exits: list[str] = field(default_factory=list)
    closed_exits: list[str] = field(default_factory=list)
    mobs: list[str] = field(default_factory=list)
    things: list[str] = field(default_factory=list)
    in_combat: bool = False
    combat_off: bool = False
    in_shop: bool = False
    resting: bool = False
    last_kill: str = ""
    prompt_seq: int = 0
    in_realm: bool = False
    dark: bool = False
    torch_lit: bool = False
    geared: bool = False
    inventory: list[str] = field(default_factory=list)
    extras: list[str] = field(default_factory=list)
    worn: list[str] = field(default_factory=list)
    already_worn: str = ""
    last_sold: str = ""
    inv_seq: int = 0
    wealth_copper: int | None = None
    deposited: bool = False
    bank_fail: bool = False
    shop_vague: bool = False
    learned: bool = False
    spell_skip: bool = False
    known_spells: list[str] = field(default_factory=list)
    spellbook_seq: int = 0
    flooded: bool = False
    needs_scan: bool = False
    scanned: bool = False
    look_scan: bool = False
    # Bumps when a move reprint / exits prove we are on a (maybe same-title) tile.
    travel_seq: int = 0
    saw_here: bool = False
    saw_see: bool = False
    whiff: bool = False
    blocked: bool = False
    blocked_dir: str = ""
    arena_gated: bool = False
    pvp_hit: str = ""
    self_names: set[str] = field(default_factory=set)
    last_actor: str = ""
    friendly_fire: str = ""
    invited_by: str = ""
    join_call_by: str = ""
    rest_call_by: str = ""
    following: str = ""
    followers: list[str] = field(default_factory=list)
    # Leader's swing target, or a farm mob hitting the party, while we follow.
    ally_aim: str = ""
    # Last farm mob that hit this window (`... at you`).
    aggro: str = ""
    backrank: bool = False
    party_rank: str = ""
    party_fail: str = ""
    not_here: str = ""
    invite_ok: str = ""
    arrivals: list[str] = field(default_factory=list)
    sneak_try: bool = False
    sneak_ok: bool = False
    sneak_fail: bool = False
    sneak_busy: bool = False
    mortal: bool = False
    just_died: bool = False
    ally_mortal: str = ""
    aided: bool = False
    bleeding: bool = False
    dragging: str = ""
    drag_fail: bool = False
    afraid: bool = False
    # Observed party cadence — not our own rest.
    ally_rest: str = ""
    ally_stood: bool = False
    ally_fled: str = ""
    ally_wounded: str = ""
    left_party: bool = False
    # lowercase given name -> spelling we saw. Observed hits only; no invented HP.
    ally_hurt: dict[str, str] = field(default_factory=dict)
    # lowercase given name -> damage seen since last heal / they left.
    ally_dmg: dict[str, int] = field(default_factory=dict)
    # lowercase given name -> they asked for a heal (`!heal`).
    heal_asks: dict[str, str] = field(default_factory=dict)
    # `!healed` / `!rested` acks while the leader holds the run.
    healed_acks: dict[str, str] = field(default_factory=dict)
    rested_acks: dict[str, str] = field(default_factory=dict)
    # lowercase given name -> we landed bless on them (luck / crits).
    ally_blessed: dict[str, str] = field(default_factory=dict)
    # From `exp` / train. Max HP/MA stay put until a level or train.
    klass: str = ""
    level: int | None = None
    trained: bool = False
    exp: int | None = None
    exp_needed: int | None = None
    exp_next: int | None = None
    exp_pct: int | None = None
    exp_known: bool = False
    exp_stale: bool = False
    exp_asked: bool = False
    # This fight's `You gain N` already counted. Kill must not re-ask `exp`.
    exp_gained: bool = False
    strength: int | None = None
    agility: int | None = None
    intellect: int | None = None
    willpower: int | None = None
    charm: int | None = None
    attack: int | None = None
    ac: int | None = None
    stat_asked: bool = False
    stat_known: bool = False
    # (monotonic, damage) for `You … for N damage` this hunt session.
    _dealt: deque[tuple[float, int]] = field(default_factory=deque)

    def apply(self, event: dict[str, object]) -> None:
        kind = event.get("kind")
        if kind == "prompt":
            self.hp = int(event["hp"])  # type: ignore[arg-type]
            self._note_max(event.get("max_hp"))
            self._note_ma(event.get("ma"), event.get("max_ma"))
            self.prompt_seq += 1
            self.in_realm = True
            self.resting = False
            self._note_hits()
            return
        if kind == "hits":
            self.hp = int(event["hp"])  # type: ignore[arg-type]
            self._note_max(event.get("max_hp"))
            self._note_ma(event.get("ma"), event.get("max_ma"))
            self._note_hits()
            return
        if kind == "mana":
            self._note_ma(event.get("ma"), event.get("max_ma"))
            return
        if kind == "trained":
            self.forget_maxes()
            self.forget_exp()
            self.forget_stat()
            self._reset_dealt()
            lvl = event.get("level")
            if isinstance(lvl, int) and lvl > 0:
                self._note_level(lvl)
            return
        if kind == "level":
            lvl = event.get("level")
            if isinstance(lvl, int) and lvl > 0:
                self._note_level(lvl)
            self._note_exp(event)
            self._reset_dealt()
            return
        if kind == "experience":
            self._gain_exp(event.get("amount"))
            return
        if kind == "stats":
            self._note_stats(event)
            return
        if kind == "cast_fail":
            self.cast_fail = str(event.get("reason") or "fail")
            return
        if kind == "buff":
            name = str(event.get("name") or "").strip().lower()
            if name == "bless":
                self._note_bless(event)
            elif name in {"way of the owl", "owl"}:
                self.willed = bool(event.get("on", True))
            return
        if kind == "invited":
            who = str(event.get("name") or "").strip()
            if who and event.get("by_me"):
                self.invite_ok = who
                return
            if who:
                self.invited_by = who
            return
        if kind == "realm_enter":
            self.in_realm = True
            return
        if kind == "not_here":
            who = str(event.get("name") or "").strip()
            self.not_here = who
            if who:
                self._drop_presence(who)
            return
        if kind == "join_call":
            who = str(event.get("name") or "").strip()
            if not who or who.lower() == "you":
                return
            if not self._is_toon(who):
                return
            self._remember_toon(who)
            self.join_call_by = who
            return
        if kind == "rest_call":
            who = str(event.get("name") or "").strip()
            if not who or who.lower() == "you":
                return
            if not self._is_toon(who):
                return
            self._remember_toon(who)
            self.rest_call_by = who
            return
        if kind == "rested":
            who = str(event.get("name") or "").strip()
            if not who or who.lower() == "you":
                return
            if not self._is_toon(who):
                return
            self._remember_toon(who)
            self.rested_acks[who.lower()] = who
            return
        if kind == "healed":
            who = str(event.get("name") or "").strip()
            if not who or who.lower() == "you":
                return
            if not self._is_toon(who):
                return
            self._remember_toon(who)
            self.healed_acks[who.lower()] = who
            self.heal_asks = {
                k: v
                for k, v in self.heal_asks.items()
                if k != who.lower() and v.lower() != who.lower()
            }
            return
        if kind == "following":
            self.following = str(event.get("name") or "").strip()
            self.invited_by = ""
            self.join_call_by = ""
            self.left_party = False
            return
        if kind == "followed":
            who = str(event.get("name") or "").strip()
            if who and who not in self.followers:
                self.followers.append(who)
            return
        if kind == "ranked":
            who = str(event.get("name") or "").strip()
            if who and who not in self.followers:
                self.followers.append(who)
            return
        if kind == "backrank":
            self.backrank = True
            self.party_rank = "back"
            return
        if kind == "rank":
            row = str(event.get("row") or "").strip().lower()
            if row == "middle":
                row = "mid"
            if row in {"front", "mid", "back"}:
                self.party_rank = row
                self.backrank = row == "back"
            return
        if kind == "party_fail":
            self.party_fail = str(event.get("reason") or "fail")
            self.invited_by = ""
            self.join_call_by = ""
            if self.party_fail == "invite":
                self.following = ""
            if self.party_fail == "party":
                self.following = ""
                self.backrank = False
                self.party_rank = ""
                self.left_party = True
            return
        if kind == "sneak_try":
            self.sneak_try = True
            return
        if kind == "sneak_ok":
            self.sneak_ok = True
            return
        if kind == "sneak_fail":
            self.sneak_fail = True
            self.sneak_busy = event.get("reason") == "busy"
            return
        if kind == "mortal":
            who = str(event.get("name") or "").strip()
            if not who or who.lower() == "you":
                self.mortal = True
                self.bleeding = True
                self.aided = False
            else:
                self.ally_mortal = who
            return
        if kind == "aided":
            self.aided = True
            self.bleeding = False
            who = str(event.get("name") or "").strip()
            if not who or who.lower() == "you":
                self.mortal = False
                self.bleeding = False
            elif self.ally_mortal and who.lower() in self.ally_mortal.lower():
                self.ally_mortal = ""
            else:
                self.ally_mortal = ""
            return
        if kind == "drag_fail":
            self.drag_fail = True
            return
        if kind == "dragging":
            self.dragging = str(event.get("name") or "").strip()
            return
        if kind == "afraid":
            self.afraid = True
            return
        if kind == "left":
            self.following = ""
            self.backrank = False
            self.party_rank = ""
            self.invited_by = ""
            self.left_party = True
            return
        if kind == "exits":
            self.exits = list(event.get("exits") or [])  # type: ignore[arg-type]
            self.closed_exits = list(event.get("closed") or [])  # type: ignore[arg-type]
            if self.look_scan and not self.saw_here:
                self.mobs = self._presence_pcs(self.mobs)
            if self.look_scan and not self.saw_see:
                self.things = []
            self.look_scan = False
            self.scanned = True
            self.needs_scan = False
            self.travel_seq += 1
            self.in_realm = True
            return
        if kind == "also_here":
            mobs: list[str] = []
            for name in event.get("mobs") or []:
                if not isinstance(name, str):
                    continue
                mobs.extend(paths.peel_presence(name, self.self_names))
            self.mobs = mobs
            self.saw_here = True
            self.look_scan = False
            self.scanned = True
            self.needs_scan = False
            self.in_realm = True
            present = {n.lower() for n in paths.players_in(self.mobs, self.self_names)}
            self.ally_hurt = {
                k: v for k, v in self.ally_hurt.items() if k in present or v.lower() in present
            }
            self.ally_dmg = {k: v for k, v in self.ally_dmg.items() if k in self.ally_hurt}
            self.heal_asks = {
                k: v for k, v in self.heal_asks.items() if k in present or v.lower() in present
            }
            return
        if kind == "you_see":
            self.things = list(event.get("things") or [])  # type: ignore[arg-type]
            self.saw_see = True
            self.scanned = True
            self.in_realm = True
            return
        if kind == "heal_ask":
            who = str(event.get("name") or "").strip()
            if not who or who.lower() == "you":
                return
            if not self._is_toon(who):
                return
            self._remember_toon(who)
            self.heal_asks[who.lower()] = who
            return
        if kind == "said":
            self.whiff = True
            self.in_combat = False
            aimed = str(event.get("aimed") or "")
            if aimed:
                self.mobs = [m for m in self.mobs if not paths.same_mob(m, aimed)]
                if paths.is_home_account(aimed) or paths.is_self(aimed, self.self_names):
                    self.friendly_fire = aimed
            return
        if kind == "drop":
            name = str(event.get("name") or "")
            if name and name not in self.things:
                self.things.append(name)
            return
        if kind == "killed":
            self.last_kill = str(event.get("name") or "")
            self.in_combat = False
            self.needs_scan = True
            dead = self.last_kill
            self.mobs = paths.without_dead(self.mobs, dead)
            self._drop_ally_hurt(dead)
            if self.ally_mortal and dead.lower() in self.ally_mortal.lower():
                self.ally_mortal = ""
            # A full Exp: line is enough. Kill must not flip needs_exp.
            if not self.has_exp_reading() and not self.exp_gained:
                self.exp_stale = True
                self.exp_asked = False
            self.exp_gained = False
            self._drop_fight_aim(dead)
            return
        if kind == "death":
            self.just_died = True
            self.in_combat = False
            self.mortal = False
            self.bleeding = False
            # The game drops the corpse from the group. Stale follow lines
            # must not keep `goto pile` / `goto gy` sending leave forever.
            self.following = ""
            self.left_party = True
            return
        if kind == "combat":
            self.in_combat = True
            actor = event.get("actor")
            if isinstance(actor, str) and actor.strip():
                self.last_actor = actor.strip()
                if paths.is_self(actor, self.self_names):
                    self.self_names.add(actor.strip().lower())
                self._remember_toon(actor)
            aim = event.get("aim")
            if (
                isinstance(aim, str)
                and aim.strip()
                and isinstance(actor, str)
                and actor.strip()
                and self._is_leader(actor)
            ):
                self.ally_aim = aim.strip()
            name = event.get("name")
            victim = event.get("victim")
            if isinstance(name, str) and name:
                for piece in paths.peel_presence(name, self.self_names):
                    toon = self._is_toon(piece)
                    if toon and not paths.is_self(piece, self.self_names):
                        self.pvp_hit = piece
                    if not toon and paths.is_self(piece, self.self_names):
                        continue
                    if piece not in self.mobs:
                        self.mobs.append(piece)
                        self.saw_here = True
                        self.look_scan = False
                hit_name = name.strip()
                if hit_name and paths.lop_in([hit_name]):
                    who = victim.strip() if isinstance(victim, str) else ""
                    if not who or who.lower() == "you":
                        self.aggro = hit_name
                    elif self._is_leader(who):
                        self.ally_aim = hit_name
            self._note_ally_hit(event)
            dealt = event.get("dealt")
            if isinstance(dealt, int) and dealt > 0:
                self.note_dealt(dealt)
            return
        if kind == "actor":
            who = str(event.get("name") or "").strip()
            if who:
                self.last_actor = who
            return
        if kind == "combat_off":
            self.in_combat = False
            self.pvp_hit = ""
            self.combat_off = True
            self.ally_aim = ""
            self.aggro = ""
            # Fight is over — drop leftover farm names. Keep party / PCs.
            # A later arrive / Also here / combat line is the live list.
            self.mobs = [m for m in self.mobs if not paths.lop_in([m])]
            # Already had Also here / a move scan — do not treat Off as empty.
            if not paths.lop_in(self.mobs) and not self.scanned:
                self.needs_scan = True
            return
        if kind == "inventory":
            blob = str(event.get("text") or "")
            low = blob.lower()
            self.geared = "(weapon" in low or "weapon hand" in low
            items = event.get("items")
            extras = event.get("extras")
            worn = event.get("worn")
            if isinstance(items, list):
                self.inventory = [str(x).lower() for x in items]
            elif blob:
                self.inventory = paths.inventory_names(blob)
            if isinstance(extras, list):
                self.extras = [str(x).lower() for x in extras]
            elif blob:
                self.extras = paths.inventory_extras(blob)
            if isinstance(worn, list):
                self.worn = [str(x).lower() for x in worn]
            elif blob:
                self.worn = paths.inventory_worn(blob)
            # Fresh `i` is truth for burning lights.
            held = self.inventory + self.worn + self.extras
            self.torch_lit = paths.has_lit_torch(held)
            self.inv_seq += 1
            self.wealth_copper = paths.purse_copper(self.inventory)
            return
        if kind == "wealth":
            copper = event.get("copper")
            if isinstance(copper, int) and copper >= 0:
                self.wealth_copper = copper
            return
        if kind == "deposit":
            if event.get("fail"):
                self.bank_fail = True
                return
            copper = event.get("copper")
            n = copper if isinstance(copper, int) and copper >= 0 else 0
            if event.get("withdraw"):
                self.wealth_copper = (self.wealth_copper or 0) + n
                self.deposited = False
                return
            self.wealth_copper = 0
            self.deposited = True
            return
        if kind == "sold":
            item = str(event.get("item") or "").strip().lower()
            self.last_sold = item
            self.shop_vague = False
            return
        if kind == "already_worn":
            item = str(event.get("item") or "").strip().lower()
            self.already_worn = item
            if item and item not in self.worn:
                self.worn.append(item)
            return
        if kind == "flood":
            self.flooded = True
            return
        if kind == "shop_vague":
            self.shop_vague = True
            return
        if kind == "arrive":
            name = str(event.get("name") or "")
            for piece in paths.peel_presence(name, self.self_names):
                if piece and piece not in self.mobs:
                    self.mobs.append(piece)
                if piece:
                    self._remember_toon(piece)
                    if self._is_toon(piece):
                        self.arrivals.append(piece)
            self.saw_here = True
            self.look_scan = False
            self.scanned = True
            self.needs_scan = False
            return
        if kind == "leave":
            self._drop_presence(event.get("name") or "")
            return
        if kind == "rest":
            actor = event.get("actor")
            who = actor.strip() if isinstance(actor, str) else ""
            if who:
                self.last_actor = who
            if who and self._is_toon(who) and not self._is_me(who):
                self.ally_rest = who
                self.ally_stood = False
                return
            self.resting = True
            self.in_combat = False
            return
        if kind == "stand":
            actor = event.get("actor")
            who = actor.strip() if isinstance(actor, str) else ""
            if who and self._is_toon(who) and not self._is_me(who):
                self.ally_stood = True
                self.ally_rest = ""
                return
            self.resting = False
            return
        if kind == "flee":
            who = str(event.get("name") or "").strip()
            step = str(event.get("dir") or "").strip().lower()
            if who and self._is_toon(who) and not self._is_me(who):
                self.ally_fled = step or "w"
            return
        if kind == "wounded":
            who = str(event.get("name") or "").strip()
            if who and self._is_toon(who) and not self._is_me(who):
                self.ally_wounded = who
                self._remember_toon(who)
            return
        if kind == "shop":
            self.in_shop = True
            return
        if kind == "bought":
            self.shop_vague = False
            return
        if kind == "learned":
            self.learned = True
            self.spell_skip = False
            return
        if kind == "spellbook":
            if event.get("reset"):
                self.known_spells = []
            names = event.get("names")
            if isinstance(names, list):
                for raw in names:
                    low = str(raw).strip().lower()
                    if low and low not in self.known_spells:
                        self.known_spells.append(low)
            name = event.get("name")
            if isinstance(name, str):
                low = name.strip().lower()
                if low and low not in self.known_spells:
                    self.known_spells.append(low)
            self.spellbook_seq += 1
            return
        if kind == "spell_skip":
            self.spell_skip = True
            return
        if kind == "room":
            title = str(event.get("title") or "")
            if parse.looks_like_stat_sheet(title):
                return
            if self.in_combat and not _looks_like_place(title):
                return
            moved = bool(self.room) and title != self.room
            self.room = title
            self.dark = False
            if _looks_like_place(title):
                self.in_realm = True
            if moved:
                if self.in_combat:
                    self.combat_off = True
                self.mobs = []
                self.things = []
                # Drop prior tile exits — Entry's `sw` must not linger on Bridge
                # or goto ts / hunt will wall-spam a stale door.
                self.exits = []
                self.closed_exits = []
                self.scanned = False
                self.in_combat = False
                self.in_shop = False
                self._wipe_allies()
            if _looks_like_place(title) and not self.in_combat:
                self.look_scan = True
                self.saw_here = False
                self.saw_see = False
            return
        if kind == "dark":
            self.dark = True
            return
        if kind == "torch_lit":
            self.torch_lit = True
            self.dark = False
            return
        if kind == "torch_out":
            self.torch_lit = False
            return
        if kind == "cannot":
            text = str(event.get("text") or "").lower()
            if "may not drag" in text or "cannot drag" in text or "can't drag" in text:
                self.drag_fail = True
            if "sell" in text and "shop" in text:
                self.in_shop = False
            gated = bool(event.get("arena")) or any(
                mark in text
                for mark in (
                    "arena",
                    "too experienced",
                    "too high",
                    "not permitted",
                    "no longer",
                )
            )
            if gated:
                self.arena_gated = True
                self.blocked = True
                if "d" in self.exits:
                    self.exits = [x for x in self.exits if x != "d"]
                return
            if (
                "no exit" in text
                or "can't go" in text
                or "cannot go" in text
                or "gate is closed" in text
                or "door is closed" in text
                or "closed door" in text
                or "closed gate" in text
                or "ran into the wall" in text
                or event.get("wall")
            ):
                self.blocked = True
                wall_dir = str(event.get("dir") or "").strip().lower()
                if wall_dir:
                    self.blocked_dir = wall_dir
                if "d" in self.exits:
                    self.exits = [x for x in self.exits if x != "d"]
            return

    def _is_me(self, name: str) -> bool:
        """This window's toon — not every Finn's account on the board."""
        mine = {x.lower() for x in self.self_names if x}
        if not mine:
            return False
        tokens = [w.strip(".,!;:").lower() for w in name.split() if w.strip(".,!;:")]
        return any(token in mine for token in tokens)

    def _names_match(self, left: str, right: str) -> bool:
        a = left.strip().lower()
        b = right.strip().lower()
        if not a or not b:
            return False
        if a == b:
            return True
        return a in b.split() or b in a.split()

    def _is_leader(self, name: str) -> bool:
        """True when `name` is the toon this window is following."""
        who = (self.following or "").strip()
        if not who or not name.strip():
            return False
        return self._names_match(name, who)

    def _drop_fight_aim(self, dead: str) -> None:
        if dead and self.ally_aim and paths.same_mob(self.ally_aim, dead):
            self.ally_aim = ""
        if dead and self.aggro and paths.same_mob(self.aggro, dead):
            self.aggro = ""

    def _is_toon(self, name: str) -> bool:
        extras = self.self_names
        return (
            paths.is_given_name(name, extras)
            or paths.is_player(name)
            or paths.is_home_account(name)
        )

    def _presence_pcs(self, names: list[str]) -> list[str]:
        """Keep standing PCs when a look reprint omits Also here."""
        kept: list[str] = []
        seen: set[str] = set()
        extras = self.self_names
        for raw in names:
            for piece in paths.peel_presence(str(raw), extras):
                if not piece or not self._is_toon(piece):
                    continue
                key = piece.strip().lower()
                if key in seen:
                    continue
                seen.add(key)
                kept.append(piece)
        return kept

    def _remember_toon(self, name: str) -> None:
        """Keep the other PC in the room list so party can invite/join."""
        extras = self.self_names
        for piece in paths.peel_presence(name, extras):
            if not piece or not self._is_toon(piece):
                continue
            if piece not in self.mobs:
                self.mobs.append(piece)
                self.saw_here = True
                self.look_scan = False

    def _note_ally_hit(self, event: dict[str, object]) -> None:
        victim = event.get("victim")
        if not isinstance(victim, str):
            return
        who = victim.strip()
        if not who or who.lower() == "you":
            return
        if not self._is_toon(who):
            return
        self._remember_toon(who)
        dmg = event.get("damage")
        if isinstance(dmg, int) and dmg > 0:
            self.ally_hurt[who.lower()] = who
            self.ally_dmg[who.lower()] = self.ally_dmg.get(who.lower(), 0) + dmg

    def ally_taken(self, key: str) -> int:
        return int(self.ally_dmg.get(key.strip().lower(), 0))

    def _note_bless(self, event: dict[str, object]) -> None:
        on = bool(event.get("on", True))
        target = str(event.get("target") or "").strip()
        if event.get("self") or not target or self._is_me(target):
            self.blessed = on
            return
        if on:
            self.ally_blessed[target.lower()] = target
            return
        self.forget_ally_bless(target)

    def ally_has_bless(self, *names: str) -> bool:
        lows = {name.strip().lower() for name in names if name and name.strip()}
        return any(
            k in lows or v.lower() in lows for k, v in self.ally_blessed.items()
        )

    def mark_ally_bless(self, name: str) -> None:
        who = name.strip()
        if who:
            self.ally_blessed[who.lower()] = who

    def forget_ally_bless(self, *names: str) -> None:
        lows = {name.strip().lower() for name in names if name and name.strip()}
        if not lows:
            return
        self.ally_blessed = {
            k: v
            for k, v in self.ally_blessed.items()
            if k not in lows and v.lower() not in lows
        }

    def forget_ally(self, *names: str) -> None:
        lows = {name.strip().lower() for name in names if name and name.strip()}
        if not lows:
            return
        self.ally_hurt = {
            k: v
            for k, v in self.ally_hurt.items()
            if k not in lows and v.lower() not in lows
        }
        self.ally_dmg = {k: v for k, v in self.ally_dmg.items() if k not in lows}
        self.heal_asks = {
            k: v
            for k, v in self.heal_asks.items()
            if k not in lows and v.lower() not in lows
        }

    def _wipe_allies(self) -> None:
        self.ally_hurt.clear()
        self.ally_dmg.clear()
        self.heal_asks.clear()
        self.healed_acks.clear()
        self.rested_acks.clear()
        self.ally_rest = ""
        self.ally_stood = False
        self.ally_wounded = ""
        self.ally_aim = ""
        self.aggro = ""

    def _drop_presence(self, raw: str | object) -> None:
        """They left this tile, or `invite` proved they were never here."""
        name = str(raw or "").strip()
        if not name:
            return
        low = name.lower()
        self.mobs = [
            m for m in self.mobs if low not in m.lower() and m.lower() not in low
        ]
        self._drop_ally_hurt(name)

    def _drop_ally_hurt(self, raw: str) -> None:
        low = raw.strip().lower()
        if not low:
            return
        self.ally_hurt = {
            k: v
            for k, v in self.ally_hurt.items()
            if k not in low and v.lower() not in low and low not in k
        }
        self.ally_dmg = {
            k: v for k, v in self.ally_dmg.items() if k not in low and low not in k
        }
        self.heal_asks = {
            k: v
            for k, v in self.heal_asks.items()
            if k not in low and v.lower() not in low and low not in k
        }

    def empty_if_look_missed(self, kinds: set[str], screen: str = "") -> None:
        if "exits" not in kinds:
            return
        if self.saw_here or "also_here" in kinds or "arrive" in kinds:
            return
        if "also here:" in screen.lower():
            return
        self.mobs = self._presence_pcs(self.mobs)
        if not self.mobs:
            self._wipe_allies()
        self.scanned = True
        self.look_scan = False

    def needs_maxes(self) -> bool:
        """True when the footer cannot show a real max HP and/or max MA."""
        if self.hp is None:
            return False
        if not self.max_hp_known:
            return True
        return self.ma is not None and self.max_ma is None

    def forget_maxes(self) -> None:
        """Level/train changes pools. Keep last numbers on the bar until `health`."""
        self.max_hp_known = False
        self.max_ma = None
        self.trained = True

    def forget_exp(self) -> None:
        """Level/train resets the exp bar. Ask `exp` again."""
        self.exp_known = False
        self.exp_stale = True
        self.exp_asked = False
        self.exp_pct = None
        self.exp_gained = False

    def forget_stat(self) -> None:
        """Level/train changes Attack. Keep last numbers on the bar until `stat`."""
        self.stat_known = False
        self.stat_asked = False

    def has_exp_reading(self) -> bool:
        """True when chrome has current, total, and percent from `Exp:`."""
        return (
            bool(self.exp_known)
            and self.exp is not None
            and self.exp_next is not None
            and self.exp_pct is not None
        )

    def needs_exp(self) -> bool:
        """True only when we have no current/total/percent yet."""
        if self.hp is None:
            return False
        if self.has_exp_reading():
            return False
        if self.exp_asked:
            return False
        if self.exp_stale:
            return True
        return not self.exp_known

    def can_train(self) -> bool:
        if not self.exp_known:
            return False
        if self.exp_needed is not None and self.exp_needed <= 0:
            return True
        return self.exp_pct is not None and self.exp_pct >= 100

    def at_trainer(self) -> bool:
        return paths.is_trainer(self.room, self.klass)

    def exp_label(self) -> str:
        """Footer progress: percent first, then current/next. TRAIN when ready."""
        if self.can_train():
            return pool_label(
                "EXP",
                self.exp,
                self.exp_next,
                pct=self.exp_pct,
                ready=True,
            )
        if self.exp_pct is None:
            return ""
        mark = "?" if self.exp_stale else ""
        if self.exp is not None and self.exp_next:
            return f"EXP {self.exp_pct}% {self.exp}/{self.exp_next}{mark}"
        return f"EXP {self.exp_pct}%{mark}"

    def _note_level(self, lvl: int) -> None:
        if self.level is not None and lvl > self.level:
            self.forget_maxes()
            self.forget_exp()
            self.forget_stat()
        self.level = lvl

    def _note_stats(self, event: dict[str, object]) -> None:
        """Merge Strength/Agility/Attack/AC from a `stat` dump line."""
        for key in (
            "strength",
            "agility",
            "intellect",
            "willpower",
            "charm",
            "attack",
            "ac",
        ):
            val = event.get(key)
            if isinstance(val, int):
                setattr(self, key, val)
        if self.attack is None:
            acc = event.get("accuracy")
            if isinstance(acc, int):
                self.attack = acc
        self.stat_asked = True
        if self.strength is not None and self.agility is not None:
            self.stat_known = True
        if self.attack is not None or self.ac is not None:
            self.stat_known = True

    def needs_stat(self) -> bool:
        """True once per gap, like `needs_exp`. Combat and pending block the send."""
        if self.hp is None:
            return False
        if self.stat_known or self.stat_asked:
            return False
        return True

    def combat_sheet(self, klass: str = "") -> combat.CombatSheet:
        return combat.sheet(
            klass=klass or self.klass or "",
            level=self.level,
            strength=self.strength,
            agility=self.agility,
            attack=self.attack,
            ac=self.ac,
            worn=self.worn,
        )

    def combat_label(self, klass: str = "", *, compact: bool = False) -> str:
        return self.combat_sheet(klass).label(compact=compact)

    def _note_exp(self, event: dict[str, object]) -> None:
        """Set from the `Exp:` status line. Replace current; never add a gain."""
        exp = event.get("exp")
        if not isinstance(exp, int):
            return
        self.exp = exp
        needed = event.get("needed")
        nxt = event.get("next")
        pct = event.get("pct")
        if isinstance(needed, int):
            self.exp_needed = needed
        if isinstance(nxt, int) and nxt > 0:
            self.exp_next = nxt
        if isinstance(pct, int):
            self.exp_pct = pct
        elif self.exp_next:
            self.exp_pct = min(999, (self.exp * 100) // self.exp_next)
        self.exp_known = True
        self.exp_stale = False
        self.exp_asked = True
        self.exp_gained = False

    def _gain_exp(self, amount: object) -> None:
        if not isinstance(amount, int) or amount <= 0:
            if self.exp_known:
                self.exp_stale = True
                self.exp_asked = False
            return
        if not self.exp_known or self.exp is None:
            self.exp_stale = True
            self.exp_asked = False
            return
        self.exp += amount
        if self.exp_needed is not None:
            self.exp_needed = max(0, self.exp_needed - amount)
        if self.exp_next and self.exp_next > 0:
            self.exp_pct = min(999, (self.exp * 100) // self.exp_next)
        self.exp_stale = False
        self.exp_gained = True

    def _note_max(self, mx: object) -> None:
        """Hits: cur/max from `health`/`stat` is this toon's pool.

        Prompt [HP=n] is current only. Do not keep a high-water from
        creation Health or another roll — starting HP follows the sheet.
        """
        if isinstance(mx, int) and mx > 0:
            self.max_hp = mx
            self.max_hp_known = True
            return
        if self.max_hp_known:
            return
        if self.hp is not None:
            self.max_hp = max(self.max_hp or 0, self.hp)

    def _note_ma(self, ma: object, mx: object) -> None:
        if isinstance(ma, int):
            self.ma = ma
            # Prompt [HP=n/MA=n] is current only. Never stamp max from that.
            if self.max_ma is not None and ma > self.max_ma:
                self.max_ma = ma
        if isinstance(mx, int) and mx > 0:
            self.max_ma = mx

    def note_dealt(self, dmg: int, now: float | None = None) -> None:
        """Count outgoing damage for the hunt-loop DPS aggregate."""
        if dmg <= 0:
            return
        when = time.monotonic() if now is None else now
        if self._dealt and when - self._dealt[-1][0] > SESSION_IDLE:
            self._dealt.clear()
        self._dealt.append((when, int(dmg)))
        self._trim_dealt(when)

    def _reset_dealt(self) -> None:
        self._dealt.clear()

    def _trim_dealt(self, now: float) -> None:
        cut = now - SESSION_KEEP
        while self._dealt and self._dealt[0][0] < cut:
            self._dealt.popleft()
        if self._dealt and now - self._dealt[-1][0] > SESSION_IDLE:
            self._dealt.clear()

    def _window_damage(self, now: float, span: float) -> int:
        cut = now - span
        return sum(dmg for at, dmg in self._dealt if at >= cut)

    def _first_in_window(self, now: float, span: float) -> float | None:
        cut = now - span
        for at, _dmg in self._dealt:
            if at >= cut:
                return at
        return None

    def _per_round(self, dmg: int, start: float, now: float) -> int | None:
        """Damage per combat-round equivalent over [start, now]."""
        if dmg <= 0:
            return None
        elapsed = now - start
        if elapsed < HIT_WINDOW:
            elapsed = HIT_WINDOW
        return max(1, round(dmg * HIT_WINDOW / elapsed))

    def dps(self, now: float | None = None) -> int | None:
        """Short-window loop rate, or None if this stretch is idle."""
        when = time.monotonic() if now is None else now
        self._trim_dealt(when)
        dmg = self._window_damage(when, SHORT_WINDOW)
        first = self._first_in_window(when, SHORT_WINDOW)
        if dmg <= 0 or first is None:
            return None
        return self._per_round(dmg, first, when)

    def dps_long(self, now: float | None = None) -> int | None:
        """Precise hunt-loop rate after enough wall time, else None."""
        when = time.monotonic() if now is None else now
        self._trim_dealt(when)
        if not self._dealt:
            return None
        first = self._dealt[0][0]
        if when - first < TREND_WARMUP:
            return None
        total = sum(dmg for _at, dmg in self._dealt)
        return self._per_round(total, first, when)

    def dps_trend(self, now: float | None = None) -> str | None:
        """ahead / behind / even versus the long loop norm."""
        when = time.monotonic() if now is None else now
        short = self.dps(when)
        long = self.dps_long(when)
        if short is None or long is None or long <= 0:
            return None
        ratio = short / long
        if ratio >= 1 + TREND_BAND:
            return "ahead"
        if ratio <= 1 - TREND_BAND:
            return "behind"
        return "even"

    def dps_label(self, now: float | None = None) -> str:
        when = time.monotonic() if now is None else now
        short = self.dps(when)
        long = self.dps_long(when)
        trend = self.dps_trend(when)
        if short is None and long is None:
            return "DPS —"
        if short is None:
            return f"DPS {long}"
        if long is None or trend is None:
            return f"DPS {short}"
        mark = {"ahead": "↑", "behind": "↓", "even": "·"}[trend]
        return f"DPS {long}{mark}{short}"

    def stats_label(self, now: float | None = None) -> str:
        """DPS, then EXP, then HP/MA — chrome right-aligns the vitals."""
        parts = [self.dps_label(now), self.exp_label(), self.hp_label()]
        return "   ".join(part for part in parts if part)

    def hp_label(self) -> str:
        """Footer vitals: current/max when a max is known. Prompt MA is current only."""
        hits = pool_label("HP", self.hp, self.max_hp)
        if not hits or self.ma is None:
            return hits
        ma = pool_label("MA", self.ma, self.max_ma)
        return f"{hits}  {ma}" if ma else hits

    def _note_hits(self) -> None:
        if self.hp is None:
            return
        if self.hp >= 1:
            self.mortal = False
            self.bleeding = False
            return
        if self.aided:
            return
        # Halls / Temple Healer reprint HP=0. That is a ghost, not a
        # mortal on the grass — `goto` must still be able to walk out.
        if paths.in_afterlife(self.room):
            self.mortal = False
            self.bleeding = False
            return
        self.mortal = True
        self.bleeding = True

    def hp_ratio(self) -> float | None:
        if self.hp is None or not self.max_hp:
            return None
        if self.hp < 0:
            return 0.0
        return max(0.0, min(1.0, self.hp / self.max_hp))

    def hp_meter_ratio(self) -> float | None:
        """Footer bar. Until `health`/`stat` Hits, don't use prompt high-water."""
        if self.hp is None:
            return None
        if self.hp < 0:
            return 0.0
        if not self.max_hp_known:
            return 1.0 if self.hp > 0 else 0.0
        return self.hp_ratio()

    def ma_ratio(self) -> float | None:
        if self.ma is None or not self.max_ma:
            return None
        return self.ma / self.max_ma


def _looks_like_place(title: str) -> bool:
    low = title.lower()
    return any(
        word in low
        for word in (
            "newhaven",
            "silvermere",
            "arena",
            "shop",
            "armour",
            "armor",
            "skali",
            "sentara",
            "helfgrim",
            "road",
            "path",
            "entrance",
            "guild",
            "healer",
            "square",
            "store",
            "temple",
            "sewer",
            "fountain",
            "passage",
            "hall",
            "cave",
            "alley",
            "graveyard",
            "street",
            "bank",
            "godfrey",
        )
    )
