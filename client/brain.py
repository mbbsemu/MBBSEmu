"""Play MajorMUD: gear up, hunt lops, rest. Localhost only."""

from __future__ import annotations

import time

from . import gear, megapath, modules, parse, party, paths, realm_map, runs, spells
from .compass import Compass, is_move
from .map import Map, at_goal, landmark_tag, list_landmarks, parse_landmark, pinned_dir
from .modules.party import (
    HEAL_ASK,
    HEALED_SAY,
    HEAL_RATIO,
    INVITE_RETRY,
    JOIN_CALL,
    REST_CALL,
    RESTED_SAY,
    PartySession,
)
from .state import WorldState

REST_RATIO = 0.60
# Ninja following the leader: sit-down rest under 75%.
PARTY_REST_RATIO = 0.75
# Walk out of the fight before rest. Below this, do not keep swinging.
FLEE_RATIO = 0.40
REST_ABS = 12
# Self-heal and party `!heal` (spoken — not the `health` command) at this
# ratio or below. Sit-down rest stays on REST_RATIO (or PARTY_REST_RATIO
# while a ninja is following).
# Recast ally bless before the typical fade so ninja crits keep luck.
BLESS_KEEP = 210.0
# Last-ditch harm on a living target already in the fight. Trash is never a boss.
HARM_DESPERATE = 0.30
ALLY_HP_ASSUME = 28
# Room-tick look gap lives in modules.realm — keep brain thin.
LOOK_GAP = modules.LOOK_GAP
PANIC_HOLD = 2.5
# `Attempting to sneak...` is not ready. `d` on that prompt breaks sneak.
# A couple of seconds is enough — not a full combat round.
SNEAK_SETTLE = 2.0


def _trusted_live(state: WorldState) -> str | None:
    """Listed lops are live. A look in flight must not hide them."""
    return modules.farm_here(state.mobs)


def _farm_species(state: WorldState) -> set[str]:
    """Unique farm swing names currently listed."""
    return modules.farm_species(state.mobs)


def _is_trash(name: str) -> bool:
    """Newhaven / arena fodder: rats, bugs, worms, slimes, kobolds, …"""
    return modules.is_trash(name)


def _still_here(state: WorldState, name: str) -> str | None:
    """Return the swing name if that farm mob is still listed here."""
    return modules.still_here(state.mobs, name)


