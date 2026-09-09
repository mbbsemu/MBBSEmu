"""Catalog + module runtime: owl will, starlight light, level-up offers."""

from __future__ import annotations

from pathlib import Path

from . import modules, spells
from .modules import PlayerFacts
from .parse import parse_line


def test_catalog_loads_playable_spells_from_wcc_and_book() -> None:
    book = modules.catalog()
    assert "starlight" in book
    assert "way of the owl" in book
    assert "bless" in book
    star = book["starlight"]
    assert star.room_light
    assert star.short == "star"
    assert star.mana == 4
    assert star.level == 1
    assert "druid" in star.classes and "ranger" in star.classes
    assert star.shop_item == "scroll of starlight"
    assert star.learn_where == "Rayth"
    assert not star.auto_shop
    assert "shimmering light" in " ".join(star.on_messages)
    assert "starlight spell fades" in " ".join(star.off_messages)
    owl = book["way of the owl"]
    assert owl.verb == "invoke"
    assert owl.buff_flag == "willed"
    assert owl.level_up
    assert not owl.shop_item
    assert owl.learn_where == ""
    assert "mystic" in owl.classes
    illu = book["illuminate"]
    assert illu.kind == "light"
    assert not illu.room_light
    assert not illu.level_up


def test_may_use_owl_false_when_willed() -> None:
    facts = PlayerFacts(klass="mystic", level=3, known=frozenset({"way of the owl"}))
    assert modules.may_use("way of the owl", facts)
    up = PlayerFacts(
        klass="mystic",
        level=3,
        known=frozenset({"way of the owl"}),
        willed=True,
    )
    assert not modules.may_use("way of the owl", up)


def test_next_learn_skips_owl_level_up_only() -> None:
    """Owl unlocks on train — never a learn/shop/goto offer."""
    facts = PlayerFacts(klass="mystic", level=2, known=frozenset())
    assert modules.next_learn(facts) == ""
    dinged = PlayerFacts(klass="mystic", level=3, known=frozenset())
    assert modules.next_learn(dinged) == ""
    have = PlayerFacts(
        klass="mystic",
        level=3,
        known=frozenset({"way of the owl", "way of the swan"}),
    )
    assert modules.next_learn(have) == ""
    assert modules.learn_offer(dinged) is None


def test_next_learn_offers_starlight_after_shop_list() -> None:
    empty = PlayerFacts(klass="druid", level=1, known=frozenset())
    assert modules.next_learn(empty) == "vine strike"
    ready = PlayerFacts(
        klass="ranger",
        level=1,
        known=frozenset({"vine strike", "mend"}),
    )
    assert modules.next_learn(ready) == "starlight"
    sherry = PlayerFacts(
        klass="druid",
        level=1,
        known=frozenset({"vine strike", "mend", "starlight"}),
    )
    assert modules.next_learn(sherry) == ""


def test_light_action_cast_or_offer_or_none() -> None:
    dark_known = PlayerFacts(
        klass="druid",
        level=1,
        known=frozenset({"starlight"}),
        dark=True,
    )
    cast = modules.light_action(dark_known)
    assert cast and not cast.ask
    assert cast.command == "cast starlight"
    dark_missing = PlayerFacts(
        klass="ranger",
        level=1,
        known=frozenset({"vine strike"}),
        dark=True,
    )
    offer = modules.light_action(dark_missing)
    assert offer and offer.ask and offer.name == "starlight"
    paladin = PlayerFacts(klass="paladin", level=4, known=frozenset(), dark=True)
    assert modules.light_action(paladin) is None
    lit = PlayerFacts(
        klass="druid",
        level=1,
        known=frozenset({"starlight"}),
        dark=True,
        lit=True,
    )
    assert modules.light_action(lit) is None



def test_auto_play_profiles_are_module_owned() -> None:
    """Hunt launch flags come from modules.party — not play-wg name lists."""
    assert modules.wants_auto_play("klymacks")
    assert modules.wants_auto_play("matt")
    assert modules.wants_auto_play("ryan")
    assert modules.wants_auto_play("robald")
    assert modules.wants_auto_play("sysop")
    assert not modules.wants_auto_play("alex")
    assert not modules.wants_auto_play("kevin")
    assert not modules.wants_auto_play("new")


def test_thin_login_campaign_defaults() -> None:
    """Missing party_leader / spells / aa → modules defaults, not json."""
    assert modules.campaign_leader({}) == modules.DEFAULT_LEADER
    assert modules.campaign_leader({"given": "Alex"}) == modules.DEFAULT_LEADER
    assert modules.campaign_leader({"party_leader": ""}) == ""
    assert modules.campaign_leader({"party_leader": "Ryan"}) == "Ryan"

    from .brain import Brain
    from . import spells

    thin = Brain(allowed=True, me="alex Alex", klass="mage", race="gaunt one")
    assert thin.leader == ""  # Brain itself stays empty until bbs_client fills
    assert thin.aa  # mage bash aa from roles when aa=None
    assert "magic missile" in thin._spells
    assert thin._learn == spells.shop_spells("mage")

    lead = Brain(
        allowed=True,
        me="matt Matthew",
        klass="paladin",
        party_leader=modules.campaign_leader({}),
    )
    assert lead._named_leader()
    assert modules.class_rank("warrior") == "front"
    assert modules.class_rank("ninja") == "back"
    assert modules.desired_rank("warrior", "kevin", "") == "front"


def test_wg_roster_thin_body_identity_only() -> None:
    import importlib.util

    root = Path(__file__).resolve().parents[1]
    spec = importlib.util.spec_from_file_location(
        "wg_roster", root / "scripts" / "wg_roster.py"
    )
    assert spec and spec.loader
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    body = mod.thin_body(
        user="alex",
        given="Alex",
        klass="mage",
        race="gaunt one",
        password="x",
    )
    assert set(body) == {
        "username",
        "password",
        "given",
        "class",
        "race",
        "auto_login",
        "pvp",
    }
    for key in mod._BEHAVIOR_KEYS:
        assert key not in body


def test_shop_spells_still_skip_starlight() -> None:
    assert spells.shop_spells("druid") == ["vine strike", "mend"]
    assert spells.shop_spells("ranger") == ["vine strike", "mend"]
    assert spells.command("starlight") == "cast starlight"
    assert spells.min_level("starlight") == 1
    assert spells.cost("starlight") == 4
    assert spells.self_only("starlight")
    assert not modules.can_shop("way of the owl")
    assert modules.can_shop("starlight")
    assert modules.learn_where("way of the owl") == ""


def _join_facts(
    me: str,
    *,
    klass: str = "",
    invited: str = "",
    room: tuple[str, ...] = (),
    scanned: bool = False,
    follow_sent: str = "",
    following: str = "",
    leader: str = "Matt",
    auto_join: bool = True,
    in_realm: bool = True,
) -> modules.PartyFacts:
    return modules.PartyFacts(
        me=me,
        klass=klass,
        leader=leader,
        auto_join=auto_join,
        in_realm=in_realm,
        following=following,
        invited_by=invited,
        room_pcs=room,
        room_scanned=scanned,
        follow_sent_to=follow_sent,
    )


def test_sherry_invite_rhiannon_and_robald_follow_once() -> None:
    """Same-room Sherry invite: each window sends `follow Sherry` once."""
    line = "Sherry has invited you to follow her."
    ev = parse_line(line)
    assert ev and ev["kind"] == "invited" and ev["name"] == "Sherry"
    for me, klass in (("rhiannon", "mystic"), ("robald", "mystic")):
        facts = _join_facts(
            me, klass=klass, invited="Sherry", room=("Sherry",), scanned=True
        )
        offer = modules.join_action(facts)
        assert offer and offer.command == "follow Sherry"
        again = _join_facts(
            me,
            klass=klass,
            invited="Sherry",
            room=("Sherry",),
            scanned=True,
            follow_sent="Sherry",
        )
        assert modules.join_action(again) is None


def test_join_action_reprint_does_not_refollow() -> None:
    facts = _join_facts(
        "rhiannon",
        klass="mystic",
        invited="Sherry",
        room=("Sherry",),
        scanned=True,
        follow_sent="Sherry",
    )
    assert modules.join_action(facts) is None
    assert modules.follow_pending(facts)