class Brain:
    def __init__(
        self,
        allowed: bool,
        rest_ratio: float = REST_RATIO,
        pvp: bool = False,
        me: str = "",
        alts: str = "",
        party_leader: str = "",
        rank: str = "",
        klass: str = "",
        race: str = "",
        spell_list: object = None,
        ambush: str = "stand",
        stealth: str = "",
        atlas: realm_map.Atlas | None = None,
        auto_join: bool = True,
        aa: bool | None = None,
        learned_path: str | None = None,
        gear_path: str | None = None,
        hunt: str = "",
    ) -> None:
        self.allowed = allowed
        self.rest_ratio = rest_ratio
        self.pvp = pvp
        self.me = me.strip().lower()
        self._aka = {
            part
            for part in f"{me} {alts}".replace(",", " ").split()
            if part
        }
        self.leader = party_leader.strip()
        self.rank = rank.strip().lower()
        self.auto_join = bool(auto_join)
        self._joined = False
        self._followed = False
        self._ranked = False
        self._party_rank = ""
        self._rank_sent = ""
        self._party_mates: list[tuple[str, str]] = []
        self._invited = False
        self._got_invite = False
        self._want_join_call = False
        self._party_at = 0.0
        self._follow_sent_to = ""
        self._invite_at: dict[str, float] = {}
        self._invite_skip: set[str] = set()
        self._want_invite_all = False
        self.bail = ""
        self.mode = "manual"
        self.next_action = "manual"
        self.goto_goal = ""
        self.goto_skip = False
        self.deathpile = ""
        self._recover_pending = False
        self._pile_grabbed = False
        self._pile_grabs = 0
        self._need_kit_check = False
        self._last_live_room = ""
        self._dead = False
        self.gear_done = False
        self._skip_sell: set[str] = set()
        self._await_inv = False
        self._pry_sent = False
        self._inv_seen = 0
        self._inv_after = 0
        self._i_prompt = 0
        self._selling = ""
        self._looked = False
        self._armour_i = 0
        self._wearing = False
        self._weapon_bought = False
        self._weapon_worn = False
        self._torch_bought = False
        self._tried_torch = False
        # Estimated torches remaining in the bag. We maintain this across
        # `light torch` / `buy torch` actions because the game only updates
        # inventory for us when we explicitly request it.
        # Keep TORCH_BAG_MIN (lit run + spare to exit/restock) before diving.
        self._torch_est: int = 0
        self._torch_wait_inv: bool = False
        self._torch_inv_after: int = 0
        self._torch_i_prompt: int = 0
        # Only re-read bag count when `inv_seq` advances past this.
        self._torch_inv_synced: int = -1
        # One `i` per surface visit at the manhole — never spam.
        self._torch_checked: bool = False
        # Non-ninja must pass a surface stock check before each manhole drop.
        self._sewer_stocked: bool = False
        # Sticky until torches are bought — do not let farm yank west mid-run.
        self._torch_shopping: bool = False
        # Bag has ≥2 torches; still need a lit one at TS before diving.
        self._torch_bag_ready: bool = False
        self._torch_lit: bool = False
        # Mid-block Silver Street tiles after Western End (2 = Giovanni's door).
        self._store_silver_east: int = 0
        self._store_expect_silver: bool = False
        # Counted Pier → TS walk (3s 6e 10s). 0 = not mid-script, or sitting on Pier.
        self._skiff_ts: int = 0
        self._default_leader: str = self.leader
        self.hunt_run = runs.parse(hunt) or runs.DEFAULT
        self._sewer_path = megapath.load_sewer_path(
            self.hunt_run.tape or megapath.DEFAULT_SEWER
        )
        self._sewer_i = 0
        self._grave_i = 0
        self._wait_prompt = 0
        self._sent_at = 0.0
        self._room_pulse_seq = -1
        self._room_pulse_at = 0.0
        self._bank_inv = False
        self._bank_sent = False
        self._bank_done = False
        self._bank_prompt = 0
        self._attacking = ""
        self._last_verb = ""
        self._last_aim = ""
        self._in_camp = False
        self._sitting = False
        self._want_look = False
        self._room_soft_tries = 0
        self._asked_health = True
        self._asked_spells = False
        self._spellbook_seen = False
        self._need_learn_check = False
        self._spells_at_prompt = 0
        self._spellbook_seq = 0
        self._spell_dumps = 0
        self._realm_maxes = False
        self._last_step = ""
        self._step_room = ""
        self._step_prompt = 0
        self._blocked_dirs: set[str] = set()
        self.klass = klass.strip().lower()
        self.race = modules.normalize_race(race)
        if not paths.needs_padded(self.klass):
            self._armour_i = len(paths.ARMOUR_ITEMS)
        if not modules.needs_weapon(self.klass):
            self._weapon_bought = True
            self._weapon_worn = True
        if not modules.needs_torch(self.race, self.klass):
            self._torch_bought = True
        # Open the next lop unless this is a ninja (hunt still swings / backstabs).
        # Mystic punches — `aa` is the armed bash wire form, not fists.
        self.aa = modules.uses_bash_aa(self.klass, aa)
        self._spells = spells.known_spells(self.klass, spell_list)
        self._learn = spells.shop_spells(self.klass, spell_list)
        self._learned_path = learned_path
        self._memorized = spells.load_learned(learned_path, self.me)
        if modules.known_room_light(self.klass, self._memorized):
            self._torch_bought = True
        self._last_read = ""
        self._spell_offer = ""
        self._spell_offer_at = 0
        self._want_spell = ""
        self._declined_at: dict[str, int] = {}
        self._gear_path = gear_path
        self._pile_path = gear.deathpile_path(gear_path)
        self._claimed = gear.load_claimed(gear_path, self.me)
        saved_pile = gear.load_deathpile(self._pile_path, self.me)
        if saved_pile:
            self.deathpile = saved_pile
        self._gear_offer = ""
        self._gear_offer_key = ""
        self._gear_offer_at = 0
        self._want_gear = ""
        self._gear_declined_at: dict[str, int] = {}
        self._seen_level: int | None = None
        self._spell_i = 0
        while self._spell_i < len(self._learn) and self._learn[self._spell_i] in self._memorized:
            self._spell_i += 1
        self._spells_shopped = self._spell_i >= len(self._learn)
        self._spell_buying = False
        self._spell_reading = False
        self._spell_alt = False
        self._scrolls_tried: set[str] = set()
        self._last_cast = ""
        self._cast_at = 0.0
        # Bless is owned at buy; do not recast until level changes after a refuse.
        self._bless_hold: int | None = None
        self._bless_until: dict[str, float] = {}
        self._sneaking = False
        self._sneak_wait = False
        self._sneak_armed = False
        self._sneak_flop = False
        self._sneak_block = False
        self._sneak_denied = ""
        self._sneak_busy = False
        self._sneak_ready_at = 0.0
        self._ambush_boot = False
        self._boot_looked = False
        self._boot_asked = False
        self._boot_plan: list[str] = []
        self._ambush_out = False
        self._pit_fight = False
        self._drop_scan = False
        self._evaded = False
        self._need_break = False
        self._need_swing = False
        mode = ambush.strip().lower()
        self.ambush = "inout" if mode == "inout" else "stand"
        want = (stealth or "").strip().lower()
        if not want:
            if mode in {"always", "sneak", "on", "stand", "inout"}:
                want = "always"
            elif mode in {"walk", "regular", "off"}:
                want = "walk"
            else:
                want = "auto"
        if want in {"always", "sneak", "on"}:
            self.stealth = "always"
        elif want in {"walk", "regular", "off"}:
            self.stealth = "walk"
        else:
            self.stealth = "auto"
        self._atlas = atlas if atlas is not None else realm_map.Atlas()
        self.world = Map(self._atlas)
        self.compass = Compass()
        self._hidden = False
        self._pending_step = ""
        self._panic_until = 0.0
        self._rescue = ""
        self._rescue_who = ""
        self._drag_on = False
        self._recovering = False
        self._want_train = False
        self._train_hold = False
        self._asked_heal = False
        self._asked_heal_hp: int | None = None
        self._party_rest = False
        self._park_rest = False
        self._party_heal = False
        self._rest_shouted = False
        self._rested_shouted = False
        self._healed_shouted = False
        self._rested_acks: dict[str, str] = {}
        self._healed_acks: dict[str, str] = {}
        self._rest_wait: set[str] = set()
        self._heal_wait: set[str] = set()
        self._crew = PartySession(identity=self.me, klass=self.klass, race=self.race)
        # Overnight cleanup resume — re-follow / re-invite after reconnect.
        self._resume_follow = ""
        self._resume_invite: list[str] = []
        self._resume_rank = ""

    def _clear_rescue(self) -> None:
        self._rescue = ""
        self._rescue_who = ""
        self._drag_on = False
        self._recovering = False

    def toggle_hunt(self) -> None:
        if not self.allowed:
            self.next_action = "hunter stays on localhost"
            return
        # Gear is "hunt on" for the footer; modules.kit decides promote vs cancel.
        if self.mode == "gear":
            action = modules.gear_f7_action(
                mode=self.mode,
                gear_done=self.gear_done,
                kit_ready=self._kit_ready(),
                weapon_worn=self._weapon_worn,
                armour_done=self._armour_i >= len(paths.ARMOUR_ITEMS),
                needs_torch=modules.needs_torch(self.race, self.klass),
            )
            if action == "promote":
                self._done_gear()
                return
            if action == "arm_torch":
                self._torch_bought = True
                self._done_gear()
                return
        if self.mode in ("hunt", "gear", "rest", "goto"):
            self.mode = "manual"
            self.next_action = "manual"
            self.goto_goal = ""
            self.goto_skip = False
            self._want_join_call = False
            self._attacking = ""
            self._last_verb = ""
            self._last_aim = ""
            self._sitting = False
            self._want_look = False
            self._sneaking = False
            self._sneak_wait = False
            self._sneak_armed = False
            self._sneak_flop = False
            self._sneak_block = False
            self._sneak_denied = ""
            self._sneak_busy = False
            self._sneak_ready_at = 0.0
            self._clear_ambush_boot()
            self._ambush_out = False
            self._pit_fight = False
            self._drop_scan = False
            self._evaded = False
            self._need_break = False
            self._need_swing = False
            self._hidden = False
            self._pending_step = ""
            self.compass.reset()
            self._panic_until = 0.0
            self._clear_rescue()
            return
        self._start_hunt()
        # Keep follow/rank — F7 while following should swing, not re-party.

    def set_hunt_run(self, name: str) -> runs.HuntRun | None:
        """Pick the grind. Does not toggle hunt on or off."""
        got = runs.parse(name)
        if got is None:
            return None
        self.hunt_run = got
        self._sewer_path = megapath.load_sewer_path(
            got.tape or megapath.DEFAULT_SEWER
        )
        self._sewer_i = 0
        self._grave_i = 0
        self.next_action = f"hunt {got.id}"
        return got

    def list_hunt_runs(self) -> str:
        return runs.list_runs()

    def list_gotos(self) -> str:
        return list_landmarks()

    def start_goto(self, name: str, *, skip: bool = False) -> str | None:
        """Walk to a landmark, then stop. `skip` is run — no swings on the way."""
        if not self.allowed:
            self.next_action = "hunter stays on localhost"
            return None
        raw = (name or "").strip().lower().replace("_", "-").replace(" ", "-")
        if raw in {"pile", "death", "deathpile", "body", "corpse"}:
            if not self.deathpile:
                self.next_action = "no deathpile"
                return None
            goal = "_pile"
            skip = True
        else:
            goal = parse_landmark(name)
            if not goal:
                return None
        if self.mode in ("hunt", "gear", "rest"):
            self.toggle_hunt()
        if goal != "_pile":
            self._recover_pending = False
        if goal != "restpark":
            self._clear_party_rest()
        self._bank_inv = False
        self._bank_sent = False
        self._bank_done = False
        self._bank_prompt = 0
        if goal == "bank":
            self._pry_sent = False
            self._await_inv = False
        self.goto_goal = goal
        self.goto_skip = bool(skip)
        self.mode = "goto"
        word = "run" if self.goto_skip else "goto"
        self.next_action = f"{word} {landmark_tag(goal)}"
        self._asked_health = False
        self._panic_until = 0.0
        self._want_train = False
        self._train_hold = False
        self._wait_prompt = 0
        self._last_step = ""
        self.compass.reset()
        return goal

    def start_run(self, name: str) -> str | None:
        return self.start_goto(name, skip=True)

    def _hunt_goal(self) -> str:
        return self.hunt_run.approach if self.hunt_run else "farm"

    def _pit_grind(self) -> bool:
        """Newhaven arena hunt: stay in the pit. Never the healer or GY."""
        return bool(self.hunt_run) and self.hunt_run.loop == "pit"

    def _farm_nav(self, state: WorldState) -> tuple[int | None, bool]:
        """Level/gate for farm steps. Arena hunt stays local even at 4+."""
        if self._pit_grind():
            level = state.level
            if level is None or level >= 4:
                level = 3
            return level, False
        return state.level, state.arena_gated

    def _nav_goal(self) -> str:
        if self._torch_shopping:
            return "store"
        if self.mode == "goto" and self.goto_goal and self.goto_goal != "_pile":
            return self.goto_goal
        return self._hunt_goal()

    def _goto_word(self) -> str:
        return "run" if self.goto_skip else "goto"

    def _at_deathpile(self, room: str) -> bool:
        if not self.deathpile or not (room or "").strip():
            return False
        return realm_map.room_key(room) == realm_map.room_key(self.deathpile)

    def _note_death(self, state: WorldState) -> None:
        if not state.just_died:
            return
        room = (state.room or "").strip()
        if paths.in_afterlife(room):
            room = self._last_live_room
        if room and not paths.in_afterlife(room):
            self.deathpile = room
            self._pile_grabbed = False
            self._pile_grabs = 0
            self._recover_pending = True
            self.gear_done = False
            gear.save_deathpile(self._pile_path, self.me, room)
        state.just_died = False
        state.geared = False
        state.worn = []
        state.inventory = []
        state.extras = []
        self._attacking = ""
        self._need_swing = False
        self._dead = True
        self._drop_follow(state)
        self.compass.reset()
        self._wait_prompt = 0
        self._last_step = ""
        # FSD often still shows the kill tile after the teleport. Treat
        # location as unknown until Halls / Temple Healer reprints.
        if not paths.in_afterlife((state.room or "").strip()):
            state.room = ""
            state.exits = []
            state.scanned = False
        where = self.deathpile or "pile"
        self.next_action = f"dead @ {where}"

    def _forget_deathpile(self) -> None:
        self.deathpile = ""
        self._recover_pending = False
        self._pile_grabbed = False
        self._pile_grabs = 0
        gear.save_deathpile(self._pile_path, self.me, "")

    def _snatching_pile(self, state: WorldState | None = None) -> bool:
        if self.goto_goal == "_pile" or self._recover_pending:
            return True
        return bool(state and self._at_deathpile(state.room))

    def _pile_corpse(self, state: WorldState) -> bool:
        listed = " ".join((*state.mobs, *state.things)).lower()
        return "corpse" in listed

    def _maybe_recover_pile(self, state: WorldState) -> bool:
        if not self._recover_pending or not self.deathpile:
            return False
        if self.mode == "goto" and self.goto_goal and self.goto_goal != "_pile":
            return False
        if state.hp is None or state.hp <= 0:
            return False
        if state.mortal:
            return False
        self._recover_pending = False
        return self.start_goto("pile", skip=True) is not None

    def _loot_deathpile(self, state: WorldState, send) -> bool:
        """`get all` first — snatch the corpse before coins or a look."""
        if self._pile_grabs < 2:
            self._pile_grabs += 1
            self._pile_grabbed = True
            self._cmd(send, "get all", state)
            self.next_action = "get pile"
            return True
        if self._pile_corpse(state) and self._pile_grabs < 3:
            self._pile_grabs += 1
            self._cmd(send, "get all corpse", state)
            self.next_action = "get pile"
            return True
        self._forget_deathpile()
        self.mode = "gear"
        self.goto_goal = ""
        self.goto_skip = False
        self._need_kit_check = True
        self._await_inv = True
        self._pry_sent = False
        self._looked = True
        return self._ask_inv(send, state)

    def _goto_tick(self, state: WorldState, send) -> None:
        """Walk to `goto_goal`, then stop. Fight lops unless this is `run`."""
        goal = self.goto_goal
        tag = landmark_tag(goal)
        word = self._goto_word()
        if not goal:
            self.mode = "manual"
            self.next_action = "manual"
            return
        ghost = self._dead or paths.in_afterlife(state.room)
        living = state.hp is None or state.hp > 0
        if goal == "_pile":
            if not self.deathpile:
                self.mode = "manual"
                self.goto_goal = ""
                self.goto_skip = False
                self.next_action = "no deathpile"
                return
            if (
                self._at_deathpile(state.room)
                and not ghost
                and living
            ):
                if self._loot_deathpile(state, send):
                    return
                tag = landmark_tag(self.goto_goal)
                word = self._goto_word()
        elif (
            not ghost
            and living
            and at_goal(
                state.room,
                goal,
                level=state.level,
                gated=state.arena_gated,
                klass=self.klass,
            )
        ):
            if goal == "restpark":
                self._arrive_rest_park(state, send)
                return
            if goal == "bank":
                if self._arrive_bank(state, send):
                    return
                self.mode = "manual"
                self.goto_goal = ""
                self.goto_skip = False
                self.next_action = f"at {tag}"
                return
            self.mode = "manual"
            self.goto_goal = ""
            self.goto_skip = False
            self.next_action = f"at {tag}"
            return
        if self._followed or state.following:
            self._cmd(send, "leave", state)
            self._drop_follow(state)
            self.next_action = f"{word} {tag}"
            return
        self._resolve_fight(state)
        live = self._aim(state) or modules.farm_here(state.mobs)
        if live and not self.goto_skip:
            if self._engage_lop(state, send, live):
                return
            self.next_action = f"{word} {tag} (fight)"
            return
        if (
            not self.goto_skip
            and (state.in_combat or self._attacking)
        ):
            self.next_action = f"{word} {tag} (fight)"
            return
        if (
            (state.mortal or state.bleeding)
            and not ghost
            and living
        ):
            self.next_action = f"{word} {tag}"
            return
        if self._down(state):
            self._sitting = False
            self._cmd(send, "break", state)
            return
        if goal == "sewer" and self._stock_torch_before_sewer(state, send):
            return
        # New tile after a move still carries the old room title's exits until
        # Obvious exits reprints. Look first — never walk Entry's stale `sw`
        # on the creek bridge (wall spam).
        if not state.scanned:
            if not state.look_scan:
                self._ask_look(send, state)
            else:
                self.next_action = f"{word} {tag}"
            return
        if goal == "_pile":
            step = self.world.walk_to(
                self.deathpile,
                state.room,
                state.exits,
                last_step=self._last_step,
                level=state.level,
                gated=state.arena_gated,
            )
        else:
            step = self.world.step(
                goal,
                state.room,
                state.exits,
                last_step=self._last_step,
                silver_east=self._store_silver_east,
                skiff_i=self._skiff_ts,
                level=state.level,
                gated=state.arena_gated,
                closed=state.closed_exits,
                klass=self.klass,
            )
        if step and self._go(send, step, state, sneak=False):
            self.next_action = f"{word} {tag}"
            return
        if not state.look_scan:
            self._ask_look(send, state)
            return
        self.next_action = f"{word} {tag}"

    def _start_hunt(self) -> None:
        """Turn hunt on. Never toggles off — join/backrank must not go manual."""
        if not self.allowed:
            self.next_action = "hunter stays on localhost"
            return
        if self.mode in ("hunt", "gear"):
            return
        self.goto_goal = ""
        # Follow starts the combat loop only after kit. A naked Ryan /
        # Robald who auto-joins Matt must still shop, then the arena.
        # Dressed klymacks (`gear_done`) hunt with the leader.
        self.goto_skip = False
        self.mode = "gear" if not self.gear_done else "hunt"
        self.next_action = self.mode
        self._asked_health = False
        self._panic_until = 0.0
        self._want_train = False
        self._train_hold = False
        if self.mode == "gear":
            self._need_kit_check = True
            self._await_inv = True
            self._pry_sent = False
            self._wait_prompt = 0
            self._last_step = ""
            self.compass.reset()
            self._looked = True
            self.next_action = "i"
        elif self._stealth_moves() and not self._followed:
            self._begin_ambush_boot()
        if self._named_leader():
            self._want_join_call = True

    def _maybe_start_kit(self, state: WorldState, send) -> bool:
        """Starter gold is not a kit. New toons shop without waiting for F7."""
        if self.gear_done or self.mode != "manual":
            return False
        if self._want_train or self._want_spell or self._want_gear:
            return False
        if state.in_combat or self._rescue:
            return False
        if not modules.in_newhaven(state.room):
            return False
        if state.inv_seq <= 0:
            return False
        if not modules.is_naked(state.worn, state.inventory, state.extras):
            return False
        self._start_hunt()
        if self.mode != "gear":
            return False
        self._await_inv = False
        self._pry_sent = False
        self._gear(state, send)
        return True

    def open_gear_inv(self, state: WorldState) -> str | None:
        """F7 / hunt: send `i` now so gear does not wait for a refresh."""
        if self.mode != "gear" or self._pry_sent:
            return None
        if self._listed_fight(state) and paths.is_dangerous(state.room):
            self._done_gear()
            return None
        self._pry_sent = True
        self._await_inv = True
        self._inv_after = state.inv_seq
        self._i_prompt = state.prompt_seq
        self._wait_prompt = state.prompt_seq + 1
        self._sent_at = time.monotonic()
        self.next_action = "i"
        return "i"

    def takeover(self) -> None:
        self.mode = "manual"
        self.next_action = "manual"
        self._want_join_call = False
        self._attacking = ""
        self._last_verb = ""
        self._last_aim = ""
        self._want_look = False
        self._sitting = False
        self._sneaking = False
        self._sneak_wait = False
        self._sneak_armed = False
        self._sneak_flop = False
        self._sneak_block = False
        self._sneak_denied = ""
        self._sneak_busy = False
        self._sneak_ready_at = 0.0
        self._clear_ambush_boot()
        self._ambush_out = False
        self._pit_fight = False
        self._drop_scan = False
        self._evaded = False
        self._need_break = False
        self._need_swing = False
        self._hidden = False
        self._pending_step = ""
        self._panic_until = 0.0
        self._want_train = False
        self._train_hold = False
        self._want_spell = ""
        self._want_gear = ""
        self.goto_goal = ""
        self.goto_skip = False
        self._clear_rescue()
        # Esc / stop: drop in-flight party join so chrome is not stuck on
        # "next: follow Curtis" forever.
        self._forget_follow_sent()
        self.clear_resume_intent()
        self._got_invite = False
        self._clear_party_rest()
        self._park_rest = False
        self._recovering = False

    def hunting(self) -> bool:
        return self.mode != "manual"

    def apply_resume(self, snap: object) -> None:
        """Restore hunt/party intent after board cleanup reconnect."""
        from . import resume as resume_mod

        if isinstance(snap, resume_mod.ResumeState):
            resume_mod.apply_to_brain(self, snap)

    def clear_resume_intent(self) -> None:
        self._resume_follow = ""
        self._resume_invite = []
        self._resume_rank = ""

    def flush_resume(self, state: WorldState, send) -> bool:
        """One-shot: re-follow leader or re-invite followers after cleanup."""
        if not state.in_realm:
            return False
        follow = (self._resume_follow or "").strip()
        if follow and not self._named_leader():
            if state.following and self._same_toon(state.following, follow):
                self._resume_follow = ""
                self._followed = True
                self._joined = True
                if self._resume_rank and not self._ranked:
                    row = self._resume_rank
                    self._resume_rank = ""
                    cmd = party.RANK_CMD.get(row, "")
                    if cmd:
                        self._note_rank_sent(cmd, state)
                        self._cmd(send, cmd, state)
                        self.next_action = cmd
                        return True
                return False
            if self._follow_sent_to and self._same_toon(self._follow_sent_to, follow):
                # Do not park forever on "next: follow X" — retry after a gap.
                if time.monotonic() - self._party_at < INVITE_RETRY:
                    self.next_action = f"follow {follow}"
                    return False
                self._forget_follow_sent()
            named = self._toon_call_name(follow) or follow
            self._follow_sent_to = follow
            self._party_at = time.monotonic()
            self._cmd(send, f"follow {named}", state)
            self.next_action = f"follow {named}"
            return True
        invites = [n for n in self._resume_invite if str(n).strip()]
        if invites and (self._named_leader() or self._leading()):
            self._resume_invite = []
            for who in invites:
                self._send_invite(who, state, send)
            self._want_join_call = True
            self.next_action = "invite party"
            return True
        if self._resume_rank and (state.following or self._followed):
            row = self._resume_rank
            self._resume_rank = ""
            cmd = party.RANK_CMD.get(row, "")
            if cmd:
                self._note_rank_sent(cmd, state)
                self._cmd(send, cmd, state)
                self.next_action = cmd
                return True
        self._resume_follow = ""
        self._resume_invite = []
        return False

    def _me(self, name: str) -> bool:
        tokens = [w.strip(".,!;:").lower() for w in name.split() if w.strip(".,!;:")]
        mine = {part for part in self.me.replace(",", " ").split() if part}
        return any(token in mine for token in tokens)

    def _mine(self, name: str, state: WorldState | None = None) -> bool:
        extras = set(self._aka)
        if state:
            extras |= state.self_names
        return paths.is_self(name, extras)

    def _remember_self(self, state: WorldState) -> None:
        who = state.last_actor.strip()
        state.last_actor = ""
        if not who:
            return
        if not (self._attacking or self._sitting or state.resting or state.in_combat):
            return
        if paths.is_player(who) or paths.is_home_account(who):
            low = who.lower()
            self._aka.add(low)
            state.self_names.add(low)

    def _leading(self) -> bool:
        if self._followed:
            return False
        if not self.leader:
            return True
        mine = {part for part in self.me.replace(",", " ").split() if part}
        return self.leader.lower() in mine

    def _named_leader(self) -> bool:
        """Configured party_leader is this toon — invite the roster, never follow."""
        return bool(self.leader) and self._leading()

    def _following(self) -> bool:
        if self._followed:
            return True
        return bool(self.leader) and not self._leading()

    def _leader_down(self, state: WorldState) -> bool:
        """The person we follow is mortal, or we are mid-rescue — we may move."""
        follow = (state.following or self.leader or "").strip()
        if not follow:
            return not (self._followed or state.following)
        if self._rescue and self._same_toon(self._rescue_who, follow):
            return True
        who = state.ally_mortal.strip()
        return bool(who and self._same_toon(who, follow))

    def _with_leader(self, state: WorldState) -> bool:
        """Live follow: no own u/d/walk. Config party_leader is not required."""
        if not (self._followed or state.following):
            return False
        if self._leader_down(state):
            return False
        return True

    def _rank_self_name(self) -> str:
        parts = [p for p in self.me.replace(",", " ").split() if p]
        for part in reversed(parts):
            if part.strip(".,!;:").lower() not in {"sysop", "guest"}:
                return part
        return parts[0] if parts else ""

    def _note_party_mate(self, name: str, klass: str = "") -> None:
        """Remember a roster toon in this group. Rank is party-wide, not room-wide."""
        who = (name or "").strip()
        if not who or self._me(who):
            return
        job = (klass or party.roster_class(who) or "").strip().lower()
        if not job:
            return
        for i, (seen, old) in enumerate(self._party_mates):
            if self._same_toon(who, seen):
                if job and not old:
                    self._party_mates[i] = (seen, job)
                return
        self._party_mates.append((who, job))

    def _rank_members(self, state: WorldState) -> list[tuple[str, str]]:
        """This toon, sticky roster mates, and the person we follow.

        Also here blanks between rooms. Counting only who is listed this
        tick flips mystic (and thief) front/mid/back and spams `backr`.
        """
        found: list[tuple[str, str]] = [
            (self._rank_self_name() or self.me, self.klass)
        ]
        if not (self._followed or state.following):
            self._party_mates = []
            return found
        for who in self.room_players(state):
            self._note_party_mate(who, party.roster_class(who))
        lead = (state.following or self.leader or "").strip()
        if lead:
            self._note_party_mate(lead, party.roster_class(lead))
        found.extend(self._party_mates)
        return found

    def _party_facts(self, state: WorldState) -> modules.PartyFacts:
        ratio = None
        if state.max_hp and state.hp is not None:
            ratio = state.hp / state.max_hp
        return modules.PartyFacts(
            me=self.me,
            klass=self.klass,
            leader=self.leader,
            auto_join=self.auto_join,
            in_realm=state.in_realm,
            following=state.following,
            invited_by=state.invited_by,
            join_call_by=state.join_call_by,
            room_pcs=tuple(self.room_players(state)),
            room_scanned=bool(state.saw_here),
            follow_sent_to=self._follow_sent_to,
            rank_sent=self._rank_sent,
            party_rank=self._party_rank or state.party_rank,
            cfg_rank=self.rank,
            named_leader=self._named_leader(),
            followed=bool(self._followed or state.following),
            ranked=self._ranked,
            mode=self.mode,
            # Self tokens only — party alts stay out of invite self-checks.
            aka=tuple(
                part for part in self.me.replace(",", " ").split() if part
            ),
            area=self._crew.area,
            can_cast_heal=bool(self._spell("heal")),
            with_leader=self._with_leader(state),
            asked_heal=self._asked_heal,
            healed_shouted=self._healed_shouted,
            rested_up=self._rested_up(state),
            hurt_ally=bool(self._hurt_ally(state)),
            hp_ratio=ratio,
            needs_heal=self._needs_heal(state),
            session_key=self._crew.key(),
        )

    def _desired_rank(self, state: WorldState) -> str:
        return party.desired_rank(self.klass, self.me, self.rank)

    def _rank_cmd(self, state: WorldState) -> str:
        """`frontr` / `midr` / `backr` when this follower is in the wrong row."""
        if self._named_leader() or self._leading():
            return ""
        offer = modules.rank_action(self._party_facts(state))
        return offer.command if offer else ""

    def _reset_party_rank(self) -> None:
        self._ranked = False
        self._party_rank = ""
        self._rank_sent = ""
        self._party_mates = []

    def _note_rank_sent(self, cmd: str, state: WorldState | None = None) -> None:
        row = {v: k for k, v in party.RANK_CMD.items()}.get(cmd, "")
        if row:
            self._party_rank = row
            if state is not None:
                # Keep WorldState on the row we just commanded. Otherwise
                # `_sync_party` restores the last printed rank (often back)
                # and the next tick / maybe_auto_party spams `midr`.
                state.party_rank = row
                state.backrank = row == "back"
        self._rank_sent = cmd
        self._ranked = True
        self._party_at = time.monotonic()

    def _ninja_party(self, state: WorldState) -> bool:
        """Ninja following the leader: follow, backrank, rest/heal, sn until hidden, bs."""
        return self._ninja() and self._with_leader(state)

    def _stealthed(self) -> bool:
        return bool(self._hidden or self._sneaking)

    def _own_ambush(self, state: WorldState) -> bool:
        """Solo sneak-and-move. Gear walks in the open. Party sneak is `_ninja_party`."""
        if self.mode == "gear":
            return False
        return self._stealth_moves() and not self._with_leader(state)

    def _pvp_sense(self, state: WorldState) -> bool:
        if not state.friendly_fire:
            return False
        self.bail = f"hit {modules.attack_name(state.friendly_fire)}"
        self.takeover()
        self.next_action = "logoff"
        return True

    def _ninja(self) -> bool:
        return modules.is_ninja(self.klass)

    def _paladin(self) -> bool:
        return modules.is_paladin(self.klass)

    def _stealth_moves(self) -> bool:
        if not self._ninja():
            return False
        if self.stealth == "always":
            return True
        if self.stealth == "walk":
            return False
        return not self._followed

    def toggle_stealth(self) -> str:
        if not self._ninja():
            self.next_action = "ambush off"
            return "walk"
        self.stealth = "walk" if self._stealth_moves() else "always"
        self.next_action = f"ambush {self.stealth}"
        return self.stealth

    def _clear_ambush_boot(self) -> None:
        self._ambush_boot = False
        self._boot_looked = False
        self._boot_asked = False
        self._boot_plan = []

    def _begin_ambush_boot(self) -> None:
        """F7 hunt / F8 ambush-on: look before sn, bs, attack, or leaving."""
        self._ambush_boot = True
        self._boot_looked = False
        self._boot_asked = False
        self._boot_plan = []
        self._need_break = False
        self._sneak_wait = False
        self._sneak_armed = False
        self._sneak_flop = False
        self._sneak_block = False
        self._sneak_denied = ""
        self._sneak_busy = False
        self._sneak_ready_at = 0.0

    def _queue_boot_look(self, send, state: WorldState) -> None:
        if self._boot_asked:
            return
        self._boot_asked = True
        state.look_scan = True
        state.saw_here = False
        state.saw_see = False
        self._cmd(send, "look", state)

    def on_ambush_on(self, state: WorldState) -> list[str]:
        """Commands to pace when F8 turns ambush on. No takeover.

        Own ambush: `look` first unless Also here already listed the room.
        A listed lop is a fight — do not leave to sneak. Empty room: one
        `sn`. Following Matt: `sn` only on an empty room; do not walk off
        with u/s.
        """
        cmds: list[str] = []

        def collect(text: str) -> None:
            cmds.append(text)

        if not self._ninja() or not state.in_realm:
            return cmds
        if self._with_leader(state):
            if not self._ninja_party(state) or state.in_combat or self._lops_here(state):
                return cmds
            if self._down(state):
                self._sitting = False
                self._cmd(collect, "break", state)
                return cmds
            if self._send_sneak(collect, state):
                self._sneak_wait = True
            return cmds
        if not self._stealth_moves():
            return cmds
        self._begin_ambush_boot()
        self._queue_boot_look(collect, state)
        return cmds

    def _run_ambush_boot(self, state: WorldState, send) -> bool:
        """Own ambush start: look if the listing is stale, else sn or fight."""
        if not self._ambush_boot:
            return False
        if not self._ninja() or not self._stealth_moves() or self._with_leader(state):
            self._clear_ambush_boot()
            return False
        if self._boot_plan:
            return self._boot_next(state, send)
        if self._lops_here(state) and (state.saw_here or self._boot_looked):
            self._clear_ambush_boot()
            return False
        if not self._boot_looked:
            if state.saw_here:
                self._boot_looked = True
            elif self._boot_asked:
                if state.look_scan:
                    self.next_action = "looking"
                    return True
                self._boot_looked = True
            else:
                self._queue_boot_look(send, state)
                return True
        if self._stealthed():
            self._clear_ambush_boot()
            return False
        if self._lops_here(state):
            self._clear_ambush_boot()
            return False
        if self._down(state):
            self._sitting = False
            self._cmd(send, "break", state)
            self._boot_plan = ["sn"]
            return True
        if self._send_sneak(send, state):
            self._sneak_wait = True
        self._clear_ambush_boot()
        return True

    def _boot_next(self, state: WorldState, send) -> bool:
        if not self._boot_plan:
            self._clear_ambush_boot()
            return False
        cmd = self._boot_plan.pop(0)
        if cmd == "break":
            self._sitting = False
            self._attacking = ""
            state.in_combat = False
            self._need_break = False
            self._cmd(send, "break", state)
            return True
        if cmd == "sn":
            if self._down(state):
                self._boot_plan.insert(0, "sn")
                self._sitting = False
                self._cmd(send, "break", state)
                return True
            if self._send_sneak(send, state):
                self._sneak_wait = True
            self._clear_ambush_boot()
            return True
        if cmd == "u":
            self._cmd(send, "u", state)
            self._need_break = False
            return True
        if cmd == "s":
            self._cmd(send, "s", state)
            self._need_break = False
            self._clear_ambush_boot()
            return True
        self._cmd(send, cmd, state)
        if not self._boot_plan:
            self._clear_ambush_boot()
        return True

    def stealth_label(self) -> str:
        if not self._ninja():
            return ""
        return "ambush" if self._stealth_moves() else "walk"

    def aa_label(self) -> str:
        return "aa" if self.aa else "aa off"

    def f8_label(self) -> str:
        """F8 word: ninja ambush/walk, else aa / aa off."""
        if self._ninja():
            return self.stealth_label()
        return self.aa_label()

    def toggle_aa(self, state: WorldState | None = None) -> str | None:
        """Flip paladin auto-bash. Returns `break` when stopping a live fight."""
        if self.aa:
            return self.stop_aa(state)
        self.aa = True
        self.next_action = "aa"
        return None

    def stop_aa(self, state: WorldState | None = None) -> str | None:
        """Stop paladin auto-bash. Server keeps swinging until `break`."""
        self.aa = False
        self.next_action = "aa off"
        fighting = bool(self._attacking) or (
            state is not None and state.in_combat
        )
        self._attacking = ""
        self._need_swing = False
        if not fighting:
            return None
        self._need_break = False
        return "break"

    def _punches(self) -> bool:
        """No weapon hand — MajorMUD `att` is a punch. Mystic still fists after a staff."""
        return modules.punches(self.klass)

    def _opens_swing(self, state: WorldState) -> bool:
        """Start or continue a swing. Ninja and mystic always; paladin follows `aa`."""
        return modules.opens_swing(self.klass, aa=self.aa)

    def toggle_auto_join(self) -> bool:
        """Flip invite auto-join. No takeover — works while hunting or manual."""
        self.auto_join = not self.auto_join
        self.next_action = "join" if self.auto_join else "join off"
        return self.auto_join

    def join_label(self) -> str:
        return "join" if self.auto_join else "join off"

    def _same_toon(self, left: str, right: str) -> bool:
        return party.same_toon(left, right)

    def _given_case(self, name: str) -> str:
        return party.given_case(name)

    def _toon_call_name(self, who: str) -> str:
        """In-game given name: Matthew, not the BBS login Matt."""
        return party.call_name(
            who, me=self.me, leader=self.leader, aka=tuple(self._aka)
        )

    def _roster_invite(self, who: str, state: WorldState) -> bool:
        """True if this inviter is the configured leader or another roster toon."""
        if not who or self._me(who):
            return False
        if self.leader and party.same_toon(self.leader, who):
            return True
        return party.on_roster(who)

    def _follow_in_flight(self, who: str = "") -> bool:
        """True after we already sent `follow` and the mud has not confirmed."""
        sent = (self._follow_sent_to or "").strip()
        if not sent:
            return False
        if not who:
            return True
        return self._same_toon(sent, who)

    def _note_follow_sent(self, who: str) -> None:
        self._follow_sent_to = who.strip()
        self._got_invite = True
        self._party_at = time.monotonic()

    def _forget_follow_sent(self) -> None:
        self._follow_sent_to = ""

    def _refresh_follow_sent(self, state: WorldState) -> None:
        """Drop a dead `follow` so we can send again (or stop waiting)."""
        who = (self._follow_sent_to or "").strip()
        if not who:
            return
        facts = self._party_facts(state)
        here = modules.party.inviter_here(
            who, facts.room_pcs, scanned=facts.room_scanned
        )
        status = modules.follow_sent_status(
            follow_sent_to=who,
            following=state.following or "",
            room_scanned=facts.room_scanned,
            inviter_present=here,
            sent_at=self._party_at,
            now=time.monotonic(),
            retry_after=INVITE_RETRY,
        )
        if status != "keep":
            self._forget_follow_sent()

    def on_invite(self, state: WorldState, send) -> bool:
        """Join a roster invite when F9 join is on. The named leader never follows."""
        if not self.allowed:
            return False
        if self._rescue or state.ally_mortal:
            return False
        self._sync_party(state)
        offer = modules.join_action(self._party_facts(state))
        if not offer:
            return False
        self._note_follow_sent(offer.name)
        self._cmd(send, offer.command, state)
        return True

    def _join_call_follow(self, who: str, state: WorldState) -> bool:
        """Hear `!join` from a roster toon — follow that speaker, not a hardcoded Matt."""
        if not who or self._me(who):
            return False
        return self._roster_invite(who, state)

    def on_join_call(self, state: WorldState, send) -> bool:
        """F9 join on: hear the leader's `!join` → follow that speaker (no shout)."""
        if not self.allowed or not state.in_realm:
            return False
        if self._rescue or state.ally_mortal:
            return False
        who = state.join_call_by.strip()
        # Leader's own echo / named leader never follows a rally shout.
        if self._named_leader() or self._me(who):
            state.join_call_by = ""
            return False
        self._sync_party(state)
        if state.following and who and self._same_toon(state.following, who):
            state.join_call_by = ""
            return self.on_follow(state, send)
        offer = modules.join_action(self._party_facts(state))
        state.join_call_by = ""
        if not offer:
            return False
        self._note_follow_sent(offer.name)
        self._cmd(send, offer.command, state)
        return True

    def on_follow(self, state: WorldState, send) -> bool:
        """backrank after following, including from manual. Then hunt with the party."""
        if not self.allowed or not state.in_realm:
            return False
        if self.mode == "goto":
            return False
        self._clear_party_rest()
        self._sync_party(state)
        if not (state.following or self._followed):
            return False
        sent = False
        cmd = self._rank_cmd(state)
        if cmd:
            self._note_rank_sent(cmd, state)
            self._cmd(send, cmd, state)
            sent = True
        self._maybe_party_hunt(state)
        return sent

    def _maybe_party_hunt(self, state: WorldState) -> None:
        """After follow+backrank onto a roster toon, hunt without F7.

        F9 join-off does not start hunt. A random PC follow does not.
        The named leader never hunts from a follow. Hunt already on stays on.
        """
        if not self.auto_join:
            return
        if self.mode == "goto":
            return
        if self._named_leader():
            return
        who = (state.following or "").strip()
        if who and not self._roster_invite(who, state):
            return
        if not (state.following or self._followed):
            return
        if self._rank_cmd(state):
            return
        self._start_hunt()

    def _tag_key(self, who: str, state: WorldState) -> str:
        extras = self._aka | state.self_names
        return (paths.party_name(who, extras) or who).strip().lower()

    def _note_wait_name(self, waiting: set[str], who: str, state: WorldState) -> None:
        key = self._tag_key(who, state)
        if key and not self._me(who):
            waiting.add(key)

    def _acked(self, who: str, acks: dict[str, str], state: WorldState) -> bool:
        key = self._tag_key(who, state)
        if not key:
            return False
        if key in acks:
            return True
        return any(self._same_toon(who, seen) for seen in acks.values())

    def _party_tag_ok(self, who: str, state: WorldState) -> bool:
        if not who or self._me(who):
            return False
        if self._roster_invite(who, state):
            return True
        if state.following and self._same_toon(who, state.following):
            return True
        if any(self._same_toon(who, name) for name in state.followers):
            return True
        return any(self._same_toon(who, name) for name in self._alts_here(state))

    def _clear_party_rest(self) -> None:
        self._party_rest = False
        self._park_rest = False
        self._rest_shouted = False
        self._rested_shouted = False
        self._rested_acks.clear()
        self._rest_wait.clear()

    def _clear_party_heal(self) -> None:
        self._party_heal = False
        self._healed_shouted = False
        self._healed_acks.clear()
        self._heal_wait.clear()

    def start_party_rest(
        self, state: WorldState | None = None, *, shouted: bool = False
    ) -> str | None:
        """Leave, walk to the creek bridge SW of the GY gate, sit, then `!rested`."""
        self._clear_party_heal()
        self._rest_shouted = bool(shouted) or self._rest_shouted
        self._rested_shouted = False
        got = self.start_goto("rest", skip=True)
        if got is None:
            return None
        self._party_rest = True
        self._park_rest = True
        if state is not None:
            for who in (*state.followers, *self._alts_here(state)):
                self._note_wait_name(self._rest_wait, who, state)
        return got

    def start_party_heal(self, *, shouted: bool = False) -> None:
        """Hold the run, heal, then `!healed` so the leader can walk again."""
        self._party_heal = True
        self._healed_shouted = False

    def rest_call_cmds(self, state: WorldState) -> list[str]:
        if not state.in_realm:
            self.next_action = "rest in the realm"
            return []
        self.start_party_rest(state, shouted=True)
        for who in (*state.followers, *self._alts_here(state)):
            self._note_wait_name(self._rest_wait, who, state)
        self.next_action = REST_CALL
        return [REST_CALL]

    def heal_call_cmds(self, state: WorldState) -> list[str]:
        if not state.in_realm:
            self.next_action = "heal in the realm"
            return []
        self.start_party_heal(shouted=True)
        self._asked_heal = True
        self._asked_heal_hp = state.hp
        self.next_action = HEAL_ASK
        return [HEAL_ASK]

    def _note_party_tags(self, state: WorldState) -> None:
        who = state.rest_call_by.strip()
        state.rest_call_by = ""
        if who and self._party_tag_ok(who, state):
            self._note_wait_name(self._rest_wait, who, state)
            if not self._party_rest:
                self.start_party_rest(state, shouted=not self._named_leader())
        for name in list(state.rested_acks.values()):
            self._rested_acks[self._tag_key(name, state)] = name
        for name in list(state.healed_acks.values()):
            key = self._tag_key(name, state)
            self._healed_acks[key] = name
        for name in list(state.heal_asks.values()):
            if self._me(name):
                continue
            if not self._party_tag_ok(name, state) and not self._mine(name, state):
                continue
            self._note_wait_name(self._heal_wait, name, state)
            if not self._party_heal:
                self._healed_shouted = False
            self._party_heal = True
            if (
                not self._party_rest
                and self._park_rest_room(state)
                and not self._can_cast_heal(state)
            ):
                # Followers wait for the leader's !heal / !rest. A peer (or
                # self-echo) !heal must not peel them to the bridge alone.
                if self._followed or state.following:
                    lead = (state.following or self.leader or "").strip()
                    if not (lead and self._same_toon(name, lead)):
                        continue
                self.start_party_rest(state, shouted=True)

    def _party_rest_wait_names(self, state: WorldState) -> list[str]:
        found: list[str] = []
        seen: set[str] = set()
        for who in (*state.followers, *(n for n, _ in self._party_mates)):
            if not who or self._me(who):
                continue
            key = self._tag_key(who, state)
            if not key or key in seen:
                continue
            seen.add(key)
            found.append(who)
        for key in self._rest_wait:
            if key in seen or self._me(key):
                continue
            seen.add(key)
            found.append(key)
        return found

    def _party_rested_ready(self, state: WorldState) -> bool:
        waiting = self._party_rest_wait_names(state)
        if not waiting:
            return True
        acks = {
            **self._rested_acks,
            **state.rested_acks,
            **self._healed_acks,
            **state.healed_acks,
        }
        return all(self._acked(who, acks, state) for who in waiting)

    def _heal_wait_pending(self, state: WorldState) -> bool:
        acks = {**self._healed_acks, **state.healed_acks}
        for key in self._heal_wait:
            if self._me(key):
                continue
            if key not in acks and not any(
                self._same_toon(key, seen) for seen in acks.values()
            ):
                return True
        return bool(state.heal_asks)

    def _holding_party_heal(self, state: WorldState) -> bool:
        """Hold GY/sewer walking until the party is healed. Keep swinging."""
        if self._lops_here(state) or state.in_combat:
            return False
        if self._needs_heal(state):
            return bool(self._asked_heal or self._party_heal)
        if not self._party_heal:
            return False
        if self._heal_wait_pending(state):
            return True
        self._clear_party_heal()
        return False

    def _try_ack_healed(self, state: WorldState, send) -> bool:
        offer = modules.heal_ack_action(self._party_facts(state))
        if not offer:
            return False
        self._healed_shouted = True
        self._clear_heal_ask()
        self._cmd(send, offer.command, state)
        return True

    def _arrive_rest_park(self, state: WorldState, send) -> None:
        self.goto_goal = ""
        self.goto_skip = False
        self._park_rest = True
        self.mode = "rest"
        if self._party_rest and not self._rest_shouted:
            self._rest_shouted = True
            self._cmd(send, HEAL_ASK, state)
            self.next_action = HEAL_ASK
            return
        self.next_action = "rest"
        self._rest(state, send)

    def _arrive_bank(self, state: WorldState, send) -> bool:
        """Deposit the purse at Godfrey. True while still talking to the teller."""
        if self._bank_done:
            return False
        if not self._bank_inv and not self._bank_sent:
            state.deposited = False
            state.bank_fail = False
        if state.bank_fail:
            state.bank_fail = False
            self._bank_done = True
            return False
        if self._bank_sent:
            if state.deposited:
                state.deposited = False
                self._bank_done = True
                return False
            if state.wealth_copper == 0:
                self._bank_done = True
                return False
            if state.prompt_seq > self._bank_prompt:
                self._bank_done = True
                return False
            self.next_action = "deposit"
            return True
        if not self._bank_inv:
            if self._ask_inv(send, state):
                self.next_action = "bank wealth"
                return True
            self._bank_inv = True
        copper = state.wealth_copper
        if copper is None:
            copper = paths.purse_copper(state.inventory)
        if not copper:
            self._bank_done = True
            return False
        self._cmd(send, f"deposit {copper}", state)
        self._bank_sent = True
        self._bank_prompt = state.prompt_seq
        self.next_action = "deposit"
        return True

    def _party_rest_tick(self, state: WorldState, send) -> None:
        if not self._rested_shouted:
            self._rested_shouted = True
            self._healed_shouted = True
            self._cmd(send, HEALED_SAY, state)
            self.next_action = "wait rest"
            return
        if self._named_leader() or self._leading():
            if not self._party_rested_ready(state):
                self.next_action = "wait rest"
                return
            self._finish_party_rest(state, send)
            return
        self.next_action = "wait rest"

    def _finish_party_rest(self, state: WorldState, send) -> None:
        sitting = self._sitting or state.resting
        self._clear_party_rest()
        self.mode = "hunt" if self.gear_done else "gear"
        self.next_action = self.mode
        if self._named_leader():
            self._want_join_call = True
        if sitting:
            self._sitting = False
            state.resting = False
            self._cmd(send, "break", state)
            self.next_action = "standing"

    def _in_party(self, state: WorldState) -> bool:
        """Grouped with someone — do not unfollow or pit-flee away from them."""
        if state.following or self._followed:
            return True
        if self.leader and self._leading() and state.followers:
            return True
        return self._party_pending(state)

    def panic(self, state: WorldState, send) -> None:
        """Break autocombat and reset the heal lock. Stay in hunt.

        Matt down: start rescue (leave / drag / aid), then solo. Do not
        hold the hunter — after aid we hunt on our own until a real invite.
        Matt up and partied: break only — no `u`, no unfollow. A short hold
        stops an immediate re-aggro. Solo in the pit may `u` to the road.
        """
        self._attacking = ""
        self._sneak_wait = False
        self._sneak_armed = False
        self._sneaking = False
        self._sneak_flop = False
        self._sneak_block = False
        self._sneak_denied = ""
        self._sneak_busy = False
        self._sneak_ready_at = 0.0
        self._ambush_out = False
        self._pit_fight = False
        self._drop_scan = False
        self._evaded = False
        self._need_break = False
        self._sitting = False
        state.in_combat = False
        self._last_cast = ""
        self._cast_at = 0.0
        self._panic_until = time.monotonic() + PANIC_HOLD
        self.next_action = "panic"
        self._want_train = False
        self._train_hold = False
        self._cmd(send, "break", state)
        if self._arm_leader_rescue(state) or self._rescue:
            self._panic_until = 0.0
            self.next_action = "rescue"
            return
        if self._in_party(state):
            return
        if self._in_pit(state):
            self._cmd(send, "u", state)

    def _inout(self) -> bool:
        return self._ninja() and self.ambush == "inout"

    def _note_sneak(self, state: WorldState) -> None:
        """Fail wins the payload. 1.11p is quiet on hide — no fail is success.

        `You don't think you're sneaking` / sound / not hidden → flop.
        `You may not sneak right now` → block `sn` on this tile. One `break`
        if needed, then walk — never retry `sn` until the room changes.
        `Attempting` is not ready — wait SNEAK_SETTLE, then assume hidden.
        `Sneaking...` confirms immediately. try+fail together still fails.
        """
        failed = state.sneak_fail
        confirmed = state.sneak_ok
        tried = state.sneak_try
        busy = state.sneak_busy
        state.sneak_fail = False
        state.sneak_ok = False
        state.sneak_try = False
        state.sneak_busy = False
        if state.in_combat and not self._attacking:
            self._sneaking = False
            self._sneak_armed = False
            self._sneak_wait = False
            self._sneak_ready_at = 0.0
            self._hidden = False
        if failed:
            self._sneaking = False
            self._sneak_armed = False
            self._sneak_wait = False
            self._sneak_ready_at = 0.0
            self._hidden = False
            if busy:
                self._need_break = True
                self._sneak_block = True
                self._sneak_busy = True
                self._sneak_denied = realm_map.room_key(state.room) or "*"
                self._attacking = ""
                self._sneak_flop = False
            else:
                self._sneak_flop = True
                self._sneak_block = False
            return
        if confirmed:
            self._sneaking = True
            self._sneak_armed = False
            self._sneak_wait = False
            self._sneak_ready_at = 0.0
            self._sneak_flop = False
            self._sneak_block = False
            self._hidden = True
            return
        if tried:
            self._sneak_wait = False
            self._sneak_flop = False
            self._sneak_armed = True
            self._sneak_ready_at = time.monotonic() + SNEAK_SETTLE
            self._sneaking = False
        self._assume_sneak()

    def _assume_sneak(self) -> None:
        """Armed and settled with no fail line — treat as hidden. Do not wait forever."""
        if self._hidden or self._sneaking or self._sneak_flop:
            return
        if not self._sneak_armed or not self._sneak_settled():
            return
        self._sneaking = True
        self._hidden = True
        self._sneak_armed = False
        self._sneak_wait = False
        self._sneak_ready_at = 0.0

    def _swing_name(self, name: str) -> str:
        """Species swing name — module attack_name, nothing else."""
        return modules.attack_name(name)

    def _swing_verb(self, state: WorldState) -> str:
        """Hidden ninja `bs`. Armed non-ninja hunt is `aa`. Mystic punches `att`."""
        if self._ninja() and self._stealthed():
            return "bs"
        if self._punches():
            return "att"
        if self.aa and self.klass and not self._ninja():
            return "aa"
        return "att"

    def _strike(self, send, state: WorldState, aim: str) -> bool:
        aim = self._swing_name(aim)
        if not aim:
            return False
        if not _still_here(state, aim):
            return False
        self._attacking = aim.lower()
        self._need_swing = False
        state.in_combat = True
        self._pit_fight = True
        verb = self._swing_verb(state)
        if verb == "bs":
            self._sneaking = False
            self._sneak_armed = False
            self._sneak_wait = False
            self._hidden = False
            if self._inout():
                self._ambush_out = True
        self._sneak_wait = False
        self._cmd(send, f"{verb} {aim}", state)
        return True

    def _spell(self, kind: str) -> str:
        return spells.of_kind(self._spells, kind)

    def _knows(self, name: str) -> bool:
        return name.strip().lower() in self._memorized

    def _remember(self, name: str) -> None:
        low = name.strip().lower()
        if not low or low in self._memorized:
            return
        self._memorized.add(low)
        spells.save_learned(self._learned_path, self.me, self._memorized)
        if modules.known_room_light(self.klass, self._memorized):
            self._torch_bought = True

    def _have_mana(self, cost: int, state: WorldState) -> bool:
        if state.ma is None:
            return True
        return state.ma >= cost

    def _note_cast_fail(self, state: WorldState) -> None:
        reason = state.cast_fail
        state.cast_fail = ""
        buff = self._last_cast.startswith("buff:")
        if buff and state.spell_skip:
            state.spell_skip = False
            if not reason:
                reason = "level"
        if not reason:
            return
        bits = [part for part in self._last_cast.split(":") if part]
        tried = bits[1] if len(bits) > 1 else ""
        who = bits[2] if len(bits) > 2 else ""
        # Can't-cast-yet is not "don't have it." Keep bless (and other buffs).
        if reason == "unknown" and tried and not buff:
            self._spells = [name for name in self._spells if name != tried]
            self._memorized.discard(tried)
            spells.save_learned(self._learned_path, self.me, self._memorized)
        if reason == "mana":
            state.ma = 0
        if buff:
            if who:
                state.forget_ally_bless(who)
                self._bless_until.pop(who.lower(), None)
            else:
                state.blessed = False
                self._bless_until.pop("self", None)
            if reason in {"level", "unknown", "fail"}:
                self._bless_hold = state.level if state.level is not None else 1
        self._last_cast = ""
        self._cast_at = 0.0

    def _spell_ready(self) -> bool:
        if not self._cast_at:
            return True
        return time.monotonic() - self._cast_at >= spells.ROUND

    def _heal_name(self, seen: str, state: WorldState) -> str:
        extras = self._aka | state.self_names
        aim = paths.party_name(seen, extras) or modules.attack_name(seen)
        if not aim:
            return ""
        first = aim.split()[0]
        if paths.is_dir_token(first) or modules.farm_here([aim]):
            return ""
        if not (
            paths.is_given_name(aim, extras)
            or paths.is_player(aim)
            or paths.is_home_account(aim)
        ):
            return ""
        if paths.is_home_account(aim):
            return aim.lower()
        return aim

    def _heal_allies_here(self, state: WorldState) -> list[str]:
        extras = self._aka | state.self_names
        found: list[str] = []
        seen_low: set[str] = set()
        for name in (
            *self._alts_here(state),
            *state.ally_hurt.values(),
            *state.heal_asks.values(),
        ):
            if not name or not self._mine(name, state) or self._me(name):
                continue
            key = (paths.party_name(name, extras) or name).lower()
            if key in seen_low:
                continue
            seen_low.add(key)
            found.append(name)
        return found

    def _ally_heal_gate(self) -> int:
        """Damage that puts assumed ally HP at or below HEAL_RATIO. Backup to `!heal`."""
        return max(1, ALLY_HP_ASSUME - int(ALLY_HP_ASSUME * HEAL_RATIO))

    def _ally_asked(self, state: WorldState, name: str) -> bool:
        extras = self._aka | state.self_names
        key = (paths.party_name(name, extras) or name).lower()
        return key in state.heal_asks or name.lower() in state.heal_asks

    def _ally_needs_heal(self, state: WorldState, name: str) -> bool:
        if self._ally_asked(state, name):
            return True
        extras = self._aka | state.self_names
        key = (paths.party_name(name, extras) or name).lower()
        taken = max(state.ally_taken(key), state.ally_taken(name))
        return taken >= self._ally_heal_gate()

    def _hurt_ally(self, state: WorldState) -> str | None:
        extras = self._aka | state.self_names
        dead = state.last_kill.lower()
        for name in self._heal_allies_here(state):
            key = (paths.party_name(name, extras) or name).lower()
            if dead and (key in dead or name.lower() in dead):
                continue
            asked = self._ally_asked(state, name)
            hurt = key in state.ally_hurt or name.lower() in state.ally_hurt
            if not asked and not hurt:
                continue
            if self._ally_needs_heal(state, name):
                return name
        return None

    def _try_ally_heal(self, state: WorldState, send, spell: str) -> bool:
        if spells.self_only(spell):
            return False
        seen = self._hurt_ally(state)
        if not seen:
            return False
        target = self._heal_name(seen, state)
        if not target:
            return False
        cost = spells.cost(spell)
        reserve = cost if self._needs_heal(state) else 0
        if not self._have_mana(cost + reserve, state):
            return False
        extras = self._aka | state.self_names
        key = (paths.party_name(seen, extras) or seen).lower()
        state.forget_ally(key, seen, target)
        self._last_cast = f"heal:{spell}:{target}"
        self._cast_at = time.monotonic()
        self._cmd(send, spells.command(spell, target), state)
        self._remember(spell)
        return True

    def _clear_heal_ask(self) -> None:
        self._asked_heal = False
        self._asked_heal_hp = None

    def _heal_ask_cleared(self, state: WorldState) -> None:
        """Keep the one `!heal` until we are actually full. No re-shout on a bump."""
        if not self._asked_heal:
            return
        if self._rested_up(state):
            return

    def _can_cast_heal(self, state: WorldState) -> bool:
        name = self._spell("heal")
        if not name:
            return False
        if state.level is not None and not spells.can_cast(name, state.level):
            return False
        return self._have_mana(spells.cost(name), state)

    def _park_rest_room(self, state: WorldState) -> bool:
        """GY / shack / crypt / sewer — walk to the bridge instead of sitting here."""
        return modules.park_rest_room(state.room, pit=self._pit_grind())

    def _try_ask_heal(self, state: WorldState, send) -> bool:
        """Follower with no heal spell: speak `!heal` once at HEAL_RATIO or below."""
        if self._spell("heal"):
            return False
        if not self._with_leader(state):
            self._clear_heal_ask()
            return False
        if self._party_rest:
            return False
        self._heal_ask_cleared(state)
        offer = modules.heal_ask_action(self._party_facts(state))
        if not offer:
            return False
        self._asked_heal = True
        self._asked_heal_hp = state.hp
        self._party_heal = True
        self._healed_shouted = False
        self._note_wait_name(self._heal_wait, self._rank_self_name() or self.me, state)
        self._cmd(send, offer.command, state)
        return True

    def _try_heal(self, state: WorldState, send) -> bool:
        name = self._spell("heal")
        if not name:
            if self._try_ask_heal(state, send):
                return True
            return self._try_ack_healed(state, send)
        if state.level is not None and not spells.can_cast(name, state.level):
            return self._try_ack_healed(state, send)
        if not self._spell_ready():
            return self._try_ack_healed(state, send)
        if self._needs_heal(state):
            if not self._have_mana(spells.cost(name), state):
                return self._try_ack_healed(state, send)
            self._last_cast = f"heal:{name}"
            self._cast_at = time.monotonic()
            self._cmd(send, spells.command(name), state)
            self._remember(name)
            return True
        if self._try_ally_heal(state, send, name):
            return True
        return self._try_ack_healed(state, send)

    def _spell_is_weapon(self) -> bool:
        """Mage / warlock damage is the harm spell, not a desperation poke."""
        return modules.spell_is_weapon(self.klass)

    def _harm_reason(self, state: WorldState, target: str) -> bool:
        """Boss kill-shot, or desperation. Casters use it as their attack."""
        if not paths.is_living(target):
            return False
        if self._spell_is_weapon():
            return True
        if not _is_trash(target):
            return True
        ratio = state.hp_ratio()
        return ratio is not None and ratio < HARM_DESPERATE

    def _try_harm(self, state: WorldState, send, aim: str) -> bool:
        """Paladin: kill shot only. Mage: magic missile once the swing is open."""
        name = self._spell("harm")
        if not name or not aim:
            return False
        if self._opening(state):
            return False
        target = aim if paths.is_living(aim) else ""
        if not target:
            return False
        if not self._attacking or not paths.same_mob(self._attacking, target):
            return False
        if not self._harm_reason(state, target):
            return False
        if not self._spell_ready():
            return False
        heal = self._spell("heal")
        reserve = 2 * spells.cost(heal) if heal else 0
        if not self._have_mana(spells.cost(name) + reserve, state):
            return False
        key = f"harm:{target.lower()}"
        if self._last_cast == key:
            return False
        self._last_cast = key
        self._cast_at = time.monotonic()
        self._cmd(send, spells.command(name, target), state)
        self._remember(name)
        return True

    def _bless_key(self, name: str) -> str:
        extras = self._aka
        return (paths.party_name(name, extras) or name).strip().lower()

    def _bless_allies_here(self, state: WorldState) -> list[str]:
        """Party here, ninja (crit luck) first — class/role, not a toon name."""
        found = list(self._alts_here(state))
        found.sort(key=lambda n: (modules.bless_priority(n), n.lower()))
        return found

    def _ally_still_blessed(self, state: WorldState, name: str) -> bool:
        key = self._bless_key(name)
        until = self._bless_until.get(key, 0.0)
        if until and time.monotonic() > until:
            state.forget_ally_bless(name, key)
            self._bless_until.pop(key, None)
            return False
        return state.ally_has_bless(name, key)

    def _next_bless_target(self, state: WorldState) -> str | None:
        """Ninja alts first for crit luck, then self. None = all set."""
        for seen in self._bless_allies_here(state):
            if not self._ally_still_blessed(state, seen):
                return seen
        if state.blessed:
            return None
        return ""

    def _try_bless(self, state: WorldState, send) -> bool:
        """`cast bless [target]` when hunt is idle. Heal wins the round."""
        name = self._spell("buff")
        if not name or name != "bless":
            return False
        if state.in_combat or self._attacking:
            return False
        if _trusted_live(state) or modules.farm_here(state.mobs):
            return False
        if not self._in_pit(state):
            return False
        if self._want_look:
            return False
        if not spells.can_cast(name, state.level):
            return False
        if self._bless_hold is not None and (
            state.level is None or state.level <= self._bless_hold
        ):
            return False
        if not self._spell_ready():
            return False
        if state.ma is None:
            return False
        seen = self._next_bless_target(state)
        if seen is None:
            return False
        target = ""
        if seen:
            target = self._heal_name(seen, state)
            if not target:
                return False
        heal = self._spell("heal") or "minor healing"
        reserve = 2 * spells.cost(heal)
        if not self._have_mana(spells.cost(name) + reserve, state):
            return False
        if target:
            self._last_cast = f"buff:{name}:{target}"
            state.mark_ally_bless(target)
            self._bless_until[self._bless_key(target)] = time.monotonic() + BLESS_KEEP
        else:
            self._last_cast = f"buff:{name}"
            state.blessed = True
            self._bless_until["self"] = time.monotonic() + BLESS_KEEP
        self._cast_at = time.monotonic()
        self._cmd(send, spells.command(name, target), state)
        self._remember(name)
        return True

    def _try_read_scroll(self, state: WorldState, send) -> bool:
        """Read a scroll we are holding. Exact name — bless is not a guess."""
        if state.in_combat or self._attacking:
            return False
        if _trusted_live(state) or modules.farm_here(state.mobs):
            return False
        if not self._learn:
            return False
        held = list(state.inventory) + list(state.extras)
        name = spells.first_held_scroll(held, self._learn, self._scrolls_tried)
        if not name:
            return False
        if self._knows(name):
            self._scrolls_tried.add(name)
            return False
        self._last_read = name
        self._scrolls_tried.add(name)
        self._cmd(send, spells.read_command(name), state)
        self.next_action = "read"
        return True

    def _sync_maxes(self, state: WorldState) -> None:
        if state.trained:
            self._asked_health = False
            self._asked_spells = False
            self._spellbook_seen = False
            self._spell_dumps = 0
            state.trained = False

    def _need_maxes(self, state: WorldState) -> bool:
        """Ask `health` for missing max HP, and for max MA only if we have a pool."""
        if state.hp is None:
            return False
        if not state.max_hp_known:
            return True
        if self._ninja():
            return False
        return state.ma is not None and state.max_ma is None

    def _ask_health(self, state: WorldState, send) -> bool:
        """Send `health` once when max HP/MA are unknown. Again after train."""
        self._sync_maxes(state)
        if not self._need_maxes(state) or self._asked_health:
            return False
        if state.in_combat or self._attacking:
            return False
        if self.mode != "manual" and (self._aim(state) or modules.farm_here(state.mobs)):
            return False
        self._asked_health = True
        self._cmd(send, "health", state)
        return True

    def _ask_exp(self, state: WorldState, send) -> bool:
        """Send `exp` once until chrome has current/total/percent. Never twice."""
        if not state.needs_exp():
            return False
        if state.in_combat or self._attacking:
            return False
        if self.mode != "manual" and (self._aim(state) or modules.farm_here(state.mobs)):
            return False
        if self.next_action == "exp":
            return False
        state.exp_asked = True
        self._cmd(send, "exp", state)
        return True

    def _take_spellbook(self, state: WorldState) -> None:
        """Fold a `spells`/`powers` dump into the memorized set."""
        self._spellbook_seq = state.spellbook_seq
        for name in state.known_spells:
            self._remember(name)
        if self._spell_offer and self._knows(self._spell_offer):
            self._spell_offer = ""
        if self._want_spell and self._knows(self._want_spell):
            self._finish_spell_run()

    def _ask_spellbook(self, state: WorldState, send) -> bool:
        """Send `spells` (or `powers`) before a Rayth y/n. One dump per check."""
        if not spells.uses_book(self.klass):
            self._spellbook_seen = True
            return False
        if self._spellbook_seen:
            return False
        if not self._need_learn_check:
            return False
        if self._want_train or self._want_spell or self._want_gear:
            return False
        if state.in_combat or self._attacking:
            return False
        if self.mode != "manual" and (self._aim(state) or modules.farm_here(state.mobs)):
            return False
        if state.spellbook_seq > self._spellbook_seq:
            self._take_spellbook(state)
            self._spellbook_seen = True
            self._asked_spells = False
            return False
        if self._asked_spells:
            if state.prompt_seq <= self._spells_at_prompt:
                return False
            if self._spell_dumps >= 2:
                self._spellbook_seen = True
                self._asked_spells = False
                self._need_learn_check = False
                return False
            self._asked_spells = False
        self._asked_spells = True
        self._spell_dumps += 1
        self._spells_at_prompt = state.prompt_seq
        self._cmd(send, spells.list_command(self.klass), state)
        return True

    def train_holding(self) -> bool:
        """Brain paused at the trainer. Keyboard stays with the player."""
        return self._train_hold

    def begin_train_hold(self) -> None:
        """Stop hunt/sneak/aa/party/gear/heal/look. Do not type train or stats."""
        self.takeover()
        self._train_hold = True
        self.next_action = "train hold"

    def request_train(self, state: WorldState | None = None) -> None:
        """Walk to this class's trainer, then pause. Never types train or stats."""
        if state is not None and paths.is_trainer(state.room, self.klass):
            self.begin_train_hold()
            return
        self._want_train = True
        self._train_hold = False
        self.next_action = "train"

    def cancel_train(self) -> None:
        was_hold = self._train_hold
        self._want_train = False
        self._train_hold = False
        if was_hold:
            self.next_action = "manual"

    def spell_offer(self) -> str:
        """Pending y/n shop name, or empty."""
        return self._spell_offer

    def gear_offer(self) -> str:
        """Pending y/n class-weapon name, or empty."""
        return self._gear_offer

    def pending_offer(self) -> str:
        return self._gear_offer or self._spell_offer

    def offer_tip(self, level: int | None) -> str:
        """Footer y/n or the quest hint after yes. Empty when nothing is up."""
        if self._gear_offer and level is not None:
            return gear.offer_tip(level, self._gear_offer, self.klass)
        if self._spell_offer and level is not None:
            where = modules.learn_where(self._spell_offer)
            if where:
                return f"lvl {level} — get {self._spell_offer} at {where}?  y/n"
            return f"lvl {level} — get {self._spell_offer}?  y/n"
        if self._want_spell:
            return f"getting {self._want_spell}  y already  Esc/stop cancel"
        if self._want_gear:
            return gear.quest_tip(self.klass, self._want_gear)
        return ""

    def cancel_spell_run(self) -> None:
        self._want_spell = ""
        self._spell_offer = ""
        if self.next_action.startswith("get ") and not self._want_gear:
            self.next_action = "manual"

    def cancel_gear_run(self) -> None:
        self._want_gear = ""
        self._gear_offer = ""
        self._gear_offer_key = ""
        if self.next_action.startswith("get ") and not self._want_spell:
            self.next_action = "manual"

    def cancel_offer_run(self) -> None:
        self.cancel_spell_run()
        self.cancel_gear_run()

    def answer_offer(self, yes: bool, state: WorldState | None = None) -> str:
        if self._gear_offer:
            return self.answer_gear_offer(yes, state)
        if self._spell_offer:
            return self.answer_spell_offer(yes, state)
        return ""

    def answer_gear_offer(self, yes: bool, state: WorldState | None = None) -> str:
        name = self._gear_offer
        key = self._gear_offer_key or gear.CLASS_WEAPON_KEY
        if not name:
            return ""
        level = self._gear_offer_at or (state.level if state is not None else 0) or 0
        self._gear_offer = ""
        self._gear_offer_key = ""
        if not yes:
            self._want_gear = ""
            self._gear_declined_at[key] = int(level)
            self.next_action = "manual"
            return f"skip {name}"
        self._want_gear = name
        self.next_action = f"get {name}"
        return f"get {name}"

    def answer_spell_offer(self, yes: bool, state: WorldState | None = None) -> str:
        name = self._spell_offer
        if not name:
            return ""
        level = self._spell_offer_at or (state.level if state is not None else 0) or 0
        self._spell_offer = ""
        if not yes:
            self._want_spell = ""
            self._declined_at[name] = int(level)
            self.next_action = "manual"
            return f"skip {name}"
        self._want_spell = name
        self.next_action = f"get {name}"
        return f"get {name}"

    def _held_gear(self, state: WorldState) -> list[str]:
        return list(state.inventory) + list(state.extras) + list(state.worn)

    def _claim_if_held(self, state: WorldState) -> None:
        weapon = gear.class_weapon(self.klass)
        if not weapon or not gear.have_item(self._held_gear(state), weapon):
            return
        if gear.CLASS_WEAPON_KEY not in self._claimed:
            self._claimed.add(gear.CLASS_WEAPON_KEY)
            gear.save_claimed(self._gear_path, self.me, self._claimed)
        if self._gear_offer == weapon or self._want_gear == weapon:
            self.cancel_gear_run()

    def _offer_class_weapon(self, state: WorldState) -> bool:
        quest = gear.next_due(
            self.klass,
            state.level,
            self._held_gear(state),
            self._claimed,
        )
        if quest is None or state.level is None:
            return False
        declined = self._gear_declined_at.get(quest.key)
        if declined is not None and state.level <= declined:
            return False
        self._gear_offer = quest.name
        self._gear_offer_key = quest.key
        self._gear_offer_at = state.level
        return True

    def _player_facts(self, state: WorldState) -> modules.PlayerFacts:
        return modules.PlayerFacts(
            klass=self.klass,
            race=self.race,
            level=state.level,
            known=frozenset(self._memorized),
            dark=state.dark,
            lit=bool(self._torch_lit or state.torch_lit),
            willed=state.willed,
            blessed=state.blessed,
            in_combat=bool(state.in_combat or self._attacking),
        )

    def _refresh_spell_offer(self, state: WorldState) -> None:
        self._claim_if_held(state)
        if self._train_hold or self._spell_offer or self._want_spell:
            return
        if self._gear_offer or self._want_gear:
            return
        if state.level is None:
            return
        if state.in_combat or self._attacking or self._lops_here(state):
            return
        first = self._seen_level is None
        dinged = self._seen_level is not None and state.level > self._seen_level
        trained = bool(state.trained)
        self._seen_level = state.level
        if first or dinged or trained:
            self._need_learn_check = True
        if not self._need_learn_check:
            return
        if self._offer_class_weapon(state):
            self._need_learn_check = False
            return
        name = modules.next_learn(self._player_facts(state), self._learn)
        if spells.uses_book(self.klass) and not self._spellbook_seen:
            if not name:
                self._need_learn_check = False
            return
        self._need_learn_check = False
        if not name:
            return
        declined = self._declined_at.get(name)
        if declined is not None and state.level <= declined:
            return
        self._spell_offer = name
        self._spell_offer_at = state.level

    def _finish_spell_run(self) -> None:
        self._want_spell = ""
        self._spell_buying = False
        self._spell_reading = False
        self.mode = "manual"
        self.next_action = "manual"

    def _step_to_spell_shop(self, state: WorldState) -> str:
        step = paths.step_toward_spell_shop(state.room, state.exits)
        if step:
            return step
        for dest in paths.SPELL_SHOP_ROOMS:
            route = self._atlas.path(state.room, dest)
            if not route:
                continue
            nxt = route[0]
            if state.exits and nxt not in state.exits:
                continue
            return nxt
        return ""

    def _step_to_class_quest(self, state: WorldState) -> str:
        if paths.at_quest_stop(state.room):
            return ""
        for dest in gear.QUEST_ROOMS:
            route = self._atlas.path(state.room, dest)
            if not route:
                continue
            nxt = route[0]
            if (
                state.exits
                and nxt not in state.exits
                and not paths.is_special_step(nxt)
            ):
                continue
            return nxt
        return paths.step_toward_silvermere(state.room, state.exits) or ""

    def _go_get_gear(self, state: WorldState, send) -> bool:
        """Walk the skiff to Silvermere. Crypt stays on the keys."""
        name = self._want_gear
        if not name:
            return False
        if gear.have_item(self._held_gear(state), name):
            self._claim_if_held(state)
            self.mode = "manual"
            self.next_action = "manual"
            return True
        if state.in_combat or self._attacking or self._lops_here(state):
            self.next_action = f"get {name}"
            return False
        if state.mortal or state.bleeding:
            return False
        if self._with_leader(state):
            self.next_action = f"get {name} (follow)"
            return False
        if self._down(state):
            self._sitting = False
            self._cmd(send, "break", state)
            return True
        if paths.at_quest_stop(state.room):
            self.mode = "manual"
            self.next_action = "manual"
            return False
        step = self._step_to_class_quest(state)
        if not step:
            return False
        if self._go(send, step, state, sneak=False):
            self.next_action = f"get {name}"
            return True
        self.next_action = f"get {name}"
        return False

    def _go_get_spell(self, state: WorldState, send) -> bool:
        """Walk to Rayth, buy/read one scroll, then manual."""
        name = self._want_spell
        if not name:
            return False
        # Sewer loop at TS comes first — don't wander the temple district.
        if self._at_manhole_entry(state) and self._farm_step(state):
            return False
        if self._knows(name):
            self._finish_spell_run()
            return True
        ab = modules.get(name)
        if ab is not None and ab.level_up:
            # Unlocks on ding/train — never walk a shop or quest for it.
            self._finish_spell_run()
            return True
        if not modules.can_shop(name):
            self.next_action = f"get {name} (no mapped path)"
            return True
        if state.in_combat or self._attacking or self._lops_here(state):
            self.next_action = f"get {name}"
            return False
        if state.mortal or state.bleeding:
            return False
        if self._with_leader(state) and not paths.is_spell_shop(state.room):
            self.next_action = f"get {name} (follow)"
            return False
        if self._down(state):
            self._sitting = False
            self._cmd(send, "break", state)
            return True
        if paths.is_spell_shop(state.room):
            held = list(state.inventory) + list(state.extras)
            if spells.have_scroll(held, name):
                if not self._spell_reading:
                    self._last_read = name
                    self._cmd(send, spells.read_command(name), state)
                    self._spell_reading = True
                    self._scrolls_tried.add(name)
                    self.next_action = f"read {name}"
                    return True
                self._remember(name)
                self._finish_spell_run()
                return True
            if not self._spell_buying:
                self._cmd(send, f"buy {spells.buy_name(name)}", state)
                self._spell_buying = True
                self.next_action = f"buy {name}"
                return True
            if not self._spell_reading:
                self._last_read = name
                self._cmd(send, spells.read_command(name), state)
                self._spell_reading = True
                self._scrolls_tried.add(name)
                self.next_action = f"read {name}"
                return True
            self._remember(name)
            self._finish_spell_run()
            return True
        step = self._step_to_spell_shop(state)
        if not step:
            self.next_action = f"get {name}"
            return False
        if self._go(send, step, state, sneak=False):
            self.next_action = f"get {name}"
            return True
        self.next_action = f"get {name}"
        return False

    def _go_train(self, state: WorldState, send) -> bool:
        """Walk to the guild and wait. Leader owns movement. Do not spend points."""
        if self._train_hold:
            self.next_action = "train hold"
            return True
        if not self._want_train:
            return False
        if state.in_combat or self._attacking or self._lops_here(state):
            return False
        if state.mortal or state.bleeding:
            return False
        if paths.is_trainer(state.room, self.klass):
            if self._down(state):
                self._sitting = False
                self._cmd(send, "break", state)
                return True
            self.begin_train_hold()
            return True
        if self._with_leader(state):
            return False
        if self._down(state):
            self._sitting = False
            self._cmd(send, "break", state)
            return True
        step = self.world.step(
            "guild",
            state.room,
            state.exits,
            last_step=self._last_step,
            silver_east=self._store_silver_east,
            skiff_i=self._skiff_ts,
            level=state.level,
            gated=state.arena_gated,
            closed=state.closed_exits,
            klass=self.klass,
        )
        if not step:
            self.next_action = "train"
            return False
        if self._go(send, step, state, sneak=False):
            self.next_action = "train"
            return True
        self.next_action = "train"
        return False

    def tick(self, state: WorldState, send, pending: bool, cancel=None) -> None:
        if not self.allowed:
            return
        if not state.in_realm:
            if self._train_hold:
                self.cancel_train()
            return
        self._map_here(state)
        self._note_skiff_ts(state.room)
        self._clear_landed_step(state)
        self._note_death(state)
        self._note_party_tags(state)
        if self._dead:
            if state.hp is not None and state.hp > 0 and not state.mortal:
                self._dead = False
            elif self.mode != "goto":
                return
        if self._train_hold:
            if state.trained or not paths.is_trainer(state.room, self.klass):
                self.cancel_train()
                self._refresh_spell_offer(state)
            else:
                self.next_action = "train hold"
                return
        if not pending and self._flush_invite_all(state, send):
            return
        if not pending and self.flush_resume(state, send):
            return
        if not pending and not self._snatching_pile(state) and self._pick_coins(
            state, send
        ):
            return
        if not pending and self._maybe_recover_pile(state):
            self._goto_tick(state, send)
            return
        if self.mode == "goto":
            if pending:
                return
            if state.prompt_seq < self._wait_prompt:
                if time.monotonic() - self._sent_at < 6:
                    return
                self._wait_prompt = state.prompt_seq
            self._goto_tick(state, send)
            return
        if self.mode == "manual":
            if pending:
                return
            if not self._realm_maxes:
                self._asked_health = False
                self._realm_maxes = True
            self._refresh_spell_offer(state)
            # Invite before torch stock — don't walk off and leave the alt.
            if self._party(state, send):
                return
            if self._maybe_start_kit(state, send):
                return
            # Even while following in manual, treat the Town Square ->
            # manhole transition as a loop “stock check” step.
            if self._stock_torch_before_sewer(state, send):
                return
            if self._ask_spellbook(state, send):
                return
            self._refresh_spell_offer(state)
            if self._ask_health(state, send):
                return
            if self._run_ambush_boot(state, send):
                return
            if not self._torch_shopping and self._dump_extras(state, send):
                return
            if self._go_get_gear(state, send):
                return
            if self._go_get_spell(state, send):
                return
            if self._go_train(state, send):
                return
            if self._want_train and self._with_leader(state):
                self.next_action = "train (follow)"
                return
            self.next_action = "manual"
            return
        state.self_names |= self._aka
        self._remember_self(state)
        if self._pvp_sense(state):
            return
        self._note_room_pulse(state)
        self._note_cast_fail(state)
        if state.learned:
            name = self._last_read or (
                self._learn[self._spell_i] if self._spell_i < len(self._learn) else ""
            )
            if name:
                self._remember(name)
                if name == self._want_spell:
                    self._finish_spell_run()
            state.learned = False
        self._note_sneak(state)
        self._assume_sneak()
        self._note_torch_light(state)
        if state.saw_here or _trusted_live(state) or modules.farm_here(state.mobs):
            self._drop_scan = False
        # Kill / Off clear _last_aim. Keep the queued swing name so a late
        # `at giant rat` can be dropped after the corpse line.
        queued_aim = self._last_aim
        queued_verb = self._last_verb
        dead = state.last_kill
        self._resolve_fight(state)
        self._note_evasion(state)
        self._note_wound(state)
        if self.mode != "gear" and self._rescue_tick(state, send):
            return
        if self.mode != "gear" and self._combat_flee(state, send):
            return
        if self.mode != "gear" and self._party_cadence(state, send):
            return
        if self.mode != "gear" and self._party(state, send):
            return
        live_now = self._aim(state)
        aim_now = self._swing_name(live_now).lower() if live_now else ""
        engaged = bool(self._attacking or state.in_combat)
        new_mob = bool(live_now and aim_now != self._attacking and not engaged)
        stale_swing = self._queued_swing_gone(
            pending, queued_aim, queued_verb, dead, state
        )
        leader_switch = bool(
            self._with_leader(state)
            and live_now
            and self._attacking
            and aim_now
            and aim_now != self._attacking
        )
        retarget = new_mob or stale_swing or leader_switch
        if pending:
            if retarget and cancel is not None and not self._party_pending(state):
                cancel()
            else:
                return
        if state.prompt_seq < self._wait_prompt:
            if retarget:
                self._wait_prompt = state.prompt_seq
            elif self.next_action == "health" and state.max_hp_known:
                # Hits after `health` is enough — do not idle for another prompt.
                self._wait_prompt = state.prompt_seq
            elif time.monotonic() - self._sent_at < 6:
                return
            else:
                self._wait_prompt = state.prompt_seq

        if self._break_after_leave(send, state):
            return

        if self.mode != "gear" and self._combat_flee(state, send):
            return

        if (
            state.geared
            and not self.gear_done
            and self._inv_has_light(state)
            and self._spells_shopped
        ):
            self._torch_bought = True
            self.gear_done = True
            if self.mode == "gear":
                self.mode = "hunt"

        listed = self._listed_fight(state)
        down = (
            self._need_rest(state)
            or state.mortal
            or state.bleeding
            or (
                self._mana_dry(state)
                and not self._recovering
                and not self._healed(state)
                and (
                    self._pit_grind()
                    or (
                        modules.at_farm(state.room)
                        and modules.in_newhaven(state.room)
                    )
                )
            )
        )
        may_rest = (
            not self._with_leader(state)
            or self._ninja_party(state)
            or state.mortal
            or state.bleeding
            or self._recovering
        )
        if listed and self.mode == "gear" and paths.is_dangerous(state.room):
            self._done_gear()
        if listed and self.mode == "rest" and not (state.mortal or state.bleeding):
            self.mode = "hunt"
        if down and self.mode != "rest" and may_rest:
            if self._try_heal(state, send):
                return
            if self._try_party_mana_rest(state, send):
                return
            if not (listed and not (state.mortal or state.bleeding)):
                self.mode = "rest"
        if self.mode == "rest":
            self._rest(state, send)
            return
        if not listed and not self._torch_shopping and self._dump_extras(state, send):
            return
        if self.mode == "gear":
            self._gear(state, send)
            return
        if self.mode == "hunt":
            self._hunt(state, send)

    def _need_rest(self, state: WorldState) -> bool:
        if state.hp is None:
            return False
        ratio = state.hp_ratio()
        if ratio is not None:
            need = PARTY_REST_RATIO if self._ninja_party(state) else self.rest_ratio
            return ratio < need
        return state.hp < REST_ABS

    def _hp_critical(self, state: WorldState) -> bool:
        ratio = state.hp_ratio()
        return ratio is not None and ratio < FLEE_RATIO

    def _flee_step(self, state: WorldState, want: str = "") -> str:
        """Out of the fight — graveyard west to the gate, pit/sewer up."""
        exits = [d for d in (state.exits or []) if d]
        step = (want or "").strip().lower()
        if step and (not exits or step in exits):
            return step

        def take(*dirs: str) -> str:
            for d in dirs:
                if d and (not exits or d in exits):
                    return d
            return ""

        low = (state.room or "").lower()
        if modules.at_gy_shack(low) or paths.at_graveyard_gate(low):
            # Rest-park flee: one SW when listed (or hidden allowlist). No wall spam.
            hit = take("sw")
            if hit:
                return hit
            if not exits or paths.allows_hidden(state.room, "sw"):
                return "sw"
            return take("w", "s", "e")
        if modules.at_graveyard(low) or paths.at_crypt(low):
            return take("w", "s", "e")
        if modules.at_sewer(low):
            return take("u")
        if self._in_pit(state):
            return take("u")
        back = realm_map.reverse_dir(self._last_step)
        return take(back, "w", "s", "n", "e", "u")

    def _should_combat_flee(self, state: WorldState) -> bool:
        if self.mode != "hunt":
            return False
        if self._panic_until and time.monotonic() < self._panic_until:
            return False
        if state.ally_fled:
            if self._with_leader(state):
                state.ally_fled = ""
                return False
            return True
        fighting = bool(
            state.in_combat or self._attacking or self._lops_here(state)
        )
        if not fighting:
            return False
        if self._hp_critical(state):
            return True
        return bool(self._leading() and state.ally_wounded)

    def _combat_flee(self, state: WorldState, send) -> bool:
        if not self._should_combat_flee(state):
            return False
        want = state.ally_fled
        state.ally_fled = ""
        step = self._flee_step(state, want)
        if not step:
            return False
        self._recovering = True
        self._attacking = ""
        self._sitting = False
        self._in_camp = True
        if self._go(send, step, state, sneak=False):
            self.next_action = "flee"
            return True
        return False

    def _party_cadence(self, state: WorldState, send) -> bool:
        """Sit when the leader rests; break when they stand."""
        if state.ally_stood:
            state.ally_stood = False
            if self._sitting or state.resting:
                self._sitting = False
                state.resting = False
                self._cmd(send, "break", state)
                self.next_action = "standing"
                return True
            return False
        if state.in_combat or self._attacking or self._lops_here(state):
            return False
        if state.ally_rest and not (self._sitting or state.resting):
            if self._sit(send, state):
                self.next_action = "rest"
                return True
        return False

    def _healed(self, state: WorldState) -> bool:
        if state.hp is None or state.hp < 1 or not state.max_hp:
            return False
        return state.hp >= state.max_hp

    def _mana_dry(self, state: WorldState) -> bool:
        """Known mana pool is empty. Ninja/thief prompts have no MA."""
        if state.ma is None:
            return False
        return state.ma <= 0

    def _mana_ready(self, state: WorldState) -> bool:
        if state.ma is None:
            return True
        if state.max_ma:
            return (state.ma / state.max_ma) >= REST_RATIO
        return state.ma > 0

    def _rested_up(self, state: WorldState) -> bool:
        """HP full, and mana back if we have a pool."""
        return self._healed(state) and self._mana_ready(state)

    def _try_party_mana_rest(self, state: WorldState, send) -> bool:
        """Empty MA or GY rest: walk to the bridge, then one `!heal` on arrival.

        Followers stay glued to the leader — no solo restpark / picklock /
        SW bridge hop. Leader drives park via !rest / !heal.
        """
        if self._party_rest or self.mode == "goto":
            return False
        if self._followed or state.following:
            return False
        if self._pit_grind() or modules.in_newhaven(state.room):
            return False
        if state.in_combat or self._lops_here(state) or self._attacking:
            return False
        if not (self._mana_dry(state) or self._need_rest(state) or modules.at_gy_shack(state.room)):
            return False
        if not (self._mana_dry(state) or self._park_rest_room(state) or modules.at_gy_shack(state.room)):
            return False
        if self.start_party_rest(state) is None:
            return False
        self._goto_tick(state, send)
        return True

    def _ready_to_hunt(self, state: WorldState) -> bool:
        if state.mortal or state.bleeding:
            return False
        if self._need_rest(state):
            return False
        if self._mana_dry(state) and not self._recovering:
            return False
        if state.hp is not None and state.hp < 1:
            return False
        return True

    def _needs_heal(self, state: WorldState) -> bool:
        if state.hp is None:
            return False
        if state.max_hp_known:
            ratio = state.hp_ratio()
            if ratio is None:
                return False
            return ratio <= HEAL_RATIO
        return state.hp < REST_ABS

    def _in_pit(self, state: WorldState) -> bool:
        if modules.at_gy_shack(state.room) or modules.at_rest_park(state.room):
            return False
        if modules.at_farm(state.room):
            return True
        if paths.in_silvermere(state.room):
            return False
        if paths.is_dangerous(state.room):
            return True
        low = state.room.lower()
        if any(
            word in low
            for word in (
                "road",
                "path",
                "entrance",
                "shop",
                "healer",
                "guild",
                "store",
                "passage",
                "street",
                "square",
            )
        ):
            return False
        return self._in_camp

    def _party_ready(self, state: WorldState) -> bool:
        """Grouped enough to hunt. Leader waits until every roster toon here followed."""
        self._sync_party(state)
        if not self._leading():
            if not (state.following or self._followed):
                return False
            return not self._rank_cmd(state)
        alts = self._alts_here(state)
        if not alts:
            return True
        return all(self._with_me(n, state) for n in alts)

    def _with_me(self, name: str, state: WorldState) -> bool:
        return any(self._same_toon(name, seen) for seen in state.followers)

    def _can_drop_arena(self, state: WorldState) -> bool:
        """Clear room with `d` to the pit once the party is ready."""
        if self._in_pit(state) or self._lops_here(state):
            return False
        if self._with_leader(state) or self._joining_leader(state):
            return False
        if self._followed or self._wounded_bleeding(state):
            return False
        if not self._ready_to_hunt(state):
            return False
        if (
            self.leader
            and self._leading()
            and self._alts_here(state)
            and not self._party_ready(state)
        ):
            return False
        step = self._farm_step(state)
        if paths.is_farm_drop(step):
            return True
        return bool(step) and paths.wants_silvermere_farm(
            state.level, state.room, state.arena_gated
        )

    def _down(self, state: WorldState) -> bool:
        return self._sitting or state.resting

    def _lops_here(self, state: WorldState) -> bool:
        return bool(_trusted_live(state) or modules.farm_here(state.mobs))

    def _listed_fight(self, state: WorldState) -> bool:
        """Join/look already printed a farm lop — swing before i/look/health."""
        if not self._lops_here(state) or not self._opens_swing(state):
            return False
        return bool(
            state.saw_here
            or state.saw_see
            or state.in_combat
            or self._attacking
            or (self._in_pit(state) and state.scanned)
        )

    def _swing_first(self, state: WorldState, send) -> bool:
        """A listed farm lop is a fight. Skip boot look when Also here already printed."""
        live = self._hunt_live(state)
        if not live or not self._opens_swing(state):
            return False
        if self._ambush_boot and not self._boot_looked and not state.saw_here:
            return False
        if self._ambush_boot:
            self._clear_ambush_boot()
        if self._engage_lop(state, send, live):
            self._evaded = False
            return True
        return False

    def _party_here(self, state: WorldState) -> set[str]:
        names = set(self._aka) | set(state.self_names)
        if self.me:
            names.add(self.me)
        if self.leader:
            names.add(self.leader)
        names.update(state.followers)
        if state.following:
            names.add(state.following)
        return {n.strip().lower() for n in names if n and str(n).strip()}

    def _occupants(self, state: WorldState) -> list[str]:
        extras = self._aka | state.self_names
        return paths.occupants_in(state.mobs, extras)

    def _strangers_here(self, state: WorldState) -> bool:
        """Other PCs (Coorwyn) or named NPCs (Corwyn). Cannot sneak with them."""
        return bool(self.compass.strangers(self._occupants(state), self._party_here(state)))

    def _may_sit(self, state: WorldState) -> bool:
        """Sit only when no lop is here and the pit scan is trustworthy.

        A look in flight, a just-`d` drop, or an unscanned pit is not empty.
        """
        if _trusted_live(state) or modules.farm_here(state.mobs):
            return False
        if state.in_combat or self._attacking:
            return False
        if state.look_scan or self._drop_scan:
            return False
        if self._in_pit(state) and not state.scanned:
            return False
        return True

    def _busy_swing(self, state: WorldState) -> bool:
        """Look mid-swing or on the same prompt as attack/bs flickers combat."""
        if paths.is_unlatch_step(self._last_step) or paths.is_unlatch_step(
            self.compass.pending
        ):
            return False
        if state.in_combat or self._attacking:
            return True
        if self._last_verb not in {"attack", "att", "bash", "aa", "bs"}:
            return False
        return state.prompt_seq <= self._wait_prompt

    def _ask_look(self, send, state: WorldState) -> bool:
        if self._busy_swing(state):
            self._want_look = False
            return False
        self._drop_scan = False
        state.look_scan = True
        state.saw_here = False
        state.saw_see = False
        self._cmd(send, "look", state)
        return True

    def _ask_room_pulse(self, send, state: WorldState) -> bool:
        """Refresh Also here without “looking around” when possible.

        Policy from ``modules.room_refresh_cmd``: bare Enter first (Statline
        Full brief reprint), then look.
        """
        if self._busy_swing(state):
            return False
        cmd = modules.room_refresh_cmd(soft_tries_used=self._room_soft_tries)
        self._drop_scan = False
        state.look_scan = True
        state.saw_here = False
        state.saw_see = False
        if cmd == "":
            self._room_soft_tries += 1
            self.next_action = "waiting"
            send("")
            self._wait_prompt = state.prompt_seq + 1
            self._sent_at = time.monotonic()
            return True
        self._room_soft_tries = 0
        self._cmd(send, cmd, state)
        return True

    def _sit(self, send, state: WorldState) -> bool:
        if not self._may_sit(state):
            return False
        if self._sitting or state.resting:
            self._sitting = True
            self.next_action = "healing"
            return True
        self._cmd(send, "rest", state)
        self._sitting = True
        return True

    def _wounded_bleeding(self, state: WorldState) -> bool:
        """Still bleeding in the room — do not leave the road to hunt."""
        if state.ally_mortal:
            return True
        return bool(state.bleeding and not state.aided)

    def _can_sneak_here(self, state: WorldState) -> bool:
        """`sn` when the tile is ours. Party mates (Matt) are not strangers.

        Town watch / village gates refuse sneak. Busy-fail locks this title
        until we land somewhere else.
        """
        if self._want_look or state.look_scan:
            return False
        if not (state.room or "").strip():
            return False
        if self._sneak_busy:
            here = realm_map.room_key(state.room)
            if here and here != self._sneak_denied:
                self._sneak_busy = False
                self._sneak_denied = ""
                self._sneak_block = False
            else:
                return False
        if paths.watchful_room(state.room):
            return False
        if not self.compass.can_sneak(
            self._occupants(state),
            self._party_here(state),
            combat=bool(state.in_combat or self._attacking),
            has_lop=self._lops_here(state),
        ):
            return False
        if self._ninja_party(state):
            return self.stealth != "walk"
        return self._own_ambush(state)

    def _note_evasion(self, state: WorldState) -> None:
        """*Combat Off* after leaving the pit is a successful flee.

        Drop the fight lock. Break first so the game fight is actually
        off, then sneak (empty) or swing (a followed lop).
        """
        off = state.combat_off
        state.combat_off = False
        if off:
            # *Combat Off* ends the swing — even in the pit. Ghost aim
            # (`acid slime` after the fight) must not fire on the next spawn.
            fighting = bool(self._attacking or self._pit_fight)
            self._attacking = ""
            if fighting:
                self._hidden = False
                self._sneaking = False
                self._sneak_armed = False
                self._sneak_wait = False
                self._sneak_block = False
                # Game still has you swinging until `break`. `sn`/`look` first
                # is the rest/look storm after a pit kill.
                self._need_break = True
            if not self._lops_here(state):
                # Prefer the timed room tick / arrive. Forcing look here made
                # the whole party spam "X is looking around the room."
                self._want_look = False
                if fighting and self._stealth_moves() and self._in_pit(state):
                    state.scanned = True
        if not off or self._in_pit(state):
            self._evaded = False
            return
        self._evaded = True
        self._need_break = True
        self._attacking = ""
        self._in_camp = False
        self._pit_fight = False

    def _break_after_leave(self, send, state: WorldState) -> bool:
        """After leaving a fight room, `break` before sneak/look/rest/aid.

        Combat can stay on in the game after the room change. Other
        commands then fail or re-aggro. One break, then the next prompt.
        Still in the pit with a monster: stay in the fight.
        """
        if self._ambush_boot:
            return False
        if not self._need_break:
            return False
        if self._needs_heal(state) or state.mortal or state.bleeding:
            return False
        if self._with_leader(state) and not self._sneak_block:
            if not self._stealth_moves():
                self._need_break = False
                return False
        if self._in_pit(state) and self._lops_here(state) and self._opens_swing(state):
            self._need_break = False
            return False
        self._need_break = False
        self._sitting = False
        self._attacking = ""
        state.in_combat = False
        self._cmd(send, "break", state)
        return True

    def _sneak_ready(self) -> bool:
        return bool(self._hidden or self._sneak_armed or self._sneaking)

    def _sneak_settled(self) -> bool:
        """Hidden already, or armed and the setup beat has passed."""
        if self._hidden or self._sneaking:
            return True
        if not self._sneak_armed:
            return False
        return time.monotonic() >= self._sneak_ready_at

    def _holding_sneak(self, state: WorldState) -> bool:
        """Armed after Attempting — look/`d` now would break sneak."""
        if state.in_combat or self._lops_here(state):
            return False
        if self._ninja_party(state):
            return bool(self._sneak_armed and not self._sneak_settled())
        if self._with_leader(state):
            return False
        return (
            self._sneak_armed
            and not self._sneak_settled()
            and not self._in_pit(state)
        )

    def _need_look(self, state: WorldState) -> bool:
        """Type `look` only for an explicit reason — prefer WG room ticks.

        `look_scan` means a title/tick is already in flight: wait, do not pile on.
        Unscanned alone is not enough; exits/party/boot set `_want_look` or call
        `_ask_look` from nav paths.
        """
        if self._busy_swing(state):
            return False
        if self._lops_here(state):
            return False
        if state.look_scan:
            return False
        if self._in_pit(state) and self._pit_fight and not paths.coins_in(state.things):
            return False
        if state.in_combat and self._attacking and _still_here(state, self._attacking):
            return False
        # Ambush outside the pit: `sn` / `d`, not a look this tick.
        if self._own_ambush(state) and not self._in_pit(state) and not self._want_look:
            return False
        # Party ninja: stay stealthed; creep/Also here lists the next lop.
        if self._ninja_party(state) and not self._want_look:
            return False
        # Solo road drop — walk `d`; do not stall on drop_scan / want_look.
        if not self._with_leader(state) and self._arena_drop(state) and not self._want_look:
            return False
        # Known walk — do not look-loop (Secret Passage, Helfgrim, streets).
        if not self._want_look and self._farm_step(state):
            return False
        if self._drop_scan or self._want_look:
            return True
        return False

    def _note_room_pulse(self, state: WorldState) -> None:
        """Remember the last Also here / exits pulse (WG timed short look)."""
        seq = int(state.travel_seq or 0)
        if seq != self._room_pulse_seq:
            self._room_pulse_seq = seq
            self._room_pulse_at = time.monotonic()
            return
        if state.saw_here and self._room_pulse_at <= 0:
            self._room_pulse_at = time.monotonic()

    def _wait_room_tick(self, state: WorldState, send) -> bool:
        """True when standing for Also here / exits instead of typing look.

        Listings come from move/combat and Statline Full bare Enter — not a
        WG idle 6n timer. After LOOK_GAP, soft-refresh (Enter) then look.
        """
        if state.scanned or state.look_scan or self._lops_here(state):
            if state.scanned:
                self._room_soft_tries = 0
            return False
        if self._busy_swing(state) or state.in_combat or self._attacking:
            return False
        # Known walk / arena drop: move without a look.
        if self._farm_step(state) or self._arena_drop(state):
            return False
        if time.monotonic() - self._sent_at >= LOOK_GAP:
            return self._ask_room_pulse(send, state)
        self.next_action = "waiting"
        return True

    def note_send(self, text: str, room: str = "") -> None:
        """Typed or peeked walk — remember the door so the next title maps."""
        raw = (text or "").strip().lower()
        if not is_move(raw):
            return
        parts = raw.split()
        if len(parts) == 3 and parts[0] == "drag":
            raw = parts[2]
        self._last_step = raw
        if room.strip():
            self._step_room = room.strip()

    def _map_here(self, state: WorldState) -> None:
        """Record this tile. Unknown rooms go on the atlas the first time we see them."""
        title = (state.room or "").strip()
        if not title or parse.looks_like_stat_sheet(title):
            return
        if not paths.in_afterlife(title):
            self._last_live_room = title
        here = realm_map.room_key(title)
        left = realm_map.room_key(self._step_room)
        via = self._last_step if here and left and here != left else ""
        prev = self._step_room if via else ""
        self.world.observe(title, state.exits, via, prev)

    def _farm_step(self, state: WorldState) -> str:
        level, gated = self._farm_nav(state)
        return (
            self.world.step(
                self._hunt_goal(),
                state.room,
                state.exits,
                last_step=self._last_step,
                silver_east=self._store_silver_east,
                skiff_i=self._skiff_ts,
                level=level,
                gated=gated,
                closed=state.closed_exits,
                klass=self.klass,
            )
            or ""
        )

    def _skip_reverse(self, step: str, state: WorldState) -> bool:
        """True when this would be an e/w thrash — except unique dead-end pins."""
        if step not in {"n", "s", "e", "w"}:
            return False
        back = realm_map.reverse_dir(self._last_step)
        if not back or step != back or len(state.exits or []) <= 1:
            return False
        want = self._nav_goal()
        return pinned_dir(state.room, want) != step

    def _walk_out(self, state: WorldState, send) -> bool:
        """Leave a known or one-door tile. Never look-spam when already scanned."""
        if self._with_leader(state) or self._joining_leader(state):
            return False
        step = self._farm_step(state)
        if step:
            if not self._skip_reverse(step, state) and self._go(send, step, state):
                return True
        if self._map_step(state, send):
            return True
        only = [d for d in (state.exits or []) if d in realm_map.DIRS]
        if len(only) == 1 and self._go(send, only[0], state):
            return True
        if not state.scanned and not state.look_scan:
            return self._ask_look(send, state)
        return False

    def _arena_drop(self, state: WorldState) -> bool:
        return paths.is_farm_drop(self._farm_step(state))

    def _should_leave_to_sneak(self, state: WorldState) -> bool:
        """A listed lop is a fight. Do not walk off to sn first."""
        return False

    def _setup_sneak(self, send, state: WorldState) -> bool:
        """One `sn` on an empty room. True if we acted.

        `Attempting` is not ready. After settle with no fail, assume hidden.
        Sitting: break first. Busy fail: one `break`, then leave this tile.
        Do not send another `sn` while `_sneak_wait` — that is the KEY_GAP storm.
        Other fail: flop — solo moves to another empty room, then `sn`.
        Party stays with Matt and `sn`s the next empty tile after the block.
        """
        if not self._can_sneak_here(state):
            return False
        if self._sneak_flop and not self._ninja_party(state):
            return False
        if self._hidden or self._sneak_armed or self._sneaking:
            return False
        if self._sneak_wait:
            # Already sent `sn`. Wait for try/fail/ok — never stack.
            return False
        if self._sneak_block and self._need_break:
            return False
        if self._down(state):
            self._sitting = False
            self._cmd(send, "break", state)
            return True
        if self._sneak_block:
            # Still on the denied tile — do not retry `sn`.
            return False
        self._sneak_flop = False
        if self._send_sneak(send, state):
            self._sneak_wait = True
            return True
        return False

    def _send_sneak(self, send, state: WorldState) -> bool:
        """Sneak while standing, combat off, room empty."""
        if not self._can_sneak_here(state):
            return False
        if state.in_combat or self._lops_here(state):
            return False
        if self._down(state):
            self._sitting = False
            self._cmd(send, "break", state)
            return False
        self._cmd(send, "sn", state)
        return True

    def _party_sneak(self, state: WorldState, send) -> bool:
        """Following: `sn` whenever the room allows it. Hidden already: hold."""
        if not self._ninja_party(state) or state.in_combat:
            return False
        if self._lops_here(state):
            return False
        if self._needs_heal(state):
            return False
        if self._stealthed():
            self.next_action = "ambush"
            return True
        if self._setup_sneak(send, state):
            return True
        if self._sneak_armed or self._sneak_wait:
            self.next_action = "ambush"
            return True
        return False

    def _retry_after_flop(self, state: WorldState, send) -> bool:
        """Solo: after a sneak fail, leave this room, then `sn` on the next empty.

        Party stays with Matt — next empty tick retries `sn`. Occupied: `bs`.
        """
        if not self._sneak_flop:
            return False
        if self._with_leader(state) or self._joining_leader(state):
            self._sneak_flop = False
            return False
        if self._lops_here(state) or state.in_combat:
            self._sneak_flop = False
            return False
        if self._ninja_party(state):
            return False
        self._sneak_flop = False
        if self._in_pit(state):
            self._travel(send, "u", state, sneak=False)
            return True
        drop = self._farm_step(state)
        if paths.is_farm_drop(drop):
            self._travel(send, drop, state, sneak=False)
            return True
        step = self._farm_step(state)
        if not step:
            return False
        if (
            state.exits
            and step not in state.exits
            and not paths.is_special_step(step)
            and not paths.allows_hidden(state.room, step)
        ):
            return False
        self._travel(send, step, state, sneak=False)
        return True

    def _street_step(self, step: str, state: WorldState) -> str:
        """Temple west is the hall; casino south is the pit. Force home."""
        if paths.step_skiff_to_square(state.room, None, skiff_i=self._skiff_ts):
            if not (
                self._nav_goal() in {"farm", "graveyard", "guild"}
                and step == "s"
                and "guild street" in (state.room or "").lower()
            ):
                return step
        low = (state.room or "").lower()
        if step == "w" and "temple street" in low:
            return "e"
        if step == "s" and ("lucky strike" in low or "casino" in low):
            return "n"
        # Mid Guild Street is n/s only. GY/guild is north; south is the n/s loop.
        if (
            step == "s"
            and "guild street" in low
            and self._nav_goal() in {"farm", "graveyard", "guild"}
            and "n" in (state.exits or [])
        ):
            return "n"
        return step

    def _cmd(self, send, text: str, state: WorldState) -> None:
        if text in {"n", "s", "e", "w"}:
            text = self._street_step(text, state)
        verb, sep, rest = text.partition(" ")
        if (
            sep
            and verb.lower() in {"attack", "att", "bash", "aa", "bs"}
            and rest
            and not paths.is_unlatch_step(text)
        ):
            aim = self._swing_name(rest)
            if not aim:
                return
            text = f"{verb.lower()} {aim}"
            self._last_aim = aim
        self._last_verb = text.split()[0].lower()
        self.next_action = text
        send(text)
        self._wait_prompt = state.prompt_seq + 1
        self._sent_at = time.monotonic()
        if text == "break":
            self._need_break = False
        parts = text.split()
        step = ""
        if text in {"n", "s", "e", "w", "u", "d", "ne", "nw", "se", "sw"}:
            step = text
        elif paths.is_special_step(text):
            step = text
        elif (
            len(parts) == 3
            and parts[0].lower() == "drag"
            and parts[2].lower() in {"n", "s", "e", "w", "u", "d"}
        ):
            step = parts[2].lower()
        if step:
            self._last_aim = ""
            if state.in_combat or self._attacking:
                self._need_break = True
            if step == "u" and self._ninja() and self._stealth_moves() and not self._ambush_boot:
                self._need_break = True
            self._last_step = step
            self._step_room = state.room
            self._step_prompt = state.prompt_seq
            self._advance_skiff_ts(state.room, step)
            self.compass.note(
                step,
                state.room,
                state.prompt_seq,
                travel_seq=state.travel_seq,
                exits=state.exits,
            )
            self._hidden = False
            self._sneaking = False
            self._sneak_armed = False
            self._sneak_block = False
            self._sneak_busy = False
            self._sneak_denied = ""
            self._sneak_ready_at = 0.0
        if step == "d" or step == "go manhole":
            self._in_camp = True
            self._pit_fight = True
            self._drop_scan = True
            state.mobs = []
            state.things = []
            state.scanned = False
            state.look_scan = True
            state.saw_here = False
            state.saw_see = False
            if step == "d" and paths.wants_silvermere_farm(
                state.level, state.room, state.arena_gated
            ):
                # Level 4+ cannot enter the pit. Do not pretend we landed.
                self._pit_fight = False
                self._drop_scan = False
            elif step == "d" and modules.in_newhaven(state.room) and "arena" not in state.room.lower():
                state.room = "Newhaven, Arena"
                state.exits = [x for x in state.exits if x != "d"]
            elif step == "d" and "arena" not in state.room.lower() and not paths.in_silvermere(
                state.room
            ):
                state.room = "Newhaven, Arena"
                state.exits = [x for x in state.exits if x != "d"]
        elif step == "u":
            self._in_camp = False
            self._pit_fight = False
            self._drop_scan = False
            self._attacking = ""
            state.mobs = []
            state.things = []
            state.scanned = False
            if "arena" in state.room.lower():
                state.room = "Newhaven, Narrow Road"
                state.exits = ["n", "e", "w", "d", "s"]

    def _travel(
        self, send, step: str, state: WorldState, *, sneak: bool = True
    ) -> None:
        """Move. Ninja ambush sneaks first on an empty room; never with lops."""
        step = self._street_step(step, state)
        if step == "go manhole" and self._needs_sewer_torch():
            if not self._sewer_stocked or not self._torch_lit:
                self.next_action = "torch"
                if self._stock_torch_before_sewer(state, send):
                    return
                if not (self._sewer_stocked and self._torch_lit):
                    return
        if step in realm_map.DIRS and state.exits and step not in state.exits:
            if step not in realm_map.SPECIALS and not paths.allows_hidden(
                state.room, step
            ):
                self.next_action = "looking"
                if not self._busy_swing(state):
                    self._cmd(send, "look", state)
                return
        if (
            sneak
            and self._own_ambush(state)
            and step in realm_map.DIRS
            and not state.in_combat
            and not self._attacking
        ):
            if self._setup_sneak(send, state):
                self._pending_step = self._pending_step or step
                return
            if (
                step == "d"
                and not self._sneak_ready()
                and not self._lops_here(state)
                and not self._in_pit(state)
            ):
                return
            if self._holding_sneak(state):
                self._pending_step = self._pending_step or step
                self.next_action = "ambush"
                return
            self._pending_step = ""
        self._pending_step = ""
        self._cmd(send, step, state)
        if step == "go manhole":
            self._sewer_stocked = False
            self._torch_bag_ready = False
            self._torch_checked = False
            self._torch_wait_inv = False

    def _clear_landed_step(self, state: WorldState) -> None:
        """Same heading from a new room is a new walk — Betram `n` then weapons `n`.

        Some streets reuse one title for many tiles (Silver Street). There a new
        prompt after a move means we landed even when the title did not change.
        River Street keeps e/w until a unique Bridge intersection; a unique pin
        that dumps onto that corridor must not wipe the heading.
        """
        if not self._last_step:
            return
        here = (state.room or "").strip()
        left = (self._step_room or "").strip()
        want = self._nav_goal()
        if here and left and here != left:
            self._blocked_dirs.clear()
            if (
                pinned_dir(left, want) == self._last_step
                and paths.same_title_corridor(here)
            ):
                return
            self._last_step = ""
            return
        if (
            here
            and left
            and here == left
            and paths.plain_river_street(here)
            and self._last_step in {"e", "w"}
        ):
            return
        if (
            here
            and left
            and here == left
            and state.prompt_seq > self._step_prompt
            and state.travel_seq > self.compass.travel
        ):
            self._last_step = ""
            self._blocked_dirs.clear()
    def _go(self, send, step: str, state: WorldState, *, sneak: bool = True) -> bool:
        if not step:
            return False
        step = self._street_step(step, state)
        raw_step = (step or "").strip().lower()
        short = (
            paths.unlatch_dir_short(raw_step)
            if paths.is_unlatch_step(raw_step)
            else raw_step
        )
        # Leader owns latches. Followers must not peel off to bash/pick the shack.
        if paths.is_unlatch_step(step) and (
            self._with_leader(state) or self._followed or state.following
        ):
            return False
        if short and short in self._blocked_dirs:
            # Same dir just hit a wall — look / pick another exit, do not re-spam.
            if state.exits:
                state.exits = [x for x in state.exits if x.lower() != short]
            self.compass._fail()
            self.next_action = "looking"
            if not self._busy_swing(state):
                self._cmd(send, "look", state)
            return True
        if state.blocked:
            listed = [x.lower() for x in (state.exits or [])]
            raw = (step or "").strip().lower()
            want = raw if raw in listed else paths.unlatch_dir_short(raw)
            if (
                want == "n"
                and want in listed
                and paths.gy_gate_never_south(state.room, None)
            ):
                # Stale "The gate is closed." after a bash that opened it.
                state.blocked = False
                state.blocked_dir = ""
            else:
                dead = (
                    state.blocked_dir
                    or self.compass.pending
                    or self._last_step
                    or step
                    or ""
                ).strip().lower()
                if paths.is_unlatch_step(dead):
                    dead = paths.unlatch_dir_short(dead) or dead
                if dead:
                    self._blocked_dirs.add(dead)
                if dead in state.exits:
                    state.exits = [x for x in state.exits if x != dead]
                self.compass._fail()
                state.blocked = False
                state.blocked_dir = ""
                self.next_action = "looking"
                if not self._busy_swing(state):
                    self._cmd(send, "look", state)
                return True
        if (
            state.exits
            and step not in state.exits
            and not paths.is_special_step(step)
            and not paths.allows_hidden(state.room, step)
        ):
            return False
        if not state.exits and self.mode == "gear":
            return False
        gate = self.compass.see(
            state.room,
            state.prompt_seq,
            blocked=state.blocked,
            travel_seq=state.travel_seq,
            exits=state.exits,
        )
        if state.blocked:
            state.blocked = False
            state.blocked_dir = ""
        if gate == "wait":
            self.next_action = "waiting"
            return True
        if gate == "look":
            self.next_action = "looking"
            unlatch = paths.is_unlatch_step(self.compass.pending)
            if not unlatch and (state.in_combat or self._busy_swing(state)):
                return True
            self._cmd(send, "l" if unlatch else "look", state)
            return True
        # Hard gate: never dive without bag + lit torch.
        if step == "go manhole" and self._needs_sewer_torch():
            if not self._sewer_stocked or not self._torch_lit:
                self.next_action = "torch"
                if self._stock_torch_before_sewer(state, send):
                    return True
                # Stock may have just lit / marked ready — dive now.
                if self._sewer_stocked and self._torch_lit:
                    pass
                else:
                    return False
        # `_travel` clears stock flags once the manhole command actually fires.
        self._travel(send, step, state, sneak=sneak)
        return True

    def _map_step(self, state: WorldState, send) -> bool:
        if self._followed or state.following:
            return False
        # Hard compass for temple district — do not atlas-wander into the hall.
        # Run even while torch-stocking: east is both TS and the store.
        low = (state.room or "").lower()
        skiffing = bool(
            paths.step_skiff_to_square(state.room, None, skiff_i=self._skiff_ts)
        )
        if not skiffing and "temple street" in low:
            if self._go(send, "e", state):
                self.next_action = "temple east"
                return True
        if not skiffing and ("lucky strike" in low or "casino" in low):
            if self._go(send, "n", state):
                self.next_action = "leave casino"
                return True
        if self._torch_shopping or self._torch_bag_ready or self._torch_wait_inv:
            return False
        if (
            self._needs_sewer_torch()
            and self._leads_sewer_loop()
            and not self._sewer_stocked
        ):
            return False
        level, gated = self._farm_nav(state)
        step = self.world.step(
            self._hunt_goal(),
            state.room,
            state.exits,
            last_step=self._last_step,
            silver_east=self._store_silver_east,
            skiff_i=self._skiff_ts,
            level=level,
            gated=gated,
            closed=state.closed_exits,
            klass=self.klass,
        )
        if step:
            if self._skip_reverse(step, state):
                return False
            if self._go(send, step, state):
                self.next_action = f"path: {step}"
                return True
        hint = self._atlas.suggest(
            state.room, state.exits, self._last_step, state.scanned
        )
        if hint.action == "look":
            if state.scanned or self._busy_swing(state):
                return False
            self._cmd(send, "look", state)
            if hint.chrome:
                self.next_action = hint.chrome
            return True
        if hint.action in {"path", "guess"} and hint.step:
            # Refuse immediate reverse — that is the classic w/e thrash.
            # A one-door tile has to reverse: that is leave, not thrash.
            if self._skip_reverse(hint.step, state):
                return False
            if "guild street" in low and hint.step in {"e", "w"}:
                return False
            if (
                "guild street" in low
                and hint.step == "s"
                and self._nav_goal() in {"farm", "graveyard", "guild"}
            ):
                return False
            if self._go(send, hint.step, state):
                if hint.chrome:
                    self.next_action = hint.chrome
                return True
        return False

    def _rest(self, state: WorldState, send) -> None:
        live = _trusted_live(state) or modules.farm_here(state.mobs)
        if live or state.in_combat or self._attacking:
            self.mode = "hunt" if self.gear_done else "gear"
            self._hunt(state, send)
            return
        if self._try_party_mana_rest(state, send):
            return
        if self._in_pit(state) and not self._may_sit(state):
            if state.look_scan:
                self.next_action = "looking"
                return
            if self._drop_scan:
                if self._ask_look(send, state):
                    return
        if self._healed(state) or (self._recovering and self._ready_to_hunt(state)):
            # Full HP but dry MA: sit to refill (party rest / bless). Do not
            # shout !healed until `_rested_up`.
            if not self._mana_ready(state):
                if self._sitting or state.resting:
                    self.next_action = "healing"
                    return
                if self._sit(send, state):
                    return
                self.next_action = "waiting"
                return
            if self._party_rest or self._park_rest:
                if self._party_rest:
                    self._party_rest_tick(state, send)
                    return
                self._park_rest = False
                self.mode = "manual"
                self.next_action = "at rest"
                return
            self._recovering = False
            self._attacking = ""
            self.mode = "hunt" if self.gear_done else "gear"
            self.next_action = self.mode
            if self._in_party(state) and (self._sitting or state.resting):
                self._sitting = False
                state.resting = False
                self._cmd(send, "break", state)
                self.next_action = "standing"
                return
            if self.mode == "hunt" and self._stealth_moves():
                self._hunt(state, send)
                return
            self._sitting = False
            return
        if self._try_heal(state, send):
            return
        if self._in_pit(state):
            stay = (
                state.mortal
                or state.bleeding
                or self._in_party(state)
                or self._pit_grind()
            )
            if stay:
                if self._sitting:
                    self.next_action = "healing"
                    return
                if self._sit(send, state):
                    return
                self.next_action = "looking" if state.look_scan else "waiting"
                return
            self._sitting = False
            self._attacking = ""
            self._travel(send, "u", state)
            return
        room = state.room.lower()
        if self._fix_dark(state, send):
            return
        if "general" in room:
            step = self._farm_step(state)
            self._sitting = False
            if step:
                self._go(send, step, state)
            return
        if self._sitting:
            self.next_action = "healing"
            return
        if not self._sit(send, state):
            self.next_action = "waiting"

    def _extra_name(self, state: WorldState) -> str | None:
        if self._await_inv:
            return None
        return paths.extra_starter(
            state.inventory, state.extras, self._skip_sell, state.worn
        )

    def _note_sales(self, state: WorldState) -> None:
        """Sold / a leftover `You are carrying` on screen is not a fresh `i`."""
        if state.flooded:
            state.flooded = False
            self._selling = ""
            self._await_inv = True
        sold = (state.last_sold or "").strip().lower()
        if sold:
            state.last_sold = ""
            self._selling = ""
            self._await_inv = True
        if state.shop_vague and self._selling:
            self._skip_sell.add(self._selling)
            self._selling = ""
            state.shop_vague = False
            self._await_inv = True
        # Off-shop sell miss — stop the sell/`i` loop.
        if self._selling and not state.in_shop and not self._in_sell_shop(state):
            self._selling = ""
            self._await_inv = False
            self._pry_sent = False
        if (
            self._pry_sent
            and state.inv_seq > self._inv_after
            and state.prompt_seq > self._i_prompt
        ):
            self._inv_seen = state.inv_seq
            self._await_inv = False
            self._pry_sent = False

    def _ask_inv(self, send, state: WorldState) -> bool:
        if self._pry_sent:
            if state.inv_seq > self._inv_after:
                self._inv_seen = state.inv_seq
                self._await_inv = False
                self._pry_sent = False
                return False
            return True
        self._pry_sent = True
        self._await_inv = True
        self._inv_after = state.inv_seq
        self._i_prompt = state.prompt_seq
        self._cmd(send, "i", state)
        return True

    def _in_sell_shop(self, state: WorldState) -> bool:
        room = state.room.lower()
        here = " ".join(state.mobs).lower()
        return (
            state.in_shop
            or paths.is_armour_shop(room)
            or paths.is_weapon_shop(room)
            or paths.is_general_store(room)
            or "shop" in room
            or "store" in room
            or "betram" in here
            or "bertram" in here
        )

    def _sell_extra(self, state: WorldState, send) -> bool:
        """Sell one spare from the last `i`. Do not guess after a miss."""
        self._note_sales(state)
        if self._await_inv:
            return self._ask_inv(send, state)
        extra = self._extra_name(state)
        if not extra or state.in_combat or state.shop_vague:
            return False
        if not self._in_sell_shop(state):
            return False
        # Never dump the sewer torch kit.
        if extra == paths.STARTER_LIGHT and (
            self._torch_shopping
            or self._torch_bag_ready
            or self._torch_est <= paths.TORCH_BAG_MIN
        ):
            return False
        self._selling = extra
        self._await_inv = True
        self._pry_sent = False
        self._cmd(send, f"sell {extra}", state)
        return True

    def _dump_extras(self, state: WorldState, send) -> bool:
        """Sell stacks until `i` has no extras. Walk to Betram if needed."""
        if self._torch_shopping or self._torch_bag_ready or self._torch_wait_inv:
            return False
        self._note_sales(state)
        if self._await_inv:
            return self._ask_inv(send, state)
        extra = self._extra_name(state)
        if not extra or state.in_combat or state.shop_vague:
            return False
        if self._sell_extra(state, send):
            return True
        room = state.room.lower()
        if "village entrance" in room:
            if self._go(send, "s", state):
                self._await_inv = True
                self._pry_sent = False
                return True
            return False
        return False

    def _note_already_worn(self, state: WorldState) -> None:
        item = (state.already_worn or "").strip().lower()
        if not item:
            return
        state.already_worn = ""
        if item not in state.worn:
            state.worn.append(item)
        if paths.is_starter_weapon(item, self.klass):
            self._weapon_worn = True
            self._wearing = False
            return
        for i, name in enumerate(paths.ARMOUR_ITEMS):
            if name in item or item in name:
                if self._armour_i <= i:
                    self._armour_i = i + 1
                self._wearing = False
                return
        if self._wearing:
            self._wearing = False
            self._armour_i += 1

    def _skip_worn_armour(self, state: WorldState) -> None:
        while self._armour_i < len(paths.ARMOUR_ITEMS):
            item = paths.ARMOUR_ITEMS[self._armour_i]
            if item in state.worn:
                self._wearing = False
                self._armour_i += 1
                continue
            break

    def _inv_has_light(self, state: WorldState) -> bool:
        held = " ".join(state.inventory + state.worn + state.extras).lower()
        return paths.STARTER_LIGHT in held

    def _sync_gear_from_inv(self, state: WorldState) -> None:
        """Last `i` worn list is truth — do not bounce south for a vest we have on."""
        self._skip_worn_armour(state)
        if state.geared or any(paths.is_starter_weapon(w, self.klass) for w in state.worn):
            self._weapon_worn = True
            self._weapon_bought = True
        if self._inv_has_light(state):
            self._torch_bought = True
            # On inventory sync, trust the count as our starting estimate.
            self._torch_est = self._torch_count(state)

    def _torch_count(self, state: WorldState) -> int:
        """Count torches in inventory/extras/worn (qty is represented by repeats)."""
        held = state.inventory + state.worn + state.extras
        return sum(1 for item in held if paths.is_torch_item(item))

    def _note_torch_light(self, state: WorldState) -> None:
        """Track burning torch from game lines / inventory.

        Inventory often omits ``(lit)`` even while a torch burns — only *set*
        lit from a positive `i` marker, never clear it (torch_out / dark do).
        """
        if state.torch_lit:
            self._torch_lit = True
            return
        held = state.inventory + state.worn + state.extras
        if paths.has_lit_torch(held):
            self._torch_lit = True
            state.torch_lit = True

    def _sync_torch_est(self, state: WorldState) -> None:
        """Trust bag count only after a fresh `i` — never clobber buy estimates."""
        if state.inv_seq <= self._torch_inv_synced:
            return
        self._torch_inv_synced = state.inv_seq
        self._torch_est = self._torch_count(state)
        self._note_torch_light(state)
        if self._torch_est > 0:
            self._torch_bought = True
        if self._torch_est >= paths.TORCH_BAG_MIN:
            self._torch_bag_ready = True

    def _light_torch(self, state: WorldState, send) -> bool:
        """Light one torch. Idempotent when already lit."""
        self._note_torch_light(state)
        if self._torch_lit or state.torch_lit:
            self._torch_lit = True
            state.torch_lit = True
            state.dark = False
            return False
        self._cmd(send, f"light {paths.STARTER_LIGHT}", state)
        # Optimistic — game may reply "already have something lit".
        self._torch_lit = True
        state.torch_lit = True
        state.dark = False
        self.next_action = "light"
        return True

    def _buy_torch_at_store(self, state: WorldState, send) -> bool:
        """Buy until TORCH_BAG_MIN (run + exit spare); one buy per fresh `i`."""
        if self._torch_wait_inv:
            if state.inv_seq > self._torch_inv_after:
                self._torch_wait_inv = False
                self._sync_torch_est(state)
            else:
                self.next_action = "torch"
                return True
        self._sync_torch_est(state)
        if self._torch_est >= paths.TORCH_BAG_MIN:
            self._torch_bag_ready = True
            self._torch_bought = True
            self._torch_shopping = False
            self._store_silver_east = 0
            self._store_expect_silver = False
            self._await_inv = False
            # Not dive-ready yet — still need TS `i` + light.
            self._sewer_stocked = False
            self.next_action = "torch"
            step = paths.step_toward_farm(
                state.room, state.exits, state.level, state.arena_gated
            ) or "n"
            if step:
                self._go(send, step, state)
            return True
        self._torch_est += 1
        self._cmd(send, f"buy {paths.STARTER_LIGHT}", state)
        self._torch_bought = True
        self._tried_torch = False
        self._torch_checked = False
        # Hold buys until we see a new `i` — never spam the shopkeeper.
        self._torch_wait_inv = True
        self._torch_inv_after = state.inv_seq
        self._torch_i_prompt = state.prompt_seq
        self._await_inv = False
        self._pry_sent = False
        self._cmd(send, "i", state)
        self.next_action = "torch"
        return True

    def _needs_sewer_torch(self) -> bool:
        """Sewer kit. Night vision / known room-light skip. Ninja is not NV."""
        return modules.sewer_torch_needed(
            self.race,
            self.klass,
            hunt_torch=bool(self.hunt_run and self.hunt_run.torch),
            room_light_spell=bool(
                modules.known_room_light(self.klass, self._memorized)
            ),
        )

    def _needs_sewer_torch_here(self, state: WorldState) -> bool:
        if modules.known_room_light(self.klass, self._memorized):
            return False
        if not modules.needs_torch(self.race, self.klass):
            return False
        return modules.at_sewer(state.room)

    def _leads_sewer_loop(self) -> bool:
        """Only the loop leader stocks/dives; followers ride along."""
        return self._leading()

    def _at_manhole_entry(self, state: WorldState) -> bool:
        return modules.at_manhole_entry(state.room)

    def _kit_ready(self) -> bool:
        return modules.kit_ready(
            armour_i=self._armour_i,
            weapon_worn=self._weapon_worn,
            torch_bought=self._torch_bought,
        )

    def _kit_shop_goal(self) -> str:
        """Next Newhaven pin: padded, club/staff, torch, class scrolls, then pit."""
        return modules.shop_goal(
            armour_i=self._armour_i,
            weapon_worn=self._weapon_worn,
            torch_bought=self._torch_bought,
            spells_shopped=self._spells_shopped,
        )

    def _walk_kit(self, state: WorldState, send) -> None:
        """Leave the guild / road toward the next missing shop, then the arena."""
        goal = self._kit_shop_goal()
        if goal == "farm" and modules.farm_arrived(
            state.room, dangerous=paths.is_dangerous(state.room)
        ):
            self._done_gear()
            return
        step = self.world.step(
            goal,
            state.room,
            state.exits,
            last_step=self._last_step,
            silver_east=self._store_silver_east,
            skiff_i=self._skiff_ts,
            level=state.level,
            gated=state.arena_gated,
            closed=state.closed_exits,
            klass=self.klass,
        )
        if not step:
            if goal == "spells":
                step = paths.step_toward_spell_shop(state.room, state.exits)
            elif goal == "store":
                step = paths.step_toward_store(state.room, state.exits)
            else:
                step = paths.step_toward_arena(state.room, state.exits)
        if step and self._go(send, step, state):
            return
        self._cmd(send, "look", state)

    def _advance_spell(self) -> None:
        self._spell_i += 1
        self._spell_buying = False
        self._spell_reading = False
        self._spell_alt = False
        if self._spell_i >= len(self._learn):
            self._spells_shopped = True

    def _shop_one_spell(self, state: WorldState, send) -> bool:
        """Buy then `read` one scroll. Minor healing is first in `_learn`."""
        if self._spells_shopped or self._spell_i >= len(self._learn):
            self._spells_shopped = True
            return False
        name = self._learn[self._spell_i]
        have = 1 if state.level is None else int(state.level)
        # Bless/mend (level 2) are still bought at creation. Illuminate / smite
        # / major healing wait for the ding offer at Rayth.
        if have < spells.min_level(name) and spells.min_level(name) >= 3:
            self._advance_spell()
            return self._shop_one_spell(state, send)
        held = list(state.inventory) + list(state.extras)
        if spells.have_known(held, name):
            self._remember(name)
            self._advance_spell()
            return self._shop_one_spell(state, send)
        if (self._knows(name) or name in self._scrolls_tried) and not spells.have_scroll(
            held, name
        ):
            self._advance_spell()
            return self._shop_one_spell(state, send)
        held_name = spells.first_held_scroll(held, self._learn, self._scrolls_tried)
        if held_name and held_name != name:
            self._spell_i = self._learn.index(held_name)
            name = held_name
            self._spell_buying = False
            self._spell_reading = False
            self._spell_alt = False
        if spells.have_scroll(held, name):
            if self._knows(name):
                self._scrolls_tried.add(name)
                self._advance_spell()
                return self._shop_one_spell(state, send)
            if not self._spell_reading:
                self._last_read = name
                self._cmd(send, spells.read_command(name), state)
                self._spell_reading = True
                self._scrolls_tried.add(name)
                return True
            self._advance_spell()
            return True
        if spells.other_held_scrolls(held, name) and self._spell_buying:
            self._advance_spell()
            return self._shop_one_spell(state, send)
        if not self._spell_buying:
            self._cmd(send, f"buy {spells.buy_name(name, alt=self._spell_alt)}", state)
            self._spell_buying = True
            return True
        if not self._spell_reading:
            self._last_read = name
            self._cmd(send, spells.read_command(name), state)
            self._spell_reading = True
            self._scrolls_tried.add(name)
            return True
        self._advance_spell()
        return True

    def _skip_kit_shops(self) -> None:
        """Dressed — skip Betram / Nathaniel. Torch still if this race needs light."""
        self._armour_i = len(paths.ARMOUR_ITEMS)
        self._weapon_bought = True
        self._weapon_worn = True
        if not modules.needs_torch(self.race, self.klass):
            self._torch_bought = True
        if modules.known_room_light(self.klass, self._memorized):
            self._torch_bought = True

    def _finish_kit_check(self, state: WorldState, send) -> bool:
        """After F7 `i`: shop only if naked. Naked + a logged pile → recover."""
        if not self._need_kit_check:
            return False
        if self._await_inv:
            return False
        self._need_kit_check = False
        naked = modules.is_naked(state.worn, state.inventory, state.extras)
        if self.deathpile and naked:
            if self.start_goto("pile", skip=True):
                self._goto_tick(state, send)
            return True
        if not naked:
            self._forget_deathpile()
            self._skip_kit_shops()
            if self._spells_shopped and self._kit_ready():
                self._done_gear()
                return True
        return False

    def _done_gear(self) -> None:
        self.gear_done = True
        self.mode = "hunt"
        self.next_action = "hunt"
        self._await_inv = False
        self._pry_sent = False

    def _gear(self, state: WorldState, send) -> None:
        if self._listed_fight(state) and paths.is_dangerous(state.room):
            self._done_gear()
            return
        if state.blocked:
            state.blocked = False
            self._last_step = ""
            self._cmd(send, "look", state)
            return
        self._note_already_worn(state)
        self._sync_gear_from_inv(state)
        if state.learned or state.spell_skip:
            if state.learned and self._spell_i < len(self._learn):
                self._remember(self._learn[self._spell_i])
            state.learned = False
            state.spell_skip = False
            if self._spell_buying or self._spell_reading:
                self._advance_spell()
        if self._sell_extra(state, send):
            return
        if self._finish_kit_check(state, send):
            return
        if (
            not self._need_kit_check
            and not self._await_inv
            and (self._followed or state.following)
            and not (self._kit_ready() and self._spells_shopped)
        ):
            # Follow steals movement — shop solo, then hunt can rejoin.
            self._cmd(send, "leave", state)
            self._drop_follow(state)
            return
        room = state.room.lower()
        if state.shop_vague:
            state.shop_vague = False
            if self._spell_reading:
                self._advance_spell()
                self.next_action = "shop"
                return
            if self._spell_buying:
                if not self._spell_alt and self._spell_i < len(self._learn):
                    self._spell_alt = True
                    name = self._learn[self._spell_i]
                    self._cmd(send, f"buy {spells.buy_name(name, alt=True)}", state)
                    return
                self._advance_spell()
                self.next_action = "shop"
                return
            if self._wearing:
                self._wearing = False
                self._armour_i += 1
            elif not self._weapon_worn:
                self._weapon_bought = True
            elif not self._torch_bought and paths.is_general_store(room):
                self._torch_bought = True
            self.next_action = "shop"
            return
        if not self._looked:
            self._looked = True
            self._cmd(send, "look", state)
            return
        extra = self._extra_name(state)
        if extra and "village entrance" in room:
            if not self._go(send, "s", state):
                self._cmd(send, "look", state)
            return
        if paths.is_armour_shop(room):
            if not paths.needs_padded(self.klass):
                self._armour_i = len(paths.ARMOUR_ITEMS)
                if not self._go(send, "n", state):
                    self._cmd(send, "look", state)
                return
            self._skip_worn_armour(state)
            if self._armour_i < len(paths.ARMOUR_ITEMS):
                item = paths.ARMOUR_ITEMS[self._armour_i]
                if item in state.worn:
                    self._wearing = False
                    self._armour_i += 1
                    return
                if not self._wearing:
                    self._cmd(send, f"buy {item}", state)
                    self._wearing = True
                    self._await_inv = True
                    self._pry_sent = False
                    return
                self._cmd(send, f"wear {item}", state)
                self._wearing = False
                self._armour_i += 1
                self._await_inv = True
                self._pry_sent = False
                return
            if not self._go(send, "n", state):
                self._cmd(send, "look", state)
            return
        if paths.is_weapon_shop(room):
            if not modules.needs_weapon(self.klass):
                self._weapon_bought = True
                self._weapon_worn = True
                if not self._go(send, "s", state):
                    self._cmd(send, "look", state)
                return
            weapon = modules.starter_weapon(self.klass)
            if not self._weapon_bought:
                self._cmd(send, f"buy {weapon}", state)
                self._weapon_bought = True
                self._await_inv = True
                self._pry_sent = False
                return
            if state.geared or any(
                paths.is_starter_weapon(w, self.klass) for w in state.worn
            ):
                self._weapon_worn = True
            if not self._weapon_worn:
                self._cmd(send, f"wear {weapon}", state)
                self._weapon_worn = True
                self._await_inv = True
                self._pry_sent = False
                return
            if not self._go(send, "s", state):
                self._cmd(send, "look", state)
            return
        if paths.is_general_store(room):
            if not modules.needs_torch(self.race, self.klass):
                self._torch_bought = True
                step = paths.leave_dead_end(state.room, state.exits)
                if not step or not self._go(send, step, state):
                    self._cmd(send, "look", state)
                return
            if not self._torch_bought:
                self._cmd(send, f"buy {modules.starter_light()}", state)
                self._torch_bought = True
                self._await_inv = True
                self._pry_sent = False
                return
            step = paths.leave_dead_end(state.room, state.exits)
            if not step or not self._go(send, step, state):
                self._cmd(send, "look", state)
            return
        if paths.is_spell_shop(room):
            if not self._spells_shopped and self._shop_one_spell(state, send):
                return
            if not self._go(send, "s", state):
                self._cmd(send, "look", state)
            return
        if paths.is_dangerous(room):
            if self._kit_ready() and self._spells_shopped:
                self._done_gear()
                return
            self._walk_kit(state, send)
            return
        if self._kit_ready() and self._spells_shopped:
            self._done_gear()
            return
        if "village entrance" in room:
            if self._armour_i < len(paths.ARMOUR_ITEMS):
                if not self._go(send, "s", state):
                    self._cmd(send, "look", state)
                return
            if not self._weapon_worn:
                if not self._go(send, "n", state):
                    self._cmd(send, "look", state)
                return
            if not self._torch_bought:
                if not self._go(send, "w", state):
                    self._cmd(send, "look", state)
                return
            if not self._spells_shopped:
                if not self._go(send, "w", state):
                    self._cmd(send, "look", state)
                return
            self._done_gear()
            return
        if "narrow path" in room:
            if self._armour_i < len(paths.ARMOUR_ITEMS) or not self._weapon_worn:
                if not self._go(send, "e", state):
                    self._cmd(send, "look", state)
                return
            if not self._torch_bought:
                if not self._go(send, "s", state):
                    self._cmd(send, "look", state)
                return
            if not self._spells_shopped:
                if not self._go(send, "n", state):
                    self._cmd(send, "look", state)
                return
            if not self._go(send, "w", state):
                self._cmd(send, "look", state)
            return
        if "narrow road" in room:
            if (
                self._armour_i < len(paths.ARMOUR_ITEMS)
                or not self._weapon_worn
                or not self._torch_bought
                or not self._spells_shopped
            ):
                if not self._go(send, "e", state):
                    self._cmd(send, "look", state)
                return
            self._done_gear()
            return
        self._walk_kit(state, send)

    def _leader_here(self, state: WorldState) -> bool:
        want = self.leader.lower()
        if not want:
            return False
        extras = self._aka | state.self_names
        for name in (*paths.players_in(state.mobs, extras), *state.mobs):
            low = name.lower()
            if want == low or want in low.split():
                return True
            tagged = paths.party_name(name, extras).lower()
            if tagged and want == tagged:
                return True
        return False

    def _clear_invite(self, state: WorldState) -> None:
        self._got_invite = False
        state.invited_by = ""
        state.join_call_by = ""

    def _sync_party(self, state: WorldState) -> None:
        if state.invited_by:
            self._got_invite = True
            # Stale `_followed` after the leader logged out still looks grouped,
            # so hunt never sends `follow` and chrome sits on "follow Matthew".
            if not state.following:
                if self._followed:
                    self._forget_follow_sent()
                self._followed = False
                self._joined = False
        if state.join_call_by and self._join_call_follow(state.join_call_by, state):
            self._got_invite = True
        if state.following:
            self._joined = True
            self._followed = True
            self._forget_follow_sent()
            self._clear_invite(state)
        if state.left_party:
            state.left_party = False
            if not state.following:
                self._joined = False
                self._followed = False
                self._forget_follow_sent()
                self._reset_party_rank()
                self._clear_invite(state)
        if state.party_rank in party.RANK_CMD:
            self._party_rank = state.party_rank
            self._ranked = True
            if state.party_rank != "back":
                state.backrank = False
        elif state.backrank:
            self._ranked = True
            self._party_rank = "back"
        if state.followers:
            self._invited = True
            if self._leading():
                # Follower-only "You have moved to the back ranks" is not
                # printed to the leader. They followed is enough.
                self._ranked = True
        reason = state.party_fail
        state.party_fail = ""
        if reason == "invite":
            self._joined = False
            self._followed = False
            self._forget_follow_sent()
            self._clear_invite(state)
        if reason == "party":
            self._drop_follow(state)
        for who in state.arrivals:
            key = self._invite_name_key(who, state)
            if key:
                self._invite_skip.discard(key)
        state.arrivals.clear()
        gone = state.not_here.strip()
        state.not_here = ""
        if gone:
            self._note_invite_miss(gone, state)
        ok = state.invite_ok.strip()
        state.invite_ok = ""
        if ok:
            self._note_invite_try(ok, state)
        self._prune_invite_skip(state)
        self._crew.note_area(modules.area_of(state.room) or state.room)
        self._crew.note_follow(state.following, state.followers)
        self._crew.note_rank(self._party_rank)
        self._crew.note_mates(self._rank_members(state))

    def _invite_name_key(self, who: str, state: WorldState) -> str:
        extras = self._aka | state.self_names
        tagged = paths.party_name(who, extras) or who.strip()
        return tagged.strip().lower()

    def _note_invite_try(self, who: str, state: WorldState) -> str:
        """One `invite Name` per sighting. Success or miss both skip retries."""
        key = self._invite_name_key(who, state)
        if key:
            self._invite_skip.add(key)
            self._invite_at[key] = time.monotonic()
        return key

    def _note_invite_miss(self, who: str, state: WorldState) -> None:
        self._note_invite_try(who, state)

    def _prune_invite_skip(self, state: WorldState) -> None:
        here = {self._invite_name_key(who, state) for who in self.room_players(state)}
        self._invite_skip = {key for key in self._invite_skip if key in here}

    def _already_invited(self, who: str, state: WorldState) -> bool:
        key = self._invite_name_key(who, state)
        if not key or key not in self._invite_skip:
            return False
        if self._with_me(who, state):
            return True
        at = self._invite_at.get(key, 0.0)
        if time.monotonic() - at >= INVITE_RETRY:
            self._invite_skip.discard(key)
            return False
        return True

    def _send_invite(self, who: str, state: WorldState, send) -> bool:
        extras = self._aka | state.self_names
        name = paths.party_name(who, extras) or who.strip()
        if not name or self._with_me(name, state) or self._already_invited(name, state):
            return False
        self._invited = True
        self._party_at = time.monotonic()
        self._note_invite_try(name, state)
        self._cmd(send, f"invite {name}", state)
        return True

    def _alts_here(self, state: WorldState) -> list[str]:
        extras = self._aka | state.self_names
        found: list[str] = []
        for name in paths.players_in(state.mobs, extras):
            if self._mine(name, state) and not self._me(name) and name not in found:
                found.append(name)
        return found

    def room_players(self, state: WorldState) -> list[str]:
        """PCs standing in this room. Not us, not shopkeepers, not lops."""
        extras = self._aka | state.self_names
        found: list[str] = []
        seen: set[str] = set()
        for raw in paths.players_in(state.mobs, extras):
            who = paths.party_name(raw, extras) or raw.strip()
            if not who or self._me(who):
                continue
            key = who.strip().lower()
            if key in seen:
                continue
            seen.add(key)
            found.append(who)
        return found

    def invite_all_cmds(self, state: WorldState) -> list[str]:
        """One `invite Name` per PC here who is not already following."""
        now = time.monotonic()
        here = self.room_players(state)
        cmds: list[str] = []
        for who in here:
            if self._with_me(who, state):
                continue
            self._invite_at[who.strip().lower()] = now
            cmds.append(f"invite {who}")
        if cmds:
            self._want_invite_all = False
            self._invited = True
            self._party_at = now
            self.next_action = "invite all" if len(cmds) > 1 else cmds[0]
        elif here:
            self._want_invite_all = False
            self.next_action = "already grouped"
        else:
            self.next_action = "no players here"
        return cmds

    def arm_invite_all_look(self, state: WorldState) -> None:
        """Typed invite all with no PCs listed — look, then invite on the reprint."""
        self._want_invite_all = True
        state.look_scan = True
        state.saw_here = False
        self.next_action = "look"

    def _flush_invite_all(self, state: WorldState, send) -> bool:
        if not self._want_invite_all:
            return False
        if state.look_scan:
            self.next_action = "looking"
            return True
        self._want_invite_all = False
        cmds = self.invite_all_cmds(state)
        for line in cmds:
            self._cmd(send, line, state)
        return bool(cmds)

    def join_call_cmds(self, state: WorldState) -> list[str]:
        """Typed `!join`: leader rally only — invitees do not shout back.

        Party leader shouts `!join`. Invitees with F9 on follow that speaker
        (or their inviter). Shouting back starts a join-chain kerfuffle.
        Never turns a local rally into `follow Matthew` / hunt-the-leader.
        """
        if not state.in_realm:
            self.next_action = "join in the realm"
            return []
        # Already following: hear the leader's shout; do not echo !join.
        if (state.following or self._followed) and not (
            self._named_leader() or self._leading() or state.followers
        ):
            self._want_join_call = False
            self.next_action = "leader shouts !join — you follow when you hear it"
            return []
        cmds: list[str] = []
        if self._named_leader() or self._leading() or state.followers:
            cmds.extend(self.invite_all_cmds(state))
        cmds.append(JOIN_CALL)
        self._want_join_call = False
        self.next_action = JOIN_CALL
        return cmds

    def _rally_party(self, state: WorldState, send) -> bool:
        """Hunt-start gather: invite PCs here, then `!join` if anyone can hear.

        Only the configured/named leader arms this — followers never shout.
        """
        if not self._want_join_call:
            return False
        if not self._named_leader():
            self._want_join_call = False
            return False
        for who in self.room_players(state):
            if self._send_invite(who, state, send):
                return True
        hearers = self.room_players(state)
        self._want_join_call = False
        if not hearers:
            return False
        self._cmd(send, JOIN_CALL, state)
        return True

    def _arm_rescue(self, state: WorldState, who: str) -> None:
        extras = self._aka | state.self_names
        self._rescue_who = paths.party_name(who, extras) or who
        self._rescue = "out"

    def _arm_leader_rescue(self, state: WorldState) -> bool:
        """F1 / panic: drag the configured leader if they are mortal."""
        who = state.ally_mortal.strip()
        if not who or self._me(who):
            return False
        if self.leader:
            if not self._same_toon(who, self.leader):
                return False
        elif not self._mine(who, state):
            return False
        self._arm_rescue(state, who)
        return True

    def _note_wound(self, state: WorldState) -> None:
        if state.mortal or state.bleeding:
            self._recovering = True
        who = state.ally_mortal.strip()
        if not who or self._me(who):
            return
        if not self._mine(who, state):
            return
        self._arm_rescue(state, who)

    def _still_need_aid(self, state: WorldState) -> bool:
        who = self._rescue_who
        if not who:
            return False
        if self._me(who):
            return False
        seen = state.ally_mortal.strip()
        return bool(seen) and self._same_toon(seen, who)

    def _drop_follow(self, state: WorldState) -> None:
        state.following = ""
        state.left_party = False
        state.backrank = False
        state.party_rank = ""
        self._joined = False
        self._followed = False
        self._forget_follow_sent()
        self._reset_party_rank()
        self._clear_invite(state)

    def _rescue_tick(self, state: WorldState, send) -> bool:
        """Healthy toon: drag the other home toon out of the pit, aid, then solo."""
        if state.mortal or state.bleeding:
            return False
        if not self._rescue:
            return False
        who = self._rescue_who
        if not who or self._me(who):
            self._clear_rescue()
            return False
        if self._break_after_leave(send, state):
            return True
        if state.afraid:
            state.afraid = False
            self._sitting = False
            self._attacking = ""
            state.in_combat = False
            self._cmd(send, "break", state)
            return True
        if state.drag_fail:
            state.drag_fail = False
            self._drag_on = False
            if self._still_need_aid(state) and not self._in_pit(state):
                self._cmd(send, f"aid {who}", state)
                return True
            self.next_action = "aid"
            return True
        if state.in_combat or self._attacking or self._down(state):
            self._sitting = False
            self._attacking = ""
            state.in_combat = False
            self._cmd(send, "break", state)
            return True
        if state.following or self._followed:
            self._cmd(send, "leave", state)
            self._drop_follow(state)
            return True
        if self._in_pit(state) and self._still_need_aid(state):
            self._drag_on = True
            self._cmd(send, f"drag {who} u", state)
            return True
        if self._still_need_aid(state):
            self._cmd(send, f"aid {who}", state)
            return True
        if self._drag_on or state.dragging:
            self._drag_on = False
            state.dragging = ""
            self._cmd(send, "drag", state)
            return True
        # Aid done. Solo ambush until a real invite — do not camp the road.
        self._clear_rescue()
        return False

    def _gathering_party(self, state: WorldState) -> bool:
        """Wait/invite at rally, the pit, or a unique tile — not mid-corridor catch-up."""
        if self._want_join_call:
            return True
        if self._in_pit(state):
            return True
        if paths.is_farm_drop(self._farm_step(state)):
            return True
        room = state.room or ""
        return not (
            paths.plain_river_street(room)
            or paths.plain_guild_street(room)
            or paths.plain_silver_street(room)
            or paths.plain_temple_street(room)
            or paths.plain_secret_passage(room)
        )

    def _party_pending(self, state: WorldState) -> bool:
        if not self.leader:
            return False
        if self._rescue or state.ally_mortal:
            return False
        self._sync_party(state)
        if self._leading():
            if not self._ready_to_hunt(state):
                return False
            if not self._gathering_party(state):
                return False
            if self._want_join_call and self.room_players(state):
                return True
            alts = self._alts_here(state)
            return bool(alts) and not self._party_ready(state)
        if self._with_leader(state) or self._followed or state.following:
            return bool(self._rank_cmd(state))
        return modules.follow_pending(self._party_facts(state))

    def _joining_leader(self, state: WorldState) -> bool:
        """Invite in flight — do not `d` the pit while `follow` is still landing."""
        if self._leading() or self._with_leader(state):
            return False
        self._refresh_follow_sent(state)
        return modules.follow_pending(self._party_facts(state))

    def _party(self, state: WorldState, send) -> bool:
        if not self.leader:
            return False
        self._sync_party(state)
        if self._leading():
            if not self._ready_to_hunt(state):
                return False
            if self._rally_party(state, send):
                return True
            if not self._gathering_party(state):
                return False
            alts = self._alts_here(state)
            if not alts:
                return False
            for raw in alts:
                if self._send_invite(raw, state, send):
                    return True
            return False
        # At the manhole, don't re-follow while the leader is stocking/diving.
        if (
            self._needs_sewer_torch()
            and self._at_manhole_entry(state)
            and not self._leads_sewer_loop()
        ):
            return False
        if not state.following:
            # Invite only — leader in the room is not a join.
            # `_followed` without `state.following` is a leftover flag.
            if self._rescue or state.ally_mortal:
                return False
            self._refresh_follow_sent(state)
            facts = self._party_facts(state)
            offer = modules.join_action(facts)
            if offer:
                self._followed = False
                self._note_follow_sent(offer.name)
                self._cmd(send, offer.command, state)
                return True
            if modules.follow_pending(facts):
                who = (self._follow_sent_to or self.leader or "leader").strip()
                self.next_action = f"follow {who}"
                return True
            return False
        cmd = self._rank_cmd(state)
        if cmd:
            self._note_rank_sent(cmd, state)
            self._cmd(send, cmd, state)
            self._maybe_party_hunt(state)
            return True
        return False

    def _cast_room_light(self, state: WorldState, send) -> bool:
        """Lighting module: `cast starlight` when the class knows a room-light spell."""
        facts = self._player_facts(state)
        offer = modules.light_action(facts)
        if offer is None:
            return False
        if offer.ask:
            if (
                not facts.in_combat
                and not self._spell_offer
                and not self._want_spell
                and not self._train_hold
            ):
                declined = self._declined_at.get(offer.name)
                if declined is None or (state.level is not None and state.level > declined):
                    self._spell_offer = offer.name
                    self._spell_offer_at = state.level or 0
            return False
        if not self._spell_ready() or not self._have_mana(spells.cost(offer.name), state):
            state.dark = False
            return True
        if state.level is not None and not spells.can_cast(offer.name, state.level):
            return False
        self._last_cast = f"light:{offer.name}"
        self._cast_at = time.monotonic()
        self._cmd(send, offer.command, state)
        self._remember(offer.name)
        self._torch_lit = True
        state.torch_lit = True
        state.dark = False
        self.next_action = "light"
        return True

    def _fix_dark(self, state: WorldState, send) -> bool:
        """Humans (and anyone without night vision) need a lit torch in dark."""
        if self._ninja():
            return False
        if not state.dark:
            return False
        spell_light = bool(modules.known_room_light(self.klass, self._memorized))
        if spell_light and self._torch_lit:
            self._torch_lit = False
            state.torch_lit = False
        if self._cast_room_light(state, send):
            return True
        if spell_light:
            state.dark = False
            return True
        self._sync_torch_est(state)
        self._note_torch_light(state)

        # Torch burned out in the pipes.
        if self._torch_lit:
            self._torch_lit = False
            state.torch_lit = False
            if self._torch_est > 0:
                self._torch_est -= 1

        # Low torches in the dark: night-vision toon leads; torch-needy defers.
        pinch = modules.dark_pinch_leader(
            self.me,
            self.race,
            self.klass,
            default_leader=self._default_leader,
            alts=set(self._aka) | set(self._alts_here(state)),
            torch_est=self._torch_est,
            tried_torch=self._tried_torch,
        )
        if pinch:
            self.leader = pinch
        # Restore default leader when we come out of the dark sewer.
        self.leader = modules.restore_default_leader(
            state.room,
            current=self.leader,
            default_leader=self._default_leader,
        )

        if not self._tried_torch:
            self._tried_torch = True
            # Clear so we do not spam light every tick; a new dark line re-sets.
            state.dark = False
            if self._light_torch(state, send):
                return True
            self.next_action = "light"
            return True
        # Still dark after lighting — restock at the local general store.
        if self._with_leader(state):
            state.dark = False
            self.next_action = "light"
            return True
        self._tried_torch = False
        self._torch_bag_ready = False
        self._sewer_stocked = False
        if paths.is_general_store(state.room):
            state.dark = False
            self._torch_shopping = True
            return self._buy_torch_at_store(state, send)
        self._torch_shopping = True
        if self._walk_store(state, send):
            return True
        state.dark = False
        if self._light_torch(state, send):
            return True
        self.next_action = "light"
        return True

    def _walk_store(self, state: WorldState, send) -> bool:
        """Sticky Giovanni / Newhaven store walk. Farm must not yank west."""
        if not self._torch_shopping and not self._torch_wait_inv:
            return False
        if paths.is_general_store(state.room):
            return self._buy_torch_at_store(state, send)
        self._note_store_progress(state.room)
        step = self.world.step(
            "store",
            state.room,
            state.exits,
            last_step=self._last_step,
            silver_east=self._store_silver_east,
            skiff_i=self._skiff_ts,
        )
        if step:
            self._sitting = False
            if self._go(send, step, state):
                self._mark_store_east(state.room, step)
            self.next_action = "torch"
            return True
        return False

    def _note_skiff_ts(self, room: str) -> None:
        """Reset the Pier → TS count at the square or back in Newhaven."""
        low = (room or "").lower()
        if "town square" in low or low == "fountain":
            self._skiff_ts = 0
            return
        if modules.in_newhaven(low):
            self._skiff_ts = 0
            return
        if self._skiff_ts >= len(paths.SKIFF_TO_SQUARE):
            self._skiff_ts = 0

    def _advance_skiff_ts(self, room: str, step: str) -> None:
        nxt = paths.step_skiff_to_square(room, None, skiff_i=self._skiff_ts)
        if nxt and step == nxt:
            self._skiff_ts += 1

    def _note_store_progress(self, room: str) -> None:
        """Count mid-Silver tiles after Western End (titles repeat)."""
        low = room.lower()
        if (
            "town square" in low
            or "fountain" in low
            or "western end" in low
            or paths.is_general_store(low)
        ):
            self._store_silver_east = 0
            self._store_expect_silver = False
            return
        if "brass" in low and "silver" in low:
            # Overshot — shop front is the next tile west.
            self._store_silver_east = 2
            self._store_expect_silver = False
            return
        if "eastern end" in low and "silver" in low:
            self._store_silver_east = 2
            self._store_expect_silver = False
            return
        if self._store_expect_silver and paths.plain_silver_street(low):
            self._store_silver_east = min(self._store_silver_east + 1, 2)
            self._store_expect_silver = False

    def _mark_store_east(self, room: str, step: str) -> None:
        """After east off Western End / mid-Silver, the next Silver tile counts."""
        if step != "e":
            return
        low = room.lower()
        if "western end" in low or paths.plain_silver_street(low):
            self._store_expect_silver = True

    def _stock_torch_before_sewer(self, state: WorldState, send) -> bool:
        """TS loop: bag check → shop if needed → return → `i` → light → dive.

        Example: human matt leading the sewer grind. Night-vision races
        (Gaunt, dark-elf, …) skip — they can see. Followers skip — the
        leader owns the surface check.
        """
        if not self._needs_sewer_torch() or not self._leads_sewer_loop():
            return False
        self._note_torch_light(state)
        entry = self._at_manhole_entry(state)
        mid_stock = bool(
            self._torch_shopping
            or self._torch_wait_inv
            or self._torch_bag_ready
            or self.next_action in {"torch", "torch ready", "light"}
            or paths.is_general_store(state.room)
        )
        if self._farm_step(state) != "go manhole" and not entry and not mid_stock:
            return False
        # Dive cleared this surface visit.
        if (
            self._sewer_stocked
            and self._torch_lit
            and not self._torch_wait_inv
            and not self._torch_shopping
        ):
            return False

        # Town Square / Fountain: one `i` per visit, then count / light.
        if entry:
            if self._torch_wait_inv:
                if state.inv_seq > self._torch_inv_after:
                    self._torch_wait_inv = False
                    self._torch_checked = True
                    self._sync_torch_est(state)
                else:
                    self.next_action = "torch"
                    return True
            elif not self._torch_checked:
                self._torch_wait_inv = True
                self._torch_inv_after = state.inv_seq
                self._torch_i_prompt = state.prompt_seq
                self._cmd(send, "i", state)
                self.next_action = "torch"
                return True

            self._sync_torch_est(state)
            # Need run torch + exit spare before diving.
            if self._torch_est < paths.TORCH_BAG_MIN:
                self._torch_bag_ready = False
                self._sewer_stocked = False
                self._torch_shopping = True
                self._torch_checked = False  # re-`i` when we return from the shop
                step = self.world.step(
                    "store",
                    state.room,
                    state.exits,
                    last_step=self._last_step,
                    silver_east=self._store_silver_east,
                    skiff_i=self._skiff_ts,
                )
                # From TS the store is east — never wander temple (west).
                if step == "w":
                    step = "e" if "e" in (state.exits or []) else None
                if step and self._go(send, step, state):
                    self._mark_store_east(state.room, step)
                    self.next_action = "torch"
                    return True
                self.next_action = "torch"
                return True

            self._torch_bag_ready = True
            # Lit check right before the manhole — only light if unlit.
            if not self._torch_lit and not state.torch_lit:
                self._sewer_stocked = True
                self._torch_bought = True
                self._torch_shopping = False
                if self._light_torch(state, send):
                    return True
            self._torch_lit = True
            state.torch_lit = True
            self._sewer_stocked = True
            self._torch_bought = True
            self._torch_shopping = False
            self._torch_bag_ready = False
            self.next_action = "torch ready"
            return False

        self._sync_torch_est(state)
        # Off the square with a full bag — walk home to TS (never further west).
        if self._torch_bag_ready or self._torch_est >= paths.TORCH_BAG_MIN:
            self._torch_bag_ready = True
            self._torch_shopping = False
            low = (state.room or "").lower()
            if "temple" in low:
                step = "e"
            elif paths.is_general_store(state.room):
                step = (
                    paths.step_toward_farm(
                        state.room, state.exits, state.level, state.arena_gated
                    )
                    or "n"
                )
            else:
                step = paths.step_toward_farm(
                    state.room, state.exits, state.level, state.arena_gated
                )
            if step == "go manhole":
                # Should be at TS — let the entry branch light/dive next tick.
                self.next_action = "torch"
                return True
            if step and self._go(send, step, state):
                self.next_action = "torch"
                return True
            self.next_action = "torch"
            return True

        self._sewer_stocked = False
        self._torch_shopping = True
        if paths.is_general_store(state.room):
            return self._buy_torch_at_store(state, send)
        self._note_store_progress(state.room)
        step = self.world.step(
            "store",
            state.room,
            state.exits,
            last_step=self._last_step,
            silver_east=self._store_silver_east,
            skiff_i=self._skiff_ts,
        )
        if step and self._go(send, step, state):
            self._mark_store_east(state.room, step)
            self.next_action = "torch"
            return True
        # Already checked and no store walk — hold, do not re-`i`.
        self.next_action = "torch"
        return True

    def _camp(self, state: WorldState, send) -> None:
        if state.scanned or self._busy_swing(state):
            self.next_action = "camping"
            return
        if state.look_scan:
            self.next_action = "camping"
            return
        # Wait for the timed short look; only look after LOOK_GAP if stuck.
        if time.monotonic() - self._sent_at >= LOOK_GAP:
            state.look_scan = True
            state.saw_here = False
            state.saw_see = False
            self._cmd(send, "look", state)
        self.next_action = "camping"

    def _sewer_loop(self, state: WorldState, send) -> bool:
        """Follow the klymacks/Winterhawk sewer tape when the room is clear."""
        if not modules.at_sewer(state.room):
            return False
        if self._followed or self._lops_here(state) or state.in_combat:
            return False
        if not state.scanned or state.look_scan or self._drop_scan:
            return False
        if not self._ready_to_hunt(state):
            return False
        step = self._sewer_path.resolve(self._sewer_i, self._last_step)
        if step == "u":
            # Never climb out while hunting — skip this tape beat.
            self._sewer_i += 1
            step = self._sewer_path.resolve(self._sewer_i, self._last_step)
        if not step or step == "u":
            step = paths.sewer_loop_step(state.exits, self._last_step)
        elif state.exits and step not in state.exits and step not in realm_map.SPECIALS:
            # Tape drifted — fall back to live compass, keep the index moving.
            step = paths.sewer_loop_step(state.exits, self._last_step)
            self._sewer_i += 1
        if not step:
            return False
        self._sitting = False
        if not self._go(send, step, state):
            return False
        self._sewer_i += 1
        self.next_action = "sewer loop"
        return True

    def _graveyard_loop(self, state: WorldState, send) -> bool:
        """East/west ping-pong on the grass. Never n/s into crypts."""
        if self.hunt_run.loop != "graveyard":
            return False
        if not (modules.at_graveyard(state.room) or paths.at_crypt(state.room)):
            return False
        if self._followed or self._lops_here(state) or state.in_combat:
            return False
        if not state.scanned or state.look_scan or self._drop_scan:
            return False
        if not self._ready_to_hunt(state) or self._hurt_ally(state):
            return False
        if paths.at_graveyard_gate(state.room):
            self._grave_i = 0
        step = paths.graveyard_loop_step(
            state.room, state.exits, self._last_step, self._grave_i
        )
        if not step:
            return False
        self._sitting = False
        if not self._go(send, step, state):
            other = "w" if step == "e" else "e"
            if other == step or not self._go(send, other, state):
                return False
            self._grave_i = paths.GRAVE_SPAN if other == "w" else 0
        else:
            self._grave_i += 1
        self.next_action = "graveyard"
        return True

    def _queued_swing_gone(
        self,
        pending: bool,
        aim: str,
        verb: str,
        dead: str,
        state: WorldState,
    ) -> bool:
        """True when a paced `at` / `bs` is aimed at a mob that just died."""
        if not pending or not aim:
            return False
        if verb not in {"attack", "att", "bash", "aa", "bs", "at"}:
            return False
        if dead and paths.same_mob(aim, dead):
            return True
        return not _still_here(state, aim)

    def _aim(self, state: WorldState) -> str | None:
        """Stay on the current fight, or open on a listed farm mob.

        Listing is arrive / Also here / a monster swing — not our echo.
        While following, the leader's fight is the source of truth.
        """
        if self._with_leader(state):
            return self._follow_aim(state)
        if self._attacking:
            held = _still_here(state, self._attacking)
            if held:
                return held
        return _trusted_live(state)

    def _follow_aim(self, state: WorldState) -> str | None:
        """Attack what the leader attacks. Hunt must not pick another lop."""
        lead = _still_here(state, state.ally_aim) if state.ally_aim else None
        if lead:
            return lead
        if self._attacking:
            held = _still_here(state, self._attacking)
            if held:
                return held
        if state.aggro:
            hit = _still_here(state, state.aggro)
            if hit:
                return hit
        species = _farm_species(state)
        if len(species) == 1:
            return _trusted_live(state) or modules.farm_here(state.mobs)
        return None

    def _hunt_live(self, state: WorldState) -> str | None:
        """Swing target for hunt. Following does not fall back to the first lop."""
        live = self._aim(state)
        if live:
            return live
        if self._with_leader(state):
            return None
        return modules.farm_here(state.mobs)

    def _opening(self, state: WorldState) -> bool:
        return self._ninja() and not state.in_combat and (
            self._sneak_wait or self._sneaking or self._sneak_armed or self._sneak_flop
        )

    def _resolve_fight(self, state: WorldState) -> None:
        if state.whiff:
            self._attacking = ""
            self._last_aim = ""
            self._last_verb = ""
            state.whiff = False
            state.in_combat = False
        if state.last_kill:
            dead = state.last_kill
            if not state.scanned:
                state.mobs = paths.without_dead(state.mobs, dead)
            self._attacking = ""
            self._last_aim = ""
            self._last_verb = ""
            self._last_cast = ""
            self._sneaking = False
            self._sneak_wait = False
            self._sneak_armed = False
            self._sneak_block = False
            self._hidden = False
            state.last_kill = ""
            if self._own_ambush(state) and not self._in_pit(state):
                self._need_break = True
            if self._in_pit(state) and not paths.coins_in(state.things):
                # Next spawn prints itself. Do not look-spam after Off.
                self._want_look = False
            # Empty road/pit: wait for the room tick or arrive — not look.
        elif self._attacking and not self._opening(state):
            if not _still_here(state, self._attacking):
                if not state.in_combat or _trusted_live(state):
                    self._attacking = ""
        if state.needs_scan:
            state.needs_scan = False
            self._attacking = ""
            # Arrive / Also here / tick will list the next lop.
        if state.scanned:
            self._want_look = False
        if self._busy_swing(state):
            self._want_look = False

    def _engage_lop(self, state: WorldState, send, live: str) -> bool:
        """Hidden/sneaking → bs. Visible or sneak-fail → attack. Never sn here."""
        if self._party_pending(state) and not self._followed:
            return False
        if not live or self._mine(live, state):
            return False
        self._want_look = False
        self._sneak_wait = False
        self._sneak_armed = False
        self._sneak_block = False
        self._sneak_flop = False
        aim = self._swing_name(live)
        extras = self._aka | state.self_names
        if not aim:
            return False
        if (
            self._mine(aim, state)
            or paths.has_toon(aim, extras)
            or any(paths.is_given_name(word, extras) for word in aim.split())
        ):
            return False
        if not _still_here(state, aim) and not _still_here(state, live):
            return False
        if self._try_heal(state, send):
            return True
        held = self._attacking
        if held and not _still_here(state, held):
            self._attacking = ""
            held = ""
        if (
            not held
            and self._last_aim
            and self._swing_name(self._last_aim).lower() == aim.lower()
            and (_still_here(state, aim) or _still_here(state, live))
        ):
            held = aim.lower()
            self._attacking = held
        if held and not paths.same_mob(held, aim):
            if self._with_leader(state):
                self._attacking = ""
                held = ""
            else:
                if self._try_harm(state, send, modules.attack_name(held)):
                    return True
                self.next_action = f"fighting {held}"
                return True
        if held and paths.same_mob(held, aim) and not self._opening(state):
            if self._need_swing:
                # `get` cancelled auto-combat. Same species is a new swing.
                self._need_swing = False
                return self._strike(send, state, aim)
            if self._try_harm(state, send, aim):
                return True
            if _still_here(state, held):
                self.next_action = f"fighting {aim}"
            else:
                self.next_action = "fighting"
            return True
        if state.in_combat:
            verb = self._swing_verb(state)
            self._attacking = aim.lower()
            self._need_swing = False
            self._pit_fight = True
            self._sneaking = False
            self._sneak_armed = False
            self._sneak_wait = False
            self._hidden = False
            self._cmd(send, f"{verb} {aim}", state)
            return True
        return self._strike(send, state, aim)

    def _pick_coins(self, state: WorldState, send) -> bool:
        """Swoop coins as soon as they hit the ground. `get` breaks auto-combat."""
        if self._snatching_pile(state):
            return False
        if state.mortal:
            return False
        if state.prompt_seq < self._wait_prompt:
            return False
        coins = paths.coins_in(state.things)
        if not coins:
            return False
        self._want_look = False
        coin = coins[0]
        state.things = [t for t in state.things if coin not in t.lower()]
        self._last_aim = ""
        self._need_swing = True
        self._cmd(send, f"get {coin}", state)
        return True

    def _hunt(self, state: WorldState, send) -> None:
        self._refresh_spell_offer(state)
        if paths.is_dangerous(state.room):
            self._in_camp = True
        if self._pick_coins(state, send):
            return
        if self._fix_dark(state, send):
            return
        if not self._with_leader(state) and self._walk_store(state, send):
            return
        # Pick up the party at TS before torch stock / manhole.
        if self._party(state, send):
            return
        if self._stock_torch_before_sewer(state, send):
            return
        if self._swing_first(state, send):
            return
        if self._run_ambush_boot(state, send):
            return
        if self._ask_health(state, send):
            return
        if self._combat_flee(state, send):
            return
        if self._party_cadence(state, send):
            return
        if self._try_heal(state, send):
            return
        if self._try_party_mana_rest(state, send):
            return
        if self._holding_party_heal(state):
            self.next_action = "heal"
            return
        if self._holding_sneak(state) and not self._lops_here(state):
            self.next_action = "ambush"
            return
        if self._try_bless(state, send):
            return
        self._refresh_spell_offer(state)
        if self._go_get_gear(state, send):
            return
        if self._go_get_spell(state, send):
            return
        if self._try_read_scroll(state, send):
            return
        if self._retry_after_flop(state, send):
            return
        if self._party_sneak(state, send):
            return
        if self._party_pending(state):
            live_wait = self._hunt_live(state)
            if not (self._followed and live_wait):
                self.next_action = "party"
                return
        if self._panic_until and time.monotonic() < self._panic_until:
            self.next_action = "panic"
            return
        live = self._hunt_live(state)
        if self._should_leave_to_sneak(state):
            self._travel(send, "u", state)
            return
        if live and self._opens_swing(state) and self._engage_lop(state, send, live):
            self._evaded = False
            return
        if live and not self._opens_swing(state):
            self.next_action = "aa off"
            return
        if state.look_scan:
            # In-flight room tick / title — never pile another look. Known farm
            # walks (Secret Passage, Skali) still move; empty unknown waits.
            if not live and not self._farm_step(state):
                self.next_action = "looking"
                return
        elif self._need_look(state):
            self._want_look = False
            if self._ask_look(send, state):
                return
        elif self._wait_room_tick(state, send):
            return
        if self._inout() and self._ambush_out:
            self._ambush_out = False
            self._sneaking = False
            if self._in_pit(state) and not self._lops_here(state):
                # Empty pit: `sn` here. Do not walk off just to sneak.
                self._attacking = ""
        if self.pvp and state.pvp_hit and not self._mine(state.pvp_hit, state):
            aim = self._swing_name(state.pvp_hit)
            if not aim:
                return
            if self._attacking == aim.lower():
                self.next_action = f"fighting {aim}"
                return
            self._attacking = aim.lower()
            state.in_combat = True
            self._cmd(send, f"{self._swing_verb(state)} {aim}", state)
            return
        if (state.in_combat or self._attacking) and (
            self._in_pit(state) or self._lops_here(state)
        ):
            self.next_action = "fighting"
            if time.monotonic() - self._sent_at > 20:
                self._attacking = ""
                state.in_combat = False
            return
        if not self._in_pit(state) and not self._lops_here(state):
            self._attacking = ""
            if state.in_combat and not self._evaded:
                # Left the fight room — break so combat is actually off.
                self._need_break = True
                if self._break_after_leave(send, state):
                    return
                self.next_action = "fighting"
                return
            # Evade / road / solo empty room: sn now. Party sneak ran earlier.
            may_sneak = self._own_ambush(state) and (
                self._evaded or self._arena_drop(state) or not self._followed
            )
            if may_sneak and self._setup_sneak(send, state):
                self._evaded = False
                return
        self._evaded = False
        self._attacking = ""
        if self._pick_coins(state, send):
            return
        if self._want_look:
            if self._setup_sneak(send, state):
                return
            self._want_look = False
            if self._ask_look(send, state):
                return
        if self._go_train(state, send):
            return
        if self._in_pit(state):
            leftover = self._hunt_live(state)
            if leftover and self._opens_swing(state) and self._engage_lop(
                state, send, leftover
            ):
                return
            if not self._with_leader(state) and (
                paths.wants_silvermere_farm(
                    state.level, state.room, state.arena_gated
                )
                and modules.in_newhaven(state.room)
                and not self._lops_here(state)
            ):
                step = self._farm_step(state)
                if step and self._go(send, step, state):
                    return
            # Named hall that isn't the farm — walk out, do not look-sit.
            if (
                not self._with_leader(state)
                and not modules.at_farm(state.room)
                and not modules.at_sewer(state.room)
            ):
                step = self._farm_step(state)
                if step and self._go(send, step, state):
                    return
            if state.look_scan:
                self.next_action = "looking"
                return
            if self._wait_room_tick(state, send):
                return
            if self._drop_scan and self._ask_look(send, state):
                return
            if self._try_heal(state, send):
                return
            if self._try_party_mana_rest(state, send):
                return
            if self._holding_party_heal(state):
                self.next_action = "heal"
                return
            # Sewers: walk a clockwise pipe loop. Newhaven pit still camps.
            if self._sewer_loop(state, send):
                return
            if self._graveyard_loop(state, send):
                return
            if (
                self._ninja()
                and self._stealth_moves()
                and not self._lops_here(state)
            ):
                if self._down(state) and self._ready_to_hunt(state):
                    self._sitting = False
                    self._cmd(send, "break", state)
                    return
                if self._stealthed() or self._sneak_armed or self._sneak_wait:
                    self.next_action = "ambush"
                    return
                if self._setup_sneak(send, state):
                    return
                if self._can_sneak_here(state) or self._ready_to_hunt(state):
                    self.next_action = "ambush"
                    return
                # Guards / busy-fail: do not idle on sn. Fall through and walk.
            # Full HP: spell-weapon casters (Audrey) wait for the next lop —
            # rest-camping for MA was a break/rest loop. Paladins still sit
            # to refill bless mana.
            if self._healed(state) and (
                self._mana_ready(state) or self._spell_is_weapon()
            ):
                self.next_action = "waiting"
                return
            if not self._sitting:
                if self._sit(send, state):
                    self.mode = "rest"
                    return
                self.next_action = "looking" if state.look_scan else "waiting"
            else:
                self.mode = "rest"
                self.next_action = "healing" if not self._healed(state) else "waiting"
            return
        if (
            self._own_ambush(state)
            and not self._wounded_bleeding(state)
            and not self._lops_here(state)
            and self._arena_drop(state)
        ):
            if self._setup_sneak(send, state):
                return
            drop = self._farm_step(state)
            if self._sneak_ready() or not self._can_sneak_here(state):
                self._travel(send, drop or "d", state, sneak=False)
                return
            return
        if self._with_leader(state):
            self.next_action = f"follow {state.following or self.leader or 'leader'}"
            return
        if not self._in_pit(state) and not self._ready_to_hunt(state):
            if self._try_heal(state, send):
                return
            self.mode = "rest"
            self._rest(state, send)
            return
        if state.blocked:
            listed = [x.lower() for x in (state.exits or [])]
            if "n" in listed and paths.gy_gate_never_south(state.room, None):
                # Bash already opened the latch. Walk n; do not look-fail it shut.
                state.blocked = False
            else:
                state.blocked = False
                self._in_camp = True
                if state.arena_gated or (
                    self._last_step == "d"
                    and paths.wants_silvermere_farm(
                        state.level, state.room, state.arena_gated
                    )
                ):
                    state.arena_gated = True
                    if "d" in state.exits:
                        state.exits = [x for x in state.exits if x != "d"]
                    self._last_step = ""
                elif self._last_step == "d" or not state.room or "road" in state.room.lower():
                    state.room = "Newhaven, Arena"
                    state.exits = [x for x in state.exits if x != "d"]
                else:
                    dead = self.compass.pending or self._last_step
                    if paths.is_unlatch_step(dead):
                        dead = paths.unlatch_dir_short(dead) or dead
                    if dead in state.exits:
                        state.exits = [x for x in state.exits if x != dead]
                    self._last_step = ""
                    if self.compass.pending:
                        self.compass._fail()
                    self.next_action = "looking"
                    if not self._busy_swing(state):
                        self._cmd(send, "look", state)
                    return
        if self._torch_shopping or self._torch_wait_inv:
            self.next_action = "torch"
            return
        if self._in_camp and paths.is_dangerous(state.room):
            if self._try_heal(state, send):
                return
            if self._holding_party_heal(state):
                self.next_action = "heal"
                return
            if self._try_party_mana_rest(state, send):
                return
            if self._sewer_loop(state, send):
                return
            if self._graveyard_loop(state, send):
                return
            self._camp(state, send)
            return
        if self._in_camp:
            if self._walk_out(state, send):
                return
            if state.exits and not modules.at_farm(state.room):
                self.next_action = "waiting"
                return
            self._camp(state, send)
            return
        if self._walk_out(state, send):
            return
        self.next_action = "waiting"