def test_sarah_join_once_no_mid_rank() -> None:
    invited = _join_facts(
        "sarah", klass="gypsy", invited="Matt", room=("Matt",), scanned=True
    )
    offer = modules.join_action(invited)
    assert offer and offer.command == "follow Matthew"
    reprint = _join_facts(
        "sarah",
        klass="gypsy",
        invited="Matt",
        room=("Matt",),
        scanned=True,
        follow_sent="Matt",
    )
    assert modules.join_action(reprint) is None
    rank = modules.PartyFacts(
        me="sarah",
        klass="gypsy",
        leader="Matt",
        following="Matt",
        followed=True,
        room_pcs=("Matt",),
        room_scanned=True,
    )
    row = modules.rank_action(rank)
    assert row and row.command == "backr"
    assert row.name != "mid"
    once = modules.PartyFacts(
        me="sarah",
        klass="gypsy",
        leader="Matt",
        following="Matt",
        followed=True,
        ranked=True,
        party_rank="back",
        rank_sent="backr",
        room_pcs=("Matt",),
        room_scanned=True,
    )
    assert modules.rank_action(once) is None
    crowded = modules.PartyFacts(
        me="sarah",
        klass="gypsy",
        leader="Matt",
        following="Matt",
        followed=True,
        ranked=True,
        party_rank="back",
        rank_sent="backr",
        room_pcs=("Matt", "klymacks", "Alex", "Ryan"),
        room_scanned=True,
    )
    assert modules.rank_action(crowded) is None


def test_invite_not_in_room_ignored() -> None:
    facts = _join_facts(
        "rhiannon",
        klass="mystic",
        invited="Sherry",
        room=("Robald",),
        scanned=True,
    )
    assert modules.join_action(facts) is None


def test_realm_enter_is_not_room_presence() -> None:
    """Empty Also here after a scan is not presence. Realm-enter ≠ here."""
    empty = _join_facts(
        "rhiannon",
        klass="mystic",
        invited="Sherry",
        room=(),
        scanned=True,
    )
    assert modules.join_action(empty) is None
    pending = _join_facts(
        "rhiannon",
        klass="mystic",
        invited="Sherry",
        room=(),
        scanned=False,
    )
    offer_pending = modules.join_action(pending)
    assert offer_pending and offer_pending.command == "follow Sherry"
    here = _join_facts(
        "rhiannon",
        klass="mystic",
        invited="Sherry",
        room=("Sherry",),
        scanned=True,
    )
    offer = modules.join_action(here)
    assert offer and offer.command == "follow Sherry"


def test_class_rank_mystic_mid_warrior_front() -> None:
    from . import party as P

    assert P.class_rank("mystic") == "mid"
    assert P.class_rank("warrior") == "front"
    assert P.class_rank("gypsy") == "back"
    assert P.class_rank("warlock") == "front"
    assert P.class_rank("mage") == "back"
    assert P.desired_rank("gypsy", "sarah", "") == "back"
    assert P.desired_rank("gypsy", "sarah", "middle") == "mid"
    mystic = modules.PartyFacts(
        me="robald",
        klass="mystic",
        leader="Matt",
        following="Matt",
        followed=True,
    )
    assert modules.rank_action(mystic).command == "midr"
    tank = modules.PartyFacts(
        me="kevin",
        klass="warrior",
        leader="Matt",
        following="Matt",
        followed=True,
    )
    assert modules.rank_action(tank).command == "frontr"
    crowded = modules.PartyFacts(
        me="kevin",
        klass="warrior",
        leader="Matt",
        following="Matt",
        followed=True,
        room_pcs=("Matt", "klymacks", "Ryan", "Alex"),
        room_scanned=True,
    )
    assert modules.rank_action(crowded).command == "frontr"
    assert "mid" not in (modules.rank_action(crowded).name,)
    already = modules.PartyFacts(
        me="kevin",
        klass="warrior",
        leader="Matt",
        following="Matt",
        followed=True,
        ranked=True,
        party_rank="front",
        rank_sent="frontr",
    )
    assert modules.rank_action(already) is None


def test_party_sessions_do_not_cross() -> None:
    """Two Brain/WorldState clients keep separate party follow/area/rank."""
    from .brain import Brain
    from .state import WorldState

    matt = Brain(allowed=True, me="matt Matthew", party_leader="Matt", klass="paladin")
    ryan = Brain(allowed=True, me="ryan Ryan", party_leader="Matt", klass="thief")
    matt_state = WorldState()
    ryan_state = WorldState()
    matt_state.in_realm = True
    ryan_state.in_realm = True
    matt_state.room = "Newhaven, Arena"
    ryan_state.room = "Graveyard"
    matt_state.following = ""
    ryan_state.following = "Matt"
    ryan_state.followers = []
    matt._crew.note_area(modules.area_of(matt_state.room))
    ryan._crew.note_area(modules.area_of(ryan_state.room))
    matt._crew.note_follow("", ())
    ryan._crew.note_follow("Matt", ())
    matt._crew.note_rank("front")
    ryan._crew.note_rank("back")
    assert matt._crew.key() != ryan._crew.key()
    assert matt._crew.area == "newhaven"
    assert ryan._crew.area == "graveyard"
    assert matt._crew.following == ""
    assert ryan._crew.following == "Matt"
    assert matt._crew.rank == "front"
    assert ryan._crew.rank == "back"
    # Mutating Ryan must not touch Matt's session.
    ryan._crew.note_follow("Sherry", ("Alex",))
    ryan._crew.note_area("crypt")
    ryan._crew.note_rank("mid")
    assert matt._crew.following == ""
    assert matt._crew.area == "newhaven"
    assert matt._crew.rank == "front"
    assert ryan._crew.following == "Sherry"
    assert ryan._crew.area == "crypt"
    assert ryan._crew.rank == "mid"


def test_heal_ack_once_when_full() -> None:
    """One !healed when full after !heal. Never a second shout."""
    hurt = modules.PartyFacts(
        me="ryan",
        klass="thief",
        leader="Matt",
        with_leader=True,
        asked_heal=True,
        healed_shouted=False,
        rested_up=False,
        hp_ratio=0.5,
        needs_heal=True,
    )
    assert modules.heal_ack_action(hurt) is None
    full = modules.PartyFacts(
        me="ryan",
        klass="thief",
        leader="Matt",
        with_leader=True,
        asked_heal=True,
        healed_shouted=False,
        rested_up=True,
        hp_ratio=1.0,
        needs_heal=False,
    )
    ack = modules.heal_ack_action(full)
    assert ack and ack.command == modules.HEALED_SAY
    once = modules.PartyFacts(
        me="ryan",
        klass="thief",
        leader="Matt",
        with_leader=True,
        asked_heal=True,
        healed_shouted=True,
        rested_up=True,
        hp_ratio=1.0,
    )
    assert modules.heal_ack_action(once) is None


def test_heal_ask_at_ratio() -> None:
    ready = modules.PartyFacts(
        me="ryan",
        klass="thief",
        leader="Matt",
        with_leader=True,
        can_cast_heal=False,
        asked_heal=False,
        hp_ratio=0.80,
        needs_heal=False,
    )
    ask = modules.heal_ask_action(ready)
    assert ask and ask.command == modules.HEAL_ASK
    caster = modules.PartyFacts(
        me="matt",
        klass="paladin",
        leader="Matt",
        with_leader=True,
        can_cast_heal=True,
        asked_heal=False,
        hp_ratio=0.5,
        needs_heal=True,
    )
    assert modules.heal_ask_action(caster) is None


def test_roles_class_race_single_source() -> None:
    """Front/back, torch, caster/melee cues live in modules.roles — not brain."""
    assert modules.class_rank("warrior") == "front"
    assert modules.class_rank("ninja") == "back"
    assert modules.always_front("paladin")
    assert not modules.always_front("mystic")
    assert modules.is_ninja("ninja")
    assert modules.is_paladin("pal")
    assert modules.punches("mystic")
    assert modules.spell_is_weapon("mage")
    assert not modules.spell_is_weapon("paladin")
    assert modules.kit_style("mage") == "caster"
    assert modules.kit_style("warrior") == "melee"
    assert modules.kit_style("ninja") == "stealth"
    assert modules.kit_style("mystic") == "unarmed"
    assert modules.is_caster_kit("warlock")
    assert not modules.uses_staff("warlock")
    assert "warlock" not in modules.STAFF_CLASSES
    assert modules.is_melee_kit("ranger")
    assert modules.needs_torch("human", "paladin")
    assert not modules.needs_torch("dark-elf", "ninja")
    assert modules.sees_in_dark("gaunt one", "mystic")
    assert modules.opens_swing("ninja", aa=False)
    assert modules.opens_swing("mystic", aa=False)
    assert not modules.opens_swing("paladin", aa=False)
    assert modules.opens_swing("paladin", aa=True)
    assert modules.bless_sort_key("ninja") == 0
    assert modules.bless_sort_key("warrior") == 1
    # party / kit re-export the same tables
    from . import party as P
    from .modules import roles as R

    assert P.CLASS_BIAS is R.CLASS_BIAS
    assert P.FRONT_CLASSES is R.FRONT_CLASSES
    assert modules.starter_weapon("mage") == "quarterstaff"
    # Caster kit / spell-weapon ≠ staff: warlock still shops club.
    assert modules.starter_weapon("warlock") == "club"
    assert modules.starter_weapon("Warlock ") == "club"
    assert modules.starter_weapon("mystic") == "quarterstaff"
    assert modules.starter_weapon("thief") == "club"
    assert modules.starter_weapon("paladin") == "club"


def test_maps_area_and_rest_park_bridge_not_shack() -> None:
    """Rest parks on the creek bridge. Shack / GY walk out; Newhaven sits."""
    assert modules.area_of("Newhaven, Arena") == "newhaven"
    assert modules.area_of("Graveyard, Southern Edge") == "graveyard"
    assert modules.area_of("Sewer Tunnel, Junction") == "sewer"
    assert modules.park_rest_room("Graveyard Entrance")
    assert modules.park_rest_room("Graveyard, Entry")
    assert modules.park_rest_room("A Small Shack")
    assert modules.park_rest_room("Sewer Tunnel")
    assert not modules.park_rest_room("Newhaven, Arena")
    assert not modules.park_rest_room("Graveyard Entrance", pit=True)
    assert modules.at_rest_park("Graveyard Bridge")
    assert modules.at_rest_park("Bridge")
    assert not modules.at_rest_park("A Small Shack")
    assert not modules.at_rest_park("Shack")
    assert not modules.at_rest_park("Bridge Street")
    assert modules.gy_to_bridge_step("Graveyard Entrance") == "sw"
    assert modules.gy_to_bridge_step("Graveyard, Entry") == "sw"
    assert modules.gy_to_bridge_step("Shack") == "sw"
    assert modules.gy_to_bridge_step("Graveyard Bridge") is None
    assert modules.at_graveyard_gate("Graveyard, Entry")
    assert modules.at_gy_shack("A Small Shack")
    assert not modules.at_rest_park("Graveyard, Entry")
    assert modules.farm_arrived("Newhaven, Arena")
    assert modules.at_manhole_entry("Town Square")


def test_kit_shop_goal_and_torch_race() -> None:
    """Class/race kit — not per-toon flags. Human buys torch; gaunt skips."""
    assert modules.starter_weapon("paladin") == "club"
    assert modules.starter_weapon("warlock") == "club"
    assert modules.starter_weapon("mystic") == "quarterstaff"
    assert modules.starter_weapon("thief") == "club"
    assert modules.needs_torch("human", "paladin")
    assert not modules.needs_torch("gaunt one", "mystic")
    assert not modules.needs_torch("dark-elf", "ninja")
    assert modules.shop_goal(armour_i=0) == "armour"
    assert modules.shop_goal(armour_i=99, weapon_worn=False) == "weapons"
    assert modules.shop_goal(armour_i=99, weapon_worn=True, torch_bought=False) == "store"
    assert (
        modules.shop_goal(
            armour_i=99, weapon_worn=True, torch_bought=True, spells_shopped=False
        )
        == "spells"
    )
    assert modules.kit_ready(armour_i=99, weapon_worn=True, torch_bought=True)
    assert modules.sewer_torch_needed("human", "paladin", hunt_torch=True)
    assert not modules.sewer_torch_needed(
        "human", "paladin", hunt_torch=True, room_light_spell=True
    )


def test_dark_pinch_leader_is_nv_role_not_name() -> None:
    """Low torches: night-vision leads; torch-needy defers to an NV alt."""
    nv = modules.dark_pinch_leader(
        "sysop klymacks",
        "dark-elf",
        "ninja",
        default_leader="Matt",
        alts=("matt",),
        torch_est=1,
        tried_torch=True,
    )
    assert nv == "klymacks"
    human = modules.dark_pinch_leader(
        "matt Matthew",
        "human",
        "paladin",
        default_leader="Matt",
        alts=("klymacks",),
        torch_est=1,
        tried_torch=True,
    )
    assert human == "klymacks"
    assert (
        modules.dark_pinch_leader(
            "matt",
            "human",
            "paladin",
            torch_est=5,
            tried_torch=True,
        )
        is None
    )
    assert modules.restore_default_leader(
        "Sewer Tunnel", current="klymacks", default_leader="Matt"
    ) == "klymacks"
    assert modules.restore_default_leader(
        "Town Square", current="klymacks", default_leader="Matt"
    ) == "Matt"


def test_monsters_farm_here_and_bless_priority() -> None:
    assert modules.farm_here(["a giant rat", "Matt"]) == "giant rat"
    assert modules.farm_here(["small guardsman"]) is None
    assert modules.attack_name("nasty acid slime") == "acid slime"
    assert modules.is_trash("giant rat")
    assert modules.still_here(["giant rat", "Matt"], "giant rat") == "giant rat"
    assert modules.still_here(["giant rat"], "lashworm") is None
    assert modules.bless_priority("klymacks") == 0
    assert modules.bless_priority("Ryan") > 0


def test_realm_room_tick_policy_lives_in_modules() -> None:
    """Soft Enter first; look only after soft tries. No look-kick helper."""
    assert modules.LOOK_GAP == 12.0
    assert modules.SOFT_REFRESH_TRIES == 1
    assert modules.REALM_CPR_ROW == 25
    assert modules.realm_cpr_reply() == b"\x1b[25;1R"
    assert modules.room_refresh_cmd(soft_tries_used=0) == ""
    assert modules.room_refresh_cmd(soft_tries_used=1) == "look"
    assert not hasattr(modules, "should_kick_room_tick")
    assert not hasattr(modules, "ROOM_TICK_STALE")


def test_follow_sent_status_module_owned() -> None:
    now = 50.0
    assert (
        modules.follow_sent_status(
            follow_sent_to="Curtis",
            following="Curtis",
            room_scanned=True,
            inviter_present=True,
            sent_at=40.0,
            now=now,
        )
        == "forget_confirmed"
    )
    assert (
        modules.follow_sent_status(
            follow_sent_to="Curtis",
            following="",
            room_scanned=True,
            inviter_present=False,
            sent_at=40.0,
            now=now,
        )
        == "forget_gone"
    )
    assert (
        modules.follow_sent_status(
            follow_sent_to="Curtis",
            following="",
            room_scanned=True,
            inviter_present=True,
            sent_at=now - modules.INVITE_RETRY - 1,
            now=now,
        )
        == "forget_retry"
    )
    assert (
        modules.follow_sent_status(
            follow_sent_to="Curtis",
            following="Matt",
            room_scanned=True,
            inviter_present=True,
            sent_at=40.0,
            now=now,
        )
        == "keep"
    )


def test_gear_f7_action_module_owned() -> None:
    assert (
        modules.gear_f7_action(
            mode="gear",
            gear_done=False,
            kit_ready=True,
            weapon_worn=True,
            armour_done=True,
            needs_torch=False,
        )
        == "promote"
    )
    assert (
        modules.gear_f7_action(
            mode="gear",
            gear_done=False,
            kit_ready=False,
            weapon_worn=True,
            armour_done=True,
            needs_torch=False,
        )
        == "arm_torch"
    )
    assert (
        modules.gear_f7_action(
            mode="gear",
            gear_done=False,
            kit_ready=False,
            weapon_worn=False,
            armour_done=False,
            needs_torch=True,
        )
        == "cancel"
    )
    assert (
        modules.gear_f7_action(
            mode="manual",
            gear_done=False,
            kit_ready=False,
            weapon_worn=False,
            armour_done=False,
            needs_torch=False,
        )
        == "start"
    )


if __name__ == "__main__":
    test_catalog_loads_playable_spells_from_wcc_and_book()
    test_may_use_owl_false_when_willed()
    test_next_learn_skips_owl_level_up_only()
    test_next_learn_offers_starlight_after_shop_list()
    test_light_action_cast_or_offer_or_none()
    test_auto_play_profiles_are_module_owned()
    test_thin_login_campaign_defaults()
    test_wg_roster_thin_body_identity_only()
    test_shop_spells_still_skip_starlight()
    test_sherry_invite_rhiannon_and_robald_follow_once()
    test_join_action_reprint_does_not_refollow()
    test_sarah_join_once_no_mid_rank()
    test_invite_not_in_room_ignored()
    test_realm_enter_is_not_room_presence()
    test_class_rank_mystic_mid_warrior_front()
    test_party_sessions_do_not_cross()
    test_heal_ack_once_when_full()
    test_heal_ask_at_ratio()
    test_roles_class_race_single_source()
    test_maps_area_and_rest_park_bridge_not_shack()
    test_kit_shop_goal_and_torch_race()
    test_dark_pinch_leader_is_nv_role_not_name()
    test_monsters_farm_here_and_bless_priority()
    test_realm_room_tick_policy_lives_in_modules()
    test_follow_sent_status_module_owned()
    test_gear_f7_action_module_owned()
    print("ok")
