from __future__ import annotations

import time

from .parse import (
    events_from_payload,
    harvest_screen,
    keep_party_lf,
    parse_events,
    parse_line,
)
from .paths import (
    attack_name,
    at_sewer,
    extra_starter,
    has_lit_torch,
    inventory_entries,
    inventory_extras,
    inventory_names,
    inventory_worn,
    in_silvermere,
    is_given_name,
    is_player,
    is_general_store,
    is_spell_shop,
    is_armour_shop,
    is_weapon_shop,
    leave_dead_end,
    lop_in,
    occupants_in,
    players_in,
    is_special_step,
    is_trainer,
    needs_padded,
    needs_torch,
    needs_weapon,
    uses_bash_aa,
    in_afterlife,
    is_naked,
    is_coin_item,
    coin_copper,
    purse_copper,
    at_bank,
    on_skiff_run,
    sees_in_dark,
    watchful_room,
    sewer_loop_step,
    step_toward_arena,
    step_toward_farm,
    step_toward_guild,
    step_toward_silvermere,
    step_toward_spell_shop,
    step_toward_store,
)
from .state import SESSION_IDLE, WorldState
from .transcript import Transcript


def test_prompt_and_room() -> None:
    s = WorldState()
    t = Transcript()
    blob = (
        b"The Town Square\r\n"
        b"Obvious exits: north, south, east, west.\r\n"
        b"Also here: a large rat, a town guard.\r\n"
        b"[HP=22]: "
    )
    for line in t.feed(blob):
        ev = parse_line(line)
        if ev:
            s.apply(ev)
    assert s.hp == 22
    assert s.hp_label() == "HP 22/22"
    s.apply({"kind": "prompt", "hp": 17, "max_hp": None})
    assert s.hp == 17
    assert s.max_hp == 22
    assert s.hp_label() == "HP 17/22"
    assert s.room == "The Town Square"
    hall = parse_line("A Dark Hall")
    assert hall == {"kind": "room", "title": "A Dark Hall"}
    assert parse_line("A large rat") is None or parse_line("A large rat").get("kind") != "room"
    assert "n" in s.exits and "s" in s.exits
    assert lop_in(s.mobs) == "rat"
    assert lop_in(["Betram", "filthbug"]) == "filthbug"
    assert is_player("klymacks")
    assert is_player("sysop")
    assert is_player("Matt")
    assert is_player("Kevin")
    assert is_player("Audrey")
    assert not is_player("Meia")
    assert not is_player("Giovanni")
    assert not is_player("Betram")
    assert not is_player("Nathaniel")
    assert not is_player("Rayth")
    assert not is_player("Corwyn")
    assert not is_player("nasty acid slime")
    assert not is_player("nasty lashworm")
    assert players_in(["Kevin", "Ryan", "Klymacks", "Meia"]) == [
        "Kevin",
        "Ryan",
        "Klymacks",
    ]
    assert "Meia" in occupants_in(["Kevin", "Ryan", "Klymacks", "Meia"])
    dart = parse_line("The nasty lashworm darts forward and bites you for 4 damage!")
    assert dart and dart["kind"] == "combat"
    assert "lashworm" in str(dart.get("name")).lower()
    assert "matt" not in str(dart.get("name")).lower()
    assert not is_player("a large rat")
    assert lop_in(["nasty lashworm"]) == "lashworm"
    assert lop_in(["klymacks", "nasty acid slime"]) == "acid slime"
    assert players_in(["klymacks", "a filthbug"]) == ["klymacks"]
    assert "a town guard" in occupants_in(["a town guard", "Matt"])
    assert occupants_in(["a large rat", "a town guard"]) == ["a town guard"]
    assert "small guardsman" in occupants_in(["small guardsman"])
    assert lop_in(["small guardsman"]) is None
    assert occupants_in(["You", "Matt"]) == ["Matt"]
    assert lop_in(["klymacks"]) is None
    assert lop_in(["fierce zombie"]) == "zombie"
    assert lop_in(["a fierce zombie"]) == "zombie"
    assert attack_name("fierce zombie") == "zombie"
    assert not is_player("fierce zombie")
    assert not is_given_name("Zombie")
    # Live WG: prompt/SGR crumb glued onto the combat sentence before parse.
    assert attack_name("37m/MA=16]:The zombie") == "zombie"
    assert attack_name("/MA=16]:The zombie") == "zombie"
    assert attack_name("[HP=84/MA=16]:The zombie") == "zombie"
    assert attack_name("1;37m/MA=16]:zombie") == "zombie"
    junk = parse_line("37m/MA=16]:The zombie swings at you with its arm!")
    assert junk and junk["kind"] == "combat"
    assert attack_name(str(junk.get("name"))) == "zombie"
    assert lop_in([str(junk.get("name"))]) == "zombie"
    assert occupants_in(["Corwyn", "acid slime"]) == ["Corwyn"]
    assert occupants_in(["Coorwyn", "acid slime"]) == ["Coorwyn"]
    assert occupants_in(["acid slime"]) == []
    assert "Coorwyn" in occupants_in(["slimeCoorwyn"])
    glued_here = parse_line("Also here: giant rat Klymacks.")
    assert glued_here and glued_here["kind"] == "also_here"
    assert glued_here["mobs"] == ["giant rat", "Klymacks"]
    comma_here = parse_line("Also here: giant rat, Klymacks.")
    assert comma_here and comma_here["mobs"] == ["giant rat", "Klymacks"]
    assert lop_in(["giant rat Klymacks"]) == "giant rat"
    assert players_in(["giant rat Klymacks"]) == ["Klymacks"]
    stuck = parse_line("Also here: acid slimeKlymacks.")
    assert stuck and stuck["mobs"] == ["acid slime", "Klymacks"]
    mashed = parse_line("Also here: d slimeKlymacks.")
    assert mashed and "Klymacks" in mashed["mobs"]
    assert lop_in(mashed["mobs"]) == "slime"
    assert attack_name("d slimeKlymacks") == "slime"
    assert attack_name("acid slimeKlymacks") == "acid slime"
    assert attack_name("d rat") == "rat"
    assert attack_name("d giant rat") == "giant rat"
    assert attack_name("down rat") == "rat"
    assert attack_name("Also here: d rat") == "rat"
    dir_rat = parse_line("Also here: d rat.")
    assert dir_rat and dir_rat["mobs"] == ["rat"]
    dir_giant = parse_line("Also here: d giant rat.")
    assert dir_giant and dir_giant["mobs"] == ["giant rat"]
    leftover = parse_events("Obvious exits: u.Also here: d rat.")
    here = next(e for e in leftover if e["kind"] == "also_here")
    assert here["mobs"] == ["rat"]
    assert lop_in(here["mobs"]) == "rat"


def test_kill() -> None:
    ev = parse_line("You have killed a large rat!")
    assert ev and ev["kind"] == "killed"
    ev = parse_line("A kobold is dead.")
    assert ev and ev["kind"] == "killed" and "kobold" in ev["name"].lower()
    feet = parse_line("The lashworm falls dead at your feet.")
    assert feet and feet["kind"] == "killed"
    assert "lashworm" in str(feet.get("name")).lower()
    down = parse_line("The lashworm falls down dead at your feet.")
    assert down and down["kind"] == "killed" and "lashworm" in str(down.get("name")).lower()
    drop = parse_line("The lashworm drops dead at your feet.")
    assert drop and drop["kind"] == "killed" and "lashworm" in str(drop.get("name")).lower()
    squeak = parse_line("The giant rat falls to the ground with a tortured squeak.")
    assert squeak and squeak["kind"] == "killed"
    assert "giant rat" in str(squeak.get("name")).lower()
    yelp = parse_line("The carrion beast falls to the ground with a yelp, and is still.")
    assert yelp and yelp["kind"] == "killed"
    assert "carrion beast" in str(yelp.get("name")).lower()


def test_falls_dead_and_combat_off() -> None:
    """Death + Combat Off — glued or split — must both land."""
    feet = "The lashworm falls dead at your feet."
    assert parse_line(feet)["kind"] == "killed"
    assert parse_line("*Combat Off*")["kind"] == "combat_off"
    assert parse_line("*Combat Off")["kind"] == "combat_off"
    assert parse_line("Combat Off")["kind"] == "combat_off"
    for blob in (
        f"{feet}*Combat Off*",
        f"{feet} *Combat Off*",
        f"{feet}*Combat Off",
        f"{feet}*Combat Off*[HP=28]:",
    ):
        kinds = [e["kind"] for e in parse_events(blob)]
        assert "killed" in kinds, blob
        assert "combat_off" in kinds, blob
        kill = next(e for e in parse_events(blob) if e["kind"] == "killed")
        assert "lashworm" in str(kill.get("name")).lower()
    harvested = harvest_screen(f"{feet}*Combat Off*\n[HP=28]:", set())
    kinds = [e["kind"] for e in harvested]
    assert "killed" in kinds
    assert "combat_off" in kinds
    payload = events_from_payload(f"{feet}*Combat Off*\r\n[HP=28]: ".encode("ascii"))
    kinds = [e["kind"] for e in payload]
    assert "killed" in kinds
    assert "combat_off" in kinds
    room = WorldState()
    room.mobs = ["nasty lashworm"]
    room.in_combat = True
    for ev in parse_events(f"{feet}*Combat Off*"):
        room.apply(ev)
    assert not any("lashworm" in m.lower() for m in room.mobs)
    assert room.in_combat is False
    assert attack_name("tack giant rat") == "giant rat"
    assert attack_name("tt giant rat") == "giant rat"
    assert attack_name("ttack acid slime") == "acid slime"
    assert attack_name("attack giant rat") == "giant rat"
    assert attack_name("attacktack giant rat") == "giant rat"
    assert "tack" not in attack_name("tack giant rat")
    assert attack_name("tack giant rat") != "tack giant rat"
    assert attack_name("attack giant rat") != "attack giant rat"
    assert attack_name("fat carrion beast") == "carrion beast"
    assert attack_name("thin giant rat") == "giant rat"
    assert attack_name("fat giant rat") == "giant rat"
    assert attack_name("large lashworm") == "lashworm"
    assert attack_name("small giant rat") == "giant rat"
    assert attack_name("nasty lashworm") == "lashworm"
    assert attack_name("nasty acid slime") == "acid slime"
    assert attack_name("large acid slime") == "acid slime"
    assert attack_name("The large acid slime") == "acid slime"
    assert attack_name("tack fat carrion beast") == "carrion beast"
    assert "fat" not in attack_name("fat carrion beast")
    assert "thin" not in attack_name("thin giant rat")
    assert "large" not in attack_name("large lashworm")
    assert "small" not in attack_name("small giant rat")
    assert "nasty" not in attack_name("nasty lashworm")
    fat_in = parse_line("A fat giant rat creeps into the room")
    assert fat_in and fat_in["kind"] == "arrive"
    assert attack_name(str(fat_in.get("name"))) == "giant rat"
    big_in = parse_line("A large lashworm crawls into the room from nowhere.")
    assert big_in and big_in["kind"] == "arrive"
    assert attack_name(str(big_in.get("name"))) == "lashworm"
    for raw in (
        "fat carrion beast",
        "thin giant rat",
        "nasty lashworm",
        "nasty acid slime",
        "tack giant rat",
        "ttack acid slime",
        "attack giant rat",
        "a large rat",
        "large kobold thief",
        "tack fat carrion beast",
    ):
        want = attack_name(raw)
        assert want, raw
        assert lop_in([raw]) == want
        assert "tack" not in want.split()
        assert "ttack" not in want.split()
        assert "attack" not in want.split()


def test_combat_and_shop() -> None:
    assert parse_line("You swing at a kobold!")["kind"] == "combat"
    ev = parse_line("The filthbug moves to attack you!")
    assert ev and ev["kind"] == "combat" and "filthbug" in str(ev.get("name")).lower()
    assert parse_line("Closed door north") is None
    assert parse_line("The door is closed!")["kind"] == "cannot"
    gate_shut = parse_line("The gate is closed.")
    assert gate_shut and gate_shut["kind"] == "cannot"
    blocked = WorldState()
    blocked.exits = ["n", "s"]
    blocked.apply(gate_shut)
    assert blocked.blocked
    assert parse_line("Your club hits, but glances off its armour!")["kind"] == "combat"
    assert parse_line("You are typing too quickly - command ignored")["kind"] == "flood"
    assert parse_line("Why don't you slow down for a few seconds?")["kind"] == "flood"
    assert parse_line("You'll have to be more specific.")["kind"] == "shop_vague"
    assert parse_line(
        "Please be more specific.  You could have meant any of these:"
    )["kind"] == "shop_vague"
    assert parse_line("The following items are for sale:")["kind"] == "shop"
    assert parse_line("You just bought a club for nothing.")["kind"] == "bought"
    assert parse_line("Newhaven, Armour Shop")["kind"] == "room"
    assert parse_line("Newhaven, Arena")["kind"] == "room"
    spawn = parse_line("A filthbug walks into the room from the north.")
    assert spawn and spawn["kind"] == "arrive" and "filthbug" in str(spawn.get("name")).lower()
    creep = parse_line("A small giant rat creeps into the room from nowhere.")
    assert creep and creep["kind"] == "arrive" and "rat" in str(creep.get("name")).lower()
    ooze = parse_line("A acid slime oozes into the room from nowhere.")
    assert ooze and ooze["kind"] == "arrive" and "slime" in str(ooze.get("name")).lower()
    sneak = parse_line("A small kobold thief sneaks into the room from nowhere.")
    assert sneak and sneak["kind"] == "arrive" and "kobold" in str(sneak.get("name")).lower()
    assert parse_line("Attempting to sneak...")["kind"] == "sneak_try"
    assert parse_line("Sneaking...")["kind"] == "sneak_ok"
    assert parse_line("You don't think you're sneaking.")["kind"] == "sneak_fail"
    assert parse_line("You are no longer hidden.")["kind"] == "sneak_fail"
    assert parse_line("You are no longer sneaking.")["kind"] == "sneak_fail"
    assert parse_line("You are no longer following Matt.")["kind"] == "left"
    blocked = parse_line("You may not sneak right now!")
    assert blocked and blocked["kind"] == "sneak_fail" and blocked.get("reason") == "busy"
    assert parse_line("You make a sound when entering the room!")["kind"] == "sneak_fail"
    assert parse_line("You make a sound as you enter the room.")["kind"] == "sneak_fail"
    hear = parse_line("You hear a sound in the distance.")
    assert not hear or hear.get("kind") != "sneak_fail"
    glued = parse_events("Attempting to sneak...You don't think you're sneaking.")
    assert [ev["kind"] for ev in glued] == ["sneak_try", "sneak_fail"]
    noisy = parse_events("Sneaking...You make a sound when entering the room!")
    assert [ev["kind"] for ev in noisy] == ["sneak_ok", "sneak_fail"]
    swing = parse_line("Klymacks moves to attack acid slime.")
    assert swing and swing["kind"] == "combat"
    assert swing.get("actor") == "Klymacks"
    assert swing.get("aim") == "acid slime"
    assert "name" not in swing
    echo = WorldState()
    echo.apply(parse_line("Klymacks moves to attack nasty acid slime."))
    assert echo.last_actor == "Klymacks"
    assert not lop_in(echo.mobs)
    matt = parse_line("Matt whaps acid slime for 8 damage!")
    assert matt and matt["kind"] == "combat" and matt.get("actor") == "Matt"
    assert matt.get("aim") == "acid slime"
    assert "matt" not in str(matt.get("name")).lower()
    seen = WorldState()
    seen.apply(matt)
    assert "matt" in seen.self_names or seen.last_actor == "Matt"
    assert not seen.pvp_hit
    lead = WorldState()
    lead.following = "Matt"
    lead.apply(parse_line("Matt moves to attack giant rat."))
    assert lead.ally_aim == "giant rat"
    assert not lop_in(lead.mobs)
    lead.apply(parse_line("Matt whaps giant rat for 8 damage!"))
    assert lead.ally_aim == "giant rat"
    lead.apply(parse_line("The giant rat snaps at Matt with its teeth!"))
    assert "rat" in lead.ally_aim.lower()
    sit = parse_line("Matt sits down and meditates.")
    assert sit and sit["kind"] == "rest" and sit.get("actor") == "Matt"
    assert parse_line("A nasty lashworm crawls into the room from nowhere.")["kind"] == "arrive"
    scuttle = parse_line("A filthbug scuttles into the room from nowhere.")
    assert scuttle and scuttle["kind"] == "arrive" and "filthbug" in str(scuttle.get("name")).lower()
    claw = parse_line("The filthbug claws at you, but you dodge out of the way!")
    assert claw and claw["kind"] == "combat" and "filthbug" in str(claw.get("name")).lower()
    swipe = parse_line("The filthbug swipes at you with its claws!")
    assert swipe and swipe["kind"] == "combat" and "filthbug" in str(swipe.get("name")).lower()
    mana = parse_events("[HP=41/MA=8]:")
    assert mana and mana[0]["kind"] == "prompt" and mana[0]["hp"] == 41
    assert mana[0].get("ma") == 8
    juice = WorldState()
    juice.apply(mana[0])
    assert juice.ma == 8
    assert juice.max_ma is None
    assert juice.hp_label() == "HP 41/41  MA 8"
    assert mana[0].get("max_hp") is None
    pool = parse_line("Mana:       3/8")
    assert pool and pool["kind"] == "mana" and pool["ma"] == 3 and pool["max_ma"] == 8
    juice.apply(pool)
    assert juice.ma == 3 and juice.max_ma == 8
    assert juice.hp_label() == "HP 41/41  MA 3/8"
    kai = parse_events("[HP=41/KA=8]:")
    assert kai and kai[0]["kind"] == "prompt" and kai[0].get("ma") == 8
    pool_kai = parse_line("Kai:       3/8")
    assert pool_kai and pool_kai["kind"] == "mana" and pool_kai["ma"] == 3
    assert parse_line("You do not have enough kai!")["kind"] == "cast_fail"
    assert parse_line("You do not have enough mana!")["kind"] == "cast_fail"
    asked = parse_line("Matt has invited you to follow Matt.")
    assert asked and asked["kind"] == "invited" and asked["name"] == "Matt"
    him = parse_line("Matt has invited you to follow him.")
    assert him and him["kind"] == "invited" and him["name"] == "Matt"
    entered = parse_line("Matt just entered the Realm.")
    assert entered and entered["kind"] == "realm_enter" and entered["name"] == "Matt"
    logged = parse_line("Sarah has entered the Realm.")
    assert logged and logged["kind"] == "realm_enter" and logged["name"] == "Sarah"
    login = WorldState()
    login.apply(entered)
    login.apply(logged)
    assert login.mobs == []
    walked = parse_line("Klymacks just arrived from the north.")
    assert walked and walked["kind"] == "arrive" and walked["name"] == "Klymacks"
    below = parse_line("Klymacks just arrived from below.")
    assert below and below["kind"] == "arrive" and below["name"] == "Klymacks"
    walked_in = parse_line("Matt walks into the room from the north.")
    assert walked_in and walked_in["kind"] == "arrive" and walked_in["name"] == "Matt"
    left = parse_line("Klymacks just left to the south.")
    assert left and left["kind"] == "leave" and left["name"] == "Klymacks"
    assert parse_line("A filthbug walks into the room from the north.")["kind"] == "arrive"
    blob = (
        b"Exp: 818 Level: 1 Exp needed for next level: 1482 (2300) [35%]\r\n"
        b"Matt has invited you to follow him.\r\n"
        b"[HP=22]:\r\n"
    )
    kinds = [ev["kind"] for ev in events_from_payload(blob)]
    assert "invited" in kinds
    assert next(ev["name"] for ev in events_from_payload(blob) if ev["kind"] == "invited") == "Matt"
    tagged = parse_line("You are now following Matt")
    assert tagged and tagged["kind"] == "following" and tagged["name"] == "Matt"
    still = parse_line("You are following Matt.")
    assert still and still["kind"] == "following" and still["name"] == "Matt"
    assert parse_line("You are no longer following Matt.")["kind"] == "left"
    they = parse_line("Klymacks started to follow you.")
    assert they and they["kind"] == "followed" and they["name"] == "Klymacks"
    offered = parse_line("You have invited Klymacks to follow you.")
    assert offered and offered["kind"] == "invited" and offered["name"] == "Klymacks"
    assert offered.get("by_me")
    glued_follow = parse_events(
        "You have invited Klymacks to follow you.Klymacks started to follow you."
    )
    assert [ev["kind"] for ev in glued_follow] == ["invited", "followed"]
    assert glued_follow[1].get("name") == "Klymacks"
    pack = WorldState()
    for ev in glued_follow:
        pack.apply(ev)
    assert "Klymacks" in pack.followers
    assert parse_line("You must be invited first!")["kind"] == "party_fail"
    miss = parse_line("You don't see Sarah here!")
    assert miss and miss["kind"] == "not_here" and miss["name"] == "Sarah"
    ghost = WorldState()
    ghost.mobs = ["Sarah", "acid slime"]
    ghost.apply(miss)
    assert ghost.mobs == ["acid slime"]
    assert ghost.not_here == "Sarah"
    solo = parse_line("You are not in a party at the present time.")
    assert solo and solo["kind"] == "left"
    dropped = WorldState()
    dropped.following = "Matt"
    dropped.party_rank = "mid"
    dropped.apply(solo)
    assert dropped.following == ""
    assert dropped.left_party
    assert dropped.party_rank == ""
    assert parse_line("You have moved to the back ranks of your group.")["kind"] == "backrank"
    they_back = parse_line("Ryan just moved to the back rank in your group.")
    assert they_back and they_back["kind"] == "ranked"
    assert they_back.get("name") == "Ryan" and they_back.get("row") == "back"
    they_mid = parse_line("Klymacks just moved to the middle ranks of your group.")
    assert they_mid and they_mid["kind"] == "ranked"
    assert they_mid.get("name") == "Klymacks" and they_mid.get("row") == "mid"
    grouped = WorldState()
    grouped.apply(they_back)
    assert "Ryan" in grouped.followers
    assert grouped.party_rank == ""
    assert not grouped.backrank
    front = parse_line("You have moved to the front ranks of your group.")
    assert front and front["kind"] == "rank" and front["row"] == "front"
    mid = parse_line("You have moved to the middle ranks of your group.")
    assert mid and mid["kind"] == "rank" and mid["row"] == "mid"
    gone = parse_line("The filthbug walks out to the north.")
    assert gone and gone["kind"] == "leave"
    assert parse_line("The nasty giant rat lunges at you!")["kind"] == "combat"
    sword = parse_line("The small kobold thief lunges at you with their shortsword!")
    assert sword and sword["kind"] == "combat" and "kobold" in str(sword.get("name")).lower()
    named = parse_line("The kobold thief lunges at Klymacks with their shortsword!")
    assert named and named["kind"] == "combat"
    assert "kobold" in str(named.get("name")).lower()
    assert "klymacks" not in str(named.get("name")).lower()
    hit_me = WorldState()
    hit_me.apply(named)
    assert not hit_me.pvp_hit
    assert any("kobold" in m.lower() for m in hit_me.mobs)
    assert "klymacks" not in hit_me.ally_hurt
    whacked = parse_line("The nasty giant rat hits Klymacks for 5 damage!")
    assert whacked and whacked["kind"] == "combat"
    assert "rat" in str(whacked.get("name")).lower()
    assert "klymacks" not in str(whacked.get("name")).lower()
    assert str(whacked.get("victim")).lower() == "klymacks"
    assert whacked.get("damage") == 5
    hurt = WorldState()
    hurt.apply(whacked)
    assert "klymacks" in hurt.ally_hurt
    assert hurt.ally_hurt["klymacks"].lower() == "klymacks"
    assert hurt.ally_taken("klymacks") == 5
    bite = parse_line("The nasty lashworm darts forward and bites Klymacks for 4 damage!")
    assert bite and str(bite.get("victim")).lower() == "klymacks" and bite.get("damage") == 4
    whip = parse_line("The nasty acid slime whips Klymacks with its pseudopod for 3 damage!")
    assert whip and str(whip.get("victim")).lower() == "klymacks" and whip.get("damage") == 3
    hurt.apply(whip)
    assert hurt.ally_taken("klymacks") == 8
    hurt.apply({"kind": "leave", "name": "Klymacks"})
    assert "klymacks" not in hurt.ally_hurt
    assert hurt.ally_taken("klymacks") == 0
    you_hit = parse_line("The nasty giant rat hits you for 5 damage!")
    assert you_hit and you_hit["kind"] == "combat"
    assert not you_hit.get("victim")
    me = WorldState()
    me.apply(you_hit)
    assert not me.ally_hurt
    slime_hit = parse_line("The giant rat hits slime for 5 damage!")
    assert slime_hit and slime_hit["kind"] == "combat"
    assert not slime_hit.get("victim")
    you_whap = parse_line("You whap nasty giant rat for 5 damage!")
    assert you_whap["kind"] == "combat" and you_whap.get("dealt") == 5
    assert parse_line("The giant rat falls to the ground with a tortured squeak.")["kind"] == "killed"
    goo = parse_line("The acid slime dissolves into a puddle of bluish goo.")
    assert goo and goo["kind"] == "killed" and "slime" in str(goo.get("name")).lower()
    crumple = parse_line("The filthbug collapses, its legs curling tightly around it.")
    assert crumple and crumple["kind"] == "killed" and "filthbug" in str(crumple.get("name")).lower()
    crit = parse_line("You critically whap filthbug for 23 damage!")
    assert crit and crit["kind"] == "combat" and crit.get("dealt") == 23
    assert lop_in(["filthbug", "large kobold thief"]) == "kobold thief"
    inroom = parse_line("A small carrion beast creeps in the room from nowhere.")
    assert inroom and inroom["kind"] == "arrive" and "carrion" in str(inroom.get("name")).lower()
    snap = parse_line("The small carrion beast snaps at you with its teeth!")
    assert snap and snap["kind"] == "combat" and "carrion" in str(snap.get("name")).lower()
    assert parse_line("*Combat Off*")["kind"] == "combat_off"
    assert parse_line("*Combat Engaged*")["kind"] == "combat"
    lash = parse_line("The acid slime lashes at you, but you dodge!")
    assert lash and lash["kind"] == "combat" and "slime" in str(lash.get("name")).lower()
    whip = parse_line("The nasty acid slime whips you with its pseudopod for 3 damage!")
    assert whip and whip["kind"] == "combat" and "slime" in str(whip.get("name")).lower()
    flail = parse_line("The nasty acid slime flails at you!")
    assert flail and flail["kind"] == "combat"
    assert parse_line("There is no exit in that direction!")["kind"] == "cannot"
    wall = parse_line("You ran into the wall to the southwest.")
    assert wall and wall["kind"] == "cannot" and wall.get("dir") == "sw"
    wall_st = WorldState()
    wall_st.exits = ["ne"]
    wall_st.apply(wall)
    assert wall_st.blocked and wall_st.blocked_dir == "sw"
    drop = parse_line("12 copper drop to the ground.")
    assert drop and drop["kind"] == "drop" and drop["name"] == "copper"
    notice = parse_line("You notice 7 silver nobles, 19 copper farthings here.")
    assert notice and notice["kind"] == "you_see"
    assert any("silver" in t.lower() for t in notice["things"])  # type: ignore[index]
    creep = parse_line("A giant rat creeps into the room from nowhere.")
    assert creep and creep["kind"] == "arrive" and "rat" in str(creep.get("name")).lower()
    assert parse_line("The nasty giant rat lunges at you!")["kind"] != "room"
    fight = WorldState()
    fight.room = "Newhaven, Arena"
    fight.in_combat = True
    fight.apply({"kind": "room", "title": "The nasty giant rat lunges at you!"})
    assert fight.room == "Newhaven, Arena"
    assert parse_line("Encumbrance: 550/2400 - Light [22%]") is None
    vitals = parse_line("Hits: 17/28")
    assert vitals and vitals["kind"] == "hits" and vitals["hp"] == 17 and vitals["max_hp"] == 28
    health = parse_line("Health:    17/28    [60%]")
    assert health and health["kind"] == "hits" and health["hp"] == 17 and health["max_hp"] == 28
    vit = WorldState()
    vit.apply(health)
    assert vit.hp_label() == "HP 17/28"
    assert vit.max_hp_known
    exp_lv = parse_line("Exp: 762 Level: 1 Exp needed for next level: 1538 (2300) [33%]")
    assert exp_lv and exp_lv["kind"] == "level" and exp_lv["level"] == 1
    assert exp_lv.get("exp") == 762 and exp_lv.get("needed") == 1538
    assert exp_lv.get("next") == 2300 and exp_lv.get("pct") == 33
    progress = WorldState()
    progress.hp = 28
    progress.apply(exp_lv)
    assert progress.exp_label() == "EXP 33% 762/2300"
    assert progress.exp_known and not progress.can_train()
    progress.apply({"kind": "experience", "amount": 40})
    assert progress.exp == 802
    assert progress.exp_needed == 1498
    assert progress.exp_pct == 34
    assert progress.exp_label() == "EXP 34% 802/2300"
    official = parse_line("Exp: 802 Level: 1 Exp needed for next level: 1498 (2300) [34%]")
    progress.apply(official)
    assert progress.exp == 802
    assert progress.exp_label() == "EXP 34% 802/2300"
    counted = WorldState()
    counted.hp = 28
    counted.apply(exp_lv)
    counted.apply({"kind": "experience", "amount": 40})
    counted.apply({"kind": "killed", "name": "giant rat"})
    assert counted.exp == 802
    assert not counted.needs_exp()
    assert counted.exp_label() == "EXP 34% 802/2300"
    progress.apply({"kind": "killed", "name": "giant rat"})
    assert progress.has_exp_reading()
    assert not progress.needs_exp()
    assert not progress.exp_stale
    assert progress.exp_label() == "EXP 34% 802/2300"
    progress.apply({"kind": "experience", "amount": 10})
    assert not progress.exp_stale
    assert progress.exp == 812
    double = parse_line(
        "Exp: 4610 Level: 3 Exp needed for next level: 3823 (8433) [54%]"
    )
    assert double and double.get("exp") == 4610 and double.get("next") == 8433
    assert double.get("pct") == 54
    full = WorldState()
    full.hp = 38
    full.apply(double)
    assert full.has_exp_reading()
    assert not full.needs_exp()
    full.apply({"kind": "killed", "name": "giant rat"})
    assert not full.needs_exp()
    full.apply({"kind": "killed", "name": "carrion beast"})
    assert not full.needs_exp()
    assert not any(
        ev.get("kind") == "experience"
        for ev in harvest_screen("You gain 40 experience.", set())
    )
    assert not any(
        ev.get("kind") == "experience"
        for ev in events_from_payload(b"You gain 40 experience.\n")
    )
    ready = WorldState()
    ready.apply(
        {
            "kind": "level",
            "level": 1,
            "exp": 2300,
            "needed": 0,
            "next": 2300,
            "pct": 100,
        }
    )
    assert ready.can_train()
    assert ready.exp_label() == "TRAIN 100%"
    assert is_trainer("Newhaven, Guild")
    assert is_trainer("Paladin Training Room")
    assert is_trainer("Adventurer's Guild, Universal Trainer")
    assert not is_trainer("Adventurer's Guild, Foyer")
    assert not is_trainer("Adventurer's Guild, Main Room")
    assert not is_trainer("Ninja Training Room", "paladin")
    assert is_trainer("Ninja Training Room", "ninja")
    assert not is_trainer("Newhaven, Narrow Road")
    assert not is_trainer("Guild Street, Southern End")
    assert not is_trainer("Intersection of Guild St. & River St.")
    wound = WorldState()
    wound.apply({"kind": "prompt", "hp": 28, "max_hp": None})
    wound.apply({"kind": "prompt", "hp": 17, "max_hp": None})
    assert wound.hp_label() == "HP 17/28"
    only = WorldState()
    only.apply({"kind": "prompt", "hp": 17, "max_hp": None})
    assert only.hp_label() == "HP 17/17"
    glued = parse_events("A giant rat creeps into the room from nowhere.[HP=28]:")
    assert [e["kind"] for e in glued] == ["arrive", "prompt"]
    assert "rat" in str(glued[0].get("name")).lower()
    packed = parse_events(
        "Newhaven, ArenaYou notice 7 silver nobles, 19 copper farthings here."
        "Also here: giant rat."
    )
    kinds = [e["kind"] for e in packed]
    assert "room" in kinds and "you_see" in kinds and "also_here" in kinds
    seen: set[str] = set()
    screen = (
        "Newhaven, Arena\n"
        "A giant rat creeps into the room from nowhere.\n"
        "Also here: giant rat.\n"
        "[HP=28]:\n"
    )
    harvested = harvest_screen(screen, seen)
    assert not any(e["kind"] == "arrive" for e in harvested)
    assert any(e["kind"] == "also_here" for e in harvested)
    assert any(e["kind"] == "room" and e.get("title") == "Newhaven, Arena" for e in harvested)
    missed = WorldState()
    missed.mobs = ["giant rat"]
    missed.empty_if_look_missed({"exits"}, "Obvious exits: up\nAlso here: nasty acid slime.\n")
    assert missed.mobs == ["giant rat"]
    stale = WorldState()
    stale.mobs = ["acid slime"]
    stale.apply({"kind": "room", "title": "Newhaven, Arena"})
    stale.apply({"kind": "exits", "exits": ["u"]})
    stale.apply({"kind": "also_here", "mobs": ["acid slime"]})
    stale.empty_if_look_missed({"exits", "room"})
    assert stale.mobs == ["acid slime"]
    keep = WorldState()
    keep.room = "Newhaven, Arena"
    keep.mobs = ["giant rat"]
    keep.apply({"kind": "room", "title": "Newhaven, Arena"})
    assert keep.mobs == ["giant rat"]
    empty = WorldState()
    empty.mobs = ["giant rat"]
    empty.things = ["7 silver"]
    empty.apply({"kind": "room", "title": "Newhaven, Arena"})
    empty.apply({"kind": "exits", "exits": ["u"]})
    assert empty.mobs == []
    assert empty.things == []
    assert empty.scanned
    here = WorldState()
    here.mobs = ["old corpse"]
    here.apply({"kind": "room", "title": "Newhaven, Arena"})
    here.apply({"kind": "also_here", "mobs": ["giant rat"]})
    here.apply({"kind": "exits", "exits": ["u"]})
    assert here.mobs == ["giant rat"]
    say = parse_line('You say "attack giant rat"')
    assert say and say["kind"] == "said" and say.get("aimed") == "giant rat"
    mashed = parse_line('You say "attack ttack acid slime"')
    assert mashed and mashed["kind"] == "said" and mashed.get("aimed") == "acid slime"
    att_tt = parse_line('You say "att tt giant rat"')
    assert att_tt and att_tt["kind"] == "said" and att_tt.get("aimed") == "giant rat"
    party_say = parse_line('Klymacks says "att tt giant rat"')
    assert party_say is None or party_say.get("kind") != "room"
    spoken = parse_line('You say "a carrion beast"')
    assert spoken and spoken["kind"] == "said" and spoken.get("aimed") == "carrion beast"
    at_said = parse_line('You say "at giant rat"')
    assert at_said and at_said.get("aimed") == "giant rat"
    k_said = parse_line('You say "k kobold thief"')
    assert k_said and k_said["kind"] == "said" and k_said.get("aimed") == "kobold thief"
    ask = parse_line('Klymacks says "heal me"')
    assert ask and ask["kind"] == "heal_ask" and str(ask.get("name")) == "Klymacks"
    comma = parse_line('Klymacks says, "heal me"')
    assert comma and comma["kind"] == "heal_ask"
    please = parse_line('Klymacks says "heal me please"')
    assert not please or please.get("kind") != "heal_ask"
    wrap = parse_line('Klymacks says "heal me pleas"')
    assert not wrap or wrap.get("kind") != "heal_ask"
    old_heal = parse_line('Klymacks says "heal"')
    assert old_heal and old_heal["kind"] == "heal_ask"
    old_say = parse_line('Klymacks says "say heal"')
    assert old_say and old_say["kind"] == "heal_ask"
    health = parse_line('Klymacks says "health"')
    assert not health or health.get("kind") != "heal_ask"
    hea = parse_line('Klymacks says "hea"')
    assert not hea or hea.get("kind") != "heal_ask"
    heard = WorldState()
    heard.in_combat = True
    heard.apply(ask)
    assert "klymacks" in heard.heal_asks
    assert heard.heal_asks["klymacks"] == "Klymacks"
    assert not heard.whiff
    assert heard.in_combat
    own = parse_line('You say "heal me"')
    assert own and own["kind"] == "heal_ask"
    self_say = WorldState()
    self_say.in_combat = True
    self_say.apply(own)
    assert not self_say.whiff
    assert self_say.in_combat
    assert not self_say.heal_asks
    miss = WorldState()
    miss.mobs = ["giant rat"]
    miss.apply({"kind": "said", "aimed": "giant rat"})
    assert miss.mobs == []
    assert miss.whiff
    miss.apply({"kind": "arrive", "name": "giant rat"})
    assert miss.mobs == ["giant rat"]


def test_join_call_say() -> None:
    shout = parse_line('Matt says "!join"')
    assert shout and shout["kind"] == "join_call" and str(shout.get("name")) == "Matt"
    comma = parse_line('Matt says, "!join"')
    assert comma and comma["kind"] == "join_call"
    up = parse_line('Matt says "join up"')
    assert up and up["kind"] == "join_call"
    own = parse_line('You say "!join"')
    assert own and own["kind"] == "join_call" and str(own.get("name")) == "You"
    chatter = parse_line('Matt says "join the hunt"')
    assert not chatter or chatter.get("kind") != "join_call"
    heard = WorldState()
    heard.in_combat = True
    heard.apply(shout)
    assert heard.join_call_by == "Matt"
    assert "Matt" in heard.mobs
    assert not heard.whiff
    assert heard.in_combat
    self_say = WorldState()
    self_say.in_combat = True
    self_say.apply(own)
    assert not self_say.join_call_by
    assert not self_say.whiff


def test_party_rest_heal_tags() -> None:
    rest = parse_line('Klymacks says "!rest"')
    assert rest and rest["kind"] == "rest_call" and str(rest.get("name")) == "Klymacks"
    comma = parse_line('Matt says, "!rest"')
    assert comma and comma["kind"] == "rest_call"
    rested = parse_line('Ryan says "!rested"')
    assert rested and rested["kind"] == "rested" and str(rested.get("name")) == "Ryan"
    assert parse_line('Matt says "!rested"').get("kind") != "rest_call"
    heal = parse_line('Klymacks says "!heal"')
    assert heal and heal["kind"] == "heal_ask"
    healed = parse_line('Klymacks says "!healed"')
    assert healed and healed["kind"] == "healed"
    own_rest = parse_line('You say "!rest"')
    assert own_rest and own_rest["kind"] == "rest_call" and str(own_rest.get("name")) == "You"
    heard = WorldState()
    heard.in_combat = True
    heard.apply(rest)
    assert heard.rest_call_by == "Klymacks"
    heard.apply(rested)
    assert heard.rested_acks["ryan"] == "Ryan"
    heard.apply(heal)
    assert "klymacks" in heard.heal_asks
    heard.apply(healed)
    assert heard.healed_acks["klymacks"] == "Klymacks"
    assert "klymacks" not in heard.heal_asks
    self_say = WorldState()
    self_say.apply(own_rest)
    assert not self_say.rest_call_by
    own_healed = parse_line('You say "!healed"')
    self_say.apply(own_healed)
    assert not self_say.healed_acks


def test_backspace_exits() -> None:
    t = Transcript()
    lines = t.feed(b"Obvious exits: nU\x08orth, eU\x08ast, wX\x08est, dE\x08own\r\n[HP=28]: ")
    evs = [parse_line(x) for x in lines]
    kinds = {e["kind"] for e in evs if e}
    assert "exits" in kinds
    exits = next(e for e in evs if e and e["kind"] == "exits")["exits"]
    assert set(exits) >= {"n", "e", "w", "d"}
    assert leave_dead_end("Newhaven, Healer", ["e"]) == "e"
    assert step_toward_arena("Newhaven, Village Entrance", ["n", "s", "w"]) == "w"
    assert step_toward_arena("Newhaven, Narrow Road", ["n", "e", "w", "d"]) == "d"
    assert step_toward_arena("Newhaven, Guild", ["s"]) == "s"
    assert step_toward_arena("Newhaven, Narrow Road", ["u"]) is None
    assert step_toward_arena("Newhaven, Arena", ["u"]) is None
    assert step_toward_store("Newhaven, Narrow Road") == "e"
    assert step_toward_store("Newhaven, Narrow Path") == "s"
    assert step_toward_store("Newhaven, General Store") is None
    assert step_toward_guild("Newhaven, Arena", ["u"]) == "u"
    assert step_toward_guild("Newhaven, Narrow Road", ["n", "e", "w", "d"]) == "n"
    assert step_toward_guild("Newhaven, Guild", ["s"]) is None
    assert step_toward_guild("Newhaven, Healer", ["e"]) == "e"
    assert step_toward_arena("Newhaven, General Store", ["n"]) == "n"
    assert step_toward_arena("The filthbug moves to attack you!", ["n", "u"]) is None
    closed = parse_line("Obvious exits: closed door north, up.")
    assert closed and closed["kind"] == "exits"
    assert closed["exits"] == ["u"]
    assert closed.get("closed") == ["n"]
    gy = parse_line(
        "Obvious exits: closed gate north, south, east, west"
    )
    assert gy and gy["kind"] == "exits"
    assert gy["exits"] == ["s", "e", "w"]
    assert gy.get("closed") == ["n"]
    t2 = Transcript()
    glued_lines = t2.feed(b"A giant rat creeps into the room from nowhere.[HP=28]:")
    glued_evs = [e for line in glued_lines for e in parse_events(line)]
    assert any(e["kind"] == "arrive" for e in glued_evs)
    assert any(e["kind"] == "prompt" for e in glued_evs)
    t3 = Transcript()
    no_nl = t3.feed(b"An acid slime creeps in the room from nowhere.")
    no_nl_evs = [e for line in no_nl for e in parse_events(line)]
    assert any(e["kind"] == "arrive" for e in no_nl_evs)


def test_mana_pool_vs_prompt() -> None:
    s = WorldState()
    prompt = parse_events("[HP=28/MA=3]:")
    assert prompt and prompt[0]["kind"] == "prompt"
    assert prompt[0]["hp"] == 28
    assert prompt[0].get("ma") == 3
    assert prompt[0].get("max_hp") is None
    assert prompt[0].get("max_ma") is None
    s.apply(prompt[0])
    assert s.ma == 3
    assert s.max_ma is None
    assert s.hp_label() == "HP 28/28  MA 3"
    assert "MA 3/3" not in s.hp_label()
    pool = parse_line("Mana:       3/8")
    assert pool and pool["kind"] == "mana" and pool["ma"] == 3 and pool["max_ma"] == 8
    s.apply(pool)
    assert s.hp_label() == "HP 28/28  MA 3/8"
    full = parse_events("[HP=28/MA=8]:")
    s.apply(full[0])
    assert s.ma == 8 and s.max_ma == 8
    assert s.hp_label() == "HP 28/28  MA 8/8"
    low = parse_events("[HP=24/MA=3]:")
    s.apply(low[0])
    assert s.ma == 3 and s.max_ma == 8
    assert "MA 3/8" in s.hp_label()
    assert "MA 3/3" not in s.hp_label()
    glued = parse_events("Health:    17/28    [60%]Mana:       3/8")
    kinds = [e["kind"] for e in glued]
    assert "hits" in kinds and "mana" in kinds
    both = parse_line("Hits: 17/28  Mana: 3/8")
    assert both and both["kind"] == "hits"
    assert both["hp"] == 17 and both["max_hp"] == 28
    assert both.get("ma") == 3 and both.get("max_ma") == 8
    seen: set[str] = set()
    harvested = harvest_screen("Health:    17/28    [60%]\nMana:       3/8\n", seen)
    assert any(e["kind"] == "mana" and e.get("max_ma") == 8 for e in harvested)
    assert any(e["kind"] == "hits" and e.get("max_hp") == 28 for e in harvested)


def test_mortal_aid_drag() -> None:
    down = parse_line("Matt is mortally wounded.")
    assert down and down["kind"] == "mortal" and down["name"] == "Matt"
    self_down = parse_line("You are mortally wounded.")
    assert self_down and self_down["kind"] == "mortal" and self_down["name"] == "you"
    fall = parse_line("Matt falls to the ground, mortally wounded.")
    assert fall and fall["kind"] == "mortal" and fall["name"] == "Matt"
    assert parse_line("The giant rat falls to the ground with a tortured squeak.")["kind"] == "killed"
    bleed = parse_line("Matt is bleeding.")
    assert bleed and bleed["kind"] == "mortal" and bleed["name"] == "Matt"
    you_bleed = parse_line("You are bleeding to death!")
    assert you_bleed and you_bleed["kind"] == "mortal" and you_bleed["name"] == "you"
    saved = parse_line("You have aided Matt, Matt's wounds are now healing.")
    assert saved and saved["kind"] == "aided" and saved["name"] == "Matt"
    helped = parse_line("Klymacks has aided you.")
    assert helped and helped["kind"] == "aided" and helped["name"] == "you"
    mend = parse_line("Your wounds are now healing.")
    assert mend and mend["kind"] == "aided"
    blocked = parse_line("You may not drag anyone through this exit.")
    assert blocked and blocked["kind"] == "drag_fail"
    assert parse_line("You are too afraid!")["kind"] == "afraid"
    assert parse_line("You are now dragging Matt.")["kind"] == "dragging"
    assert parse_line("You are no longer following anyone.")["kind"] == "left"
    low = parse_events("[HP=-95]:")
    assert low and low[0]["kind"] == "prompt" and low[0]["hp"] == -95
    wound = WorldState()
    wound.apply(down)
    assert wound.ally_mortal == "Matt"
    assert not wound.mortal
    wound.apply(saved)
    assert wound.aided
    assert wound.ally_mortal == ""
    self_hp = WorldState()
    self_hp.apply(low[0])
    assert self_hp.hp == -95
    assert self_hp.hp_label() == "HP -95"
    assert self_hp.mortal
    assert self_hp.bleeding
    self_hp.apply(helped)
    assert self_hp.aided
    assert not self_hp.mortal
    assert not self_hp.bleeding
    self_hp.apply({"kind": "prompt", "hp": -90, "max_hp": None})
    assert self_hp.hp == -90
    assert not self_hp.mortal
    assert self_hp.hp_ratio() is None
    self_hp.max_hp = 28
    self_hp.max_hp_known = True
    assert self_hp.hp_ratio() == 0.0
    assert self_hp.hp_meter_ratio() == 0.0
    assert self_hp.hp_label() == "HP -90/28"
    glued = parse_events("Matt is mortally wounded.You have aided Matt, Matt's wounds are now healing.")
    assert [ev["kind"] for ev in glued] == ["mortal", "aided"]
    killed = WorldState()
    killed.following = "Matt"
    killed.bleeding = True
    killed.mortal = True
    killed.apply({"kind": "death"})
    assert killed.just_died
    assert killed.following == ""
    assert killed.left_party
    assert not killed.mortal
    assert not killed.bleeding
    ghost = WorldState()
    ghost.room = "Temple Healer"
    ghost.apply({"kind": "prompt", "hp": 0, "max_hp": None})
    assert ghost.hp == 0
    assert not ghost.mortal
    assert not ghost.bleeding


def test_train_and_level_forget_maxes() -> None:
    gained = parse_line("You have gained a level!")
    assert gained and gained["kind"] == "trained"
    now = parse_line("You are now level 2.")
    assert now and now["kind"] == "trained" and now["level"] == 2
    assert parse_line("You train for a while...")["kind"] == "trained"
    gained_exp = parse_line("You gain 40 experience.")
    assert gained_exp and gained_exp["kind"] == "experience"
    assert gained_exp.get("amount") == 40
    s = WorldState()
    s.apply({"kind": "hits", "hp": 17, "max_hp": 28})
    s.apply({"kind": "mana", "ma": 3, "max_ma": 8})
    s.apply({"kind": "level", "level": 1})
    assert s.max_hp_known and s.max_ma == 8 and s.level == 1
    assert not s.needs_maxes()
    s.apply({"kind": "level", "level": 2})
    assert s.level == 2
    assert s.trained
    assert not s.max_hp_known
    assert s.max_ma is None
    assert s.needs_maxes()
    assert s.hp_label() == "HP 17/28  MA 3"
    s.apply({"kind": "hits", "hp": 20, "max_hp": 32})
    s.apply({"kind": "mana", "ma": 4, "max_ma": 10})
    assert s.max_hp_known and s.max_ma == 10
    assert not s.needs_maxes()
    s.apply(
        {
            "kind": "level",
            "level": 2,
            "exp": 2300,
            "needed": 0,
            "next": 2300,
            "pct": 100,
        }
    )
    assert s.can_train()
    s.apply({"kind": "trained"})
    assert s.needs_maxes()
    assert s.needs_exp()
    assert not s.can_train()
    assert s.exp_label() == ""
    empty = WorldState()
    assert not empty.needs_maxes()
    empty.hp = 24
    assert empty.needs_maxes()


def test_bless_lucky_and_wear_off() -> None:
    lucky = parse_line("You feel lucky!")
    assert lucky and lucky["kind"] == "buff"
    assert lucky.get("name") == "bless" and lucky.get("on") is True
    assert lucky.get("self") is True
    cast = parse_line("You cast bless on Matt!")
    assert cast and cast["kind"] == "buff"
    assert cast.get("name") == "bless" and cast.get("on") is True
    assert cast.get("target") == "Matt"
    off = parse_line("The effects of bless wear off!")
    assert off and off["kind"] == "buff"
    assert off.get("name") == "bless" and off.get("on") is False
    s = WorldState()
    s.self_names.add("matt")
    s.apply(cast)
    s.apply(lucky)
    assert s.blessed
    s.apply(off)
    assert not s.blessed
    other = WorldState()
    other.self_names.add("matt")
    hit = parse_line("You cast bless on klymacks!")
    assert hit and hit.get("target") == "klymacks"
    other.apply(hit)
    assert not other.blessed
    assert other.ally_has_bless("klymacks")
    fade = parse_line("The effects of bless wear off of klymacks!")
    assert fade and fade.get("target") == "klymacks"
    other.apply(fade)
    assert not other.ally_has_bless("klymacks")
    glued = parse_events("You cast bless on Matt!You feel lucky![HP=49/MA=8]:")
    assert any(e["kind"] == "buff" and e.get("on") for e in glued)
    assert glued[-1]["kind"] == "prompt" and glued[-1].get("ma") == 8
    pack = parse_events(
        "The acid slime dissolves into a puddle of bluish goo."
        "You gain 16 experience."
        "*Combat Off*"
    )
    assert [e["kind"] for e in pack] == ["killed", "experience", "combat_off"]
    swipe = parse_line("You swipe at large acid slime!")
    assert swipe and swipe["kind"] == "combat"
    miss = WorldState()
    miss.mobs = ["large acid slime"]
    miss.in_combat = True
    miss.apply(swipe)
    assert miss.in_combat
    assert not miss.whiff
    flail = parse_line("The large acid slime flails at you!")
    assert flail and flail["kind"] == "combat"
    assert "slime" in str(flail.get("name")).lower()
    assert attack_name(str(flail.get("name"))) == "acid slime"
    ooze = parse_line("A acid slime oozes into the room from nowhere.")
    assert ooze and ooze["kind"] == "arrive" and "slime" in str(ooze.get("name")).lower()


def test_owl_strong_willed_and_wear_off() -> None:
    on = parse_line("You feel strong-willed!")
    assert on and on["kind"] == "buff"
    assert on.get("name") == "way of the owl" and on.get("on") is True
    assert on.get("self") is True
    spaced = parse_line("You feel strong willed!")
    assert spaced and spaced.get("name") == "way of the owl" and spaced.get("on")
    off = parse_line("The effects of way of the owl wear off!")
    assert off and off["kind"] == "buff"
    assert off.get("name") == "way of the owl" and off.get("on") is False
    s = WorldState()
    s.apply(on)
    assert s.willed
    assert not s.blessed
    s.apply(off)
    assert not s.willed
    glued = parse_events("You feel strong-willed![HP=41/KA=8]:")
    assert any(e["kind"] == "buff" and e.get("name") == "way of the owl" for e in glued)
    assert glued[-1]["kind"] == "prompt" and glued[-1].get("ma") == 8


def test_starlight_shimmer_and_fade() -> None:
    on = parse_line("You are surrounded by a shimmering light!")
    assert on == {"kind": "torch_lit"}
    off = parse_line("Your starlight spell fades away.")
    assert off == {"kind": "torch_out"}
    s = WorldState()
    s.apply({"kind": "dark"})
    assert s.dark
    s.apply(on)
    assert s.torch_lit and not s.dark
    s.apply(off)
    assert not s.torch_lit
    glued = parse_events("You are surrounded by a shimmering light![HP=22/MA=4]:")
    assert any(e.get("kind") == "torch_lit" for e in glued)
    dark = parse_line("It is pitch black.")
    assert dark == {"kind": "dark"}


def test_inventory_geared() -> None:
    ev = parse_line("padded pants (Legs), padded boots (Feet), club (Weapon Hand), club")
    assert ev and ev["kind"] == "inventory"
    s = WorldState()
    s.apply(ev)
    assert s.geared
    assert ev.get("items")
    assert extra_starter(list(ev["items"])) == "club"  # type: ignore[arg-type]
    assert ev.get("extras") == ["club"]
    assert extra_starter(s.inventory, s.extras) == "club"
    lit = inventory_entries("You are carrying a torch (lit), a torch.")
    assert ("torch (lit)", False) in lit
    assert ("torch", False) in lit
    assert has_lit_torch(["torch (lit)", "torch"])
    assert not has_lit_torch(["torch", "torch"])
    readied = inventory_entries(
        "You are carrying torch (Readied/77), battle axe (Weapon Hand), torch"
    )
    assert ("torch (lit)", False) in readied
    assert ("torch", False) in readied
    assert ("torch", True) not in readied
    assert has_lit_torch([n for n, _ in readied])
    assert (
        extra_starter(
            [n for n, _ in readied],
            [n for n, w in readied if not w],
            worn=[n for n, w in readied if w],
        )
        is None
    )
    assert parse_line("You light a torch.") == {"kind": "torch_lit"}
    assert parse_line("You already have something lit!") == {"kind": "torch_lit"}
    assert parse_line("Your torch goes out.") == {"kind": "torch_out"}
    assert parse_line("You have been killed!") == {"kind": "death"}
    assert parse_line("MUD Internal Error - Please tell your sysop") is None
    assert parse_line(
        "monbadroom 7402 (214 - fat Templar) is 303:1 vs. 222:1 (50), townsman."
    ) is None
    jammed = parse_events(
        "monbadroom 7402 (214 - fat Templar) is 241:1 vs. 324:1 (50)Obvious exits:\n"
        "north, south, east, west\n"
    )
    assert any(
        e.get("kind") == "exits" and e.get("exits") == ["n", "s", "e", "w"]
        for e in jammed
    )
    stuck = parse_line(
        "Also here: nasty black cat, fat black cat, elite "
        "MUD Internal Error - Please tell your sysop"
    )
    assert stuck and stuck["kind"] == "also_here"
    mobs = [m.lower() for m in stuck["mobs"]]  # type: ignore[index]
    assert "nasty black cat" in mobs
    assert not any("internal" in m or "sysop" in m for m in mobs)


def test_inventory_wrapped_padded_set() -> None:
    text = (
        "You are carrying padded vest (Torso), padded helm (Head), padded pants (Legs),\n"
        "padded boots (Feet), padded gloves (Hands), padded vest, padded helm, padded\n"
        "pants, padded boots, padded gloves\n"
        "You have no keys.\n"
        "Wealth: 0 copper farthings\n"
        "Encumbrance: 800/1920 - Medium [41%]\n"
    )
    evs = [e for e in parse_events(text) if e.get("kind") == "inventory"]
    assert len(evs) == 1
    items = list(evs[0]["items"])  # type: ignore[arg-type]
    extras = list(evs[0]["extras"])  # type: ignore[arg-type]
    worn = list(evs[0]["worn"])  # type: ignore[arg-type]
    assert extras == [
        "padded vest",
        "padded helm",
        "padded pants",
        "padded boots",
        "padded gloves",
    ]
    assert worn == [
        "padded vest",
        "padded helm",
        "padded pants",
        "padded boots",
        "padded gloves",
    ]
    assert extra_starter(items, extras) == "padded vest"
    s = WorldState()
    s.apply(evs[0])
    assert s.extras == extras
    assert "padded gloves" in s.worn
    raw = " ".join(
        [
            "You are carrying padded vest (Torso), padded helm (Head), padded pants (Legs),",
            "padded boots (Feet), padded gloves (Hands), padded vest, padded helm, padded",
            "pants, padded boots, padded gloves",
        ]
    )
    assert inventory_extras(raw) == extras
    assert inventory_worn(raw) == worn

    tr = Transcript()
    first = tr.feed(
        b"You are carrying padded vest (Torso), padded helm (Head), padded pants (Legs),\n"
        b"padded boots (Feet), padded gloves (Hands), padded vest, padded helm, padded\n"
    )
    assert first == []
    rest = tr.feed(b"pants, padded boots, padded gloves\nYou have no keys.\n")
    joined = [line for line in rest if line.lower().startswith("you are carrying")]
    assert joined
    assert "padded pants" in joined[0]
    assert extra_starter(inventory_names(joined[0]), inventory_extras(joined[0])) == "padded vest"
    seen: set[str] = set()
    harvested = [
        e
        for e in harvest_screen(text, seen)
        if e.get("kind") == "inventory"
    ]
    assert harvested
    assert list(harvested[-1]["extras"]) == extras


def test_inventory_stacked_extras() -> None:
    text = (
        "You are carrying padded vest (Torso), padded helm (Head), padded pants (Legs),\n"
        "padded boots (Feet), padded gloves (Hands), 3 padded helm, 4 padded pants, 4\n"
        "padded boots, 4 padded gloves\n"
        "You have no keys.\n"
        "Wealth: 0 copper farthings\n"
        "Encumbrance: 1000/1920 - Medium [52%]\n"
    )
    evs = [e for e in parse_events(text) if e.get("kind") == "inventory"]
    assert len(evs) == 1
    extras = list(evs[0]["extras"])  # type: ignore[arg-type]
    worn = list(evs[0]["worn"])  # type: ignore[arg-type]
    assert extras.count("padded helm") == 3
    assert extras.count("padded pants") == 4
    assert extras.count("padded boots") == 4
    assert extras.count("padded gloves") == 4
    assert "padded vest" not in extras
    assert worn == [
        "padded vest",
        "padded helm",
        "padded pants",
        "padded boots",
        "padded gloves",
    ]
    s = WorldState()
    s.apply(evs[0])
    assert extra_starter(s.inventory, s.extras, worn=s.worn) == "padded helm"


def test_already_worn_gloves() -> None:
    ev = parse_line("You sold padded vest for 0 copper farthings.")
    assert ev is not None
    assert ev.get("kind") == "sold"
    assert ev.get("item") == "padded vest"
    ev = parse_line("You do not have padded gloves left unequipped.")
    assert ev and ev["kind"] == "already_worn"
    assert ev.get("item") == "padded gloves"
    s = WorldState()
    s.apply(ev)
    assert s.already_worn == "padded gloves"
    assert "padded gloves" in s.worn


def test_night_vision_kit_rules() -> None:
    assert sees_in_dark("gaunt one")
    assert sees_in_dark("Gaunt")
    assert sees_in_dark("dark-elf")
    assert not sees_in_dark("human")
    assert not sees_in_dark("halfling")
    assert not needs_torch("gaunt one", "mystic")
    assert not needs_torch("dark-elf", "ninja")
    assert not needs_torch("goblin", "gypsy")
    assert not needs_torch("elf", "bard")
    assert not needs_torch("dwarf", "priest")
    assert needs_torch("human", "mystic")
    assert needs_torch("human", "paladin")
    assert needs_torch("halfling", "thief")
    assert needs_torch("half-ogre", "warrior")
    assert needs_weapon("mystic")
    assert needs_weapon("thief")
    assert needs_weapon("mage")
    assert needs_weapon("warlock")
    assert not uses_bash_aa("mystic")
    assert not uses_bash_aa("mystic", True)
    assert not uses_bash_aa("ninja")
    assert uses_bash_aa("")
    assert uses_bash_aa("paladin")
    assert uses_bash_aa("warrior", True)
    assert not uses_bash_aa("warrior", False)
    assert needs_padded("mage")
    assert needs_padded("mystic")
    assert needs_padded("priest")
    assert needs_padded("paladin")
    assert needs_padded("warlock")
    assert is_naked([], [], [])
    assert is_naked([], ["nothing"], [])
    assert is_naked([], ["9 silver nobles", "27 copper farthings"], [])
    assert is_naked([], ["silver nobles"], ["copper farthings"])
    assert not is_naked(["padded vest"], ["silver nobles"], [])
    assert not is_naked(["padded vest"], [], [])
    assert not is_naked([], ["quarterstaff"], [])
    purse = parse_line("You are carrying 9 silver nobles, 27 copper farthings.")
    assert purse and purse["kind"] == "inventory"
    assert is_naked(purse["worn"], purse["items"], purse["extras"])
    assert is_coin_item("9 silver nobles")
    assert is_coin_item("copper farthings")
    assert is_coin_item("2 runic coins")
    assert not is_coin_item("gold ring")
    assert not is_coin_item("silver longsword")
    assert coin_copper("9 silver nobles") == 90
    assert coin_copper("27 copper farthings") == 27
    assert coin_copper("2 gold crowns") == 200
    assert coin_copper("runic coins") == 10000
    assert purse_copper(["9 silver nobles", "27 copper farthings"]) == 117
    assert at_bank("Bank of Godfrey")
    assert at_bank("bank of godfrey")
    assert not at_bank("Town Square")
    assert not at_bank("Temple Street, Eastern End")
    assert in_afterlife("Halls of the Dead")
    assert in_afterlife("The Halls of the Dead")
    assert in_afterlife("Temple Healer")
    assert not in_afterlife("Graveyard")
    assert not in_afterlife("Sewer Tunnel, Junction")


def test_wealth_and_deposit_lines() -> None:
    wealth = parse_line("Wealth: 117 copper farthings")
    assert wealth == {"kind": "wealth", "copper": 117}
    big = parse_line("Wealth: 1,234 copper farthings")
    assert big and big["copper"] == 1234
    equiv = parse_line("This is equivalent to 90 copper farthings.")
    assert equiv == {"kind": "wealth", "copper": 90}
    put = parse_line("You deposit 117 copper farthings.")
    assert put == {"kind": "deposit", "copper": 117}
    take = parse_line("You withdraw 50 copper farthings.")
    assert take and take["kind"] == "deposit" and take.get("withdraw")
    assert take["copper"] == 50
    fail = parse_line("You must be in a bank to do that.")
    assert fail == {"kind": "deposit", "fail": True}
    s = WorldState()
    s.apply(parse_line("You are carrying 9 silver nobles, 27 copper farthings."))
    assert s.wealth_copper == 117
    s.apply(wealth)
    assert s.wealth_copper == 117
    s.apply(put)
    assert s.deposited
    assert s.wealth_copper == 0
    text = (
        "You are carrying 9 silver nobles, 27 copper farthings\n"
        "You have no keys.\n"
        "Wealth: 117 copper farthings\n"
        "Encumbrance: 800/1920 - Medium [41%]\n"
    )
    kinds = [e["kind"] for e in parse_events(text)]
    assert "inventory" in kinds
    assert "wealth" in kinds
    harvested = [e["kind"] for e in harvest_screen(text, set())]
    assert "wealth" in harvested


def test_nathaniel_steps_south() -> None:
    assert is_weapon_shop("Nathaniel")
    assert step_toward_arena("Nathaniel", ["s"]) == "s"
    assert step_toward_arena("Nathaniel", ["s"]) != "n"
    assert step_toward_arena("Newhaven, Village Entrance", ["s"]) is None


def test_general_store_is_torch_shop() -> None:
    assert is_general_store("Newhaven, General Store")
    assert is_general_store("General Store")
    assert step_toward_store("Newhaven, Village Entrance") == "w"
    assert step_toward_store("Newhaven, Narrow Path") == "s"
    assert step_toward_store("Newhaven, General Store") is None
    assert step_toward_store("Sewer Tunnel, Junction", ["u", "n", "e", "s", "w"]) == "u"
    assert step_toward_store("Town Square", ["n", "s", "e", "w"]) == "e"
    assert step_toward_store(
        "Sovereign Street, Northern End", ["n", "s", "e", "w"]
    ) == "n"
    assert step_toward_store(
        "Silver Street, Western End", ["n", "s", "e", "w"]
    ) == "e"
    assert step_toward_store("Silver Street", ["n", "s", "e", "w"], silver_east=1) == "e"
    assert step_toward_store("Silver Street", ["n", "s", "e", "w"], silver_east=2) == "s"
    assert step_toward_store("Silver Street", ["n", "s", "e", "w"], last_step="s") == "s"
    assert (
        step_toward_store(
            "Intersection of Silver St. & Brass St.", ["n", "e", "s", "w"]
        )
        == "w"
    )
    assert step_toward_farm(
        "Sovereign Street, Northern End", ["n", "s", "e", "w"], 4
    ) == "n"
    assert step_toward_farm(
        "Silver Street, Western End", ["n", "s", "e", "w"], 4
    ) == "w"
    assert in_silvermere("Sovereign Street, Northern End")
    assert in_silvermere("Silver Street, Western End")
    assert step_toward_store("General Store", ["n"]) is None
    assert step_toward_store("Homely Hearth", ["n", "e", "w"]) == "n"


def test_spell_shop_steps() -> None:
    assert is_spell_shop("Newhaven, Spell Shop")
    assert is_spell_shop("Dathalar")
    assert is_spell_shop("Rayth")
    assert not is_spell_shop("Newhaven, General Store")
    assert step_toward_spell_shop("Newhaven, Village Entrance") == "w"
    assert step_toward_spell_shop("Newhaven, Narrow Path") == "n"
    assert step_toward_spell_shop("Newhaven, Narrow Road") == "e"
    assert step_toward_spell_shop("Newhaven, General Store") == "n"
    assert step_toward_spell_shop("Newhaven, Spell Shop") is None
    assert step_toward_spell_shop("Newhaven, Arena", ["u"]) == "u"


def test_silvermere_titles_and_skiff() -> None:
    room = parse_line("Intersection of Guild St. & River St.")
    assert room and room["kind"] == "room"
    assert room["title"] == "Intersection of Guild St. & River St."
    assert in_silvermere("Town Square")
    assert in_silvermere("Docks")
    assert not in_silvermere("Newhaven, Docks")
    assert is_special_step("borrow skiff")
    assert is_special_step("go manhole")
    assert is_special_step("open north")
    assert is_special_step("bash north")
    assert is_special_step("picklock north")
    assert not is_special_step("bash kobold thief")
    from .paths import unlatch_dir, unlatch_dir_short

    gate = "Intersection of River St. & Bridge St."
    assert unlatch_dir("n", "ninja", room=gate) == "picklock north"
    assert unlatch_dir("n", "thief", room="Bridge Street") == "picklock north"
    assert unlatch_dir("n", "paladin", room=gate) == "bash north"
    assert unlatch_dir("n", "ninja") == "picklock north"
    assert unlatch_dir_short("bash north") == "n"
    assert unlatch_dir_short("picklock east") == "e"
    assert step_toward_arena("Town Square", ["n", "s", "e", "w"]) == "n"
    assert step_toward_arena("Sewer Tunnel, Junction", ["u", "n", "e", "s", "w"]) is None
    assert step_toward_arena("Sewer Tunnel, Junction (below TS)", ["u"]) is None
    assert at_sewer("Sewer Tunnel, Junction")
    assert sewer_loop_step(["u", "n", "e", "s", "w"], "") == "n"
    assert sewer_loop_step(["u", "n", "e", "s", "w"], "n") == "e"
    assert sewer_loop_step(["s"], "n") == "s"
    assert sewer_loop_step(["u"], "n") is None
    assert sewer_loop_step(["n", "s"], "n") == "n"
    assert step_toward_silvermere("Newhaven, Village Entrance", ["n", "s", "w", "se"]) == "se"
    assert step_toward_silvermere("Newhaven, Village Entrance", ["n", "s", "w"]) == "se"
    assert step_toward_silvermere("Newhaven, Docks", ["n"]) == "borrow skiff"
    assert step_toward_silvermere("Town Square") is None
    assert on_skiff_run("Newhaven, Forest Path")
    assert watchful_room("Newhaven, Village Entrance")
    assert watchful_room("Gates of Silvermere")
    assert not watchful_room("Graveyard Entrance")
    assert not watchful_room("Newhaven, Narrow Road")
    assert step_toward_farm("Newhaven, Narrow Road", ["n", "e", "w", "d"], 3) == "d"
    assert step_toward_farm("Newhaven, Narrow Road", ["n", "e", "w", "d"], 4) == "e"
    assert step_toward_farm("Newhaven, Arena", ["u"], 4) == "u"
    assert step_toward_farm(
        "Newhaven, Narrow Road", ["n", "e", "w", "d"], None, gated=True
    ) == "e"
    assert step_toward_farm("Newhaven, Forest Path", ["nw", "s"], 4) == "s"
    assert step_toward_farm("Town Square", ["n", "s", "e", "w"], 4) == "n"
    assert in_silvermere("Skali's Fine Armour, Front Room")
    assert not is_armour_shop("Skali's Fine Armour, Front Room")
    assert is_armour_shop("Newhaven, Armour Shop")
    assert step_toward_farm(
        "Skali's Fine Armour, Front Room", ["e", "s", "w"], 4
    ) == "s"
    assert step_toward_farm(
        "Skali's Fine Armour, Showroom", ["e", "n", "s"], 4
    ) == "s"
    assert step_toward_farm(
        "Skali's Fine Armour, Back Room", ["s"], 4
    ) == "s"
    skali = parse_line("Skali's Fine Armour, Front Room")
    assert skali and skali["kind"] == "room"
    assert step_toward_guild("Guild Street, Southern End", ["n", "s"]) is None
    gate = parse_line("You may not enter the arena.")
    assert gate and gate["kind"] == "cannot" and gate.get("arena")
    high = parse_line("You are too high a level to enter the arena.")
    assert high and high["kind"] == "cannot" and high.get("arena")
    s = WorldState()
    s.exits = ["n", "e", "w", "d"]
    s.apply(gate)
    assert s.arena_gated and "d" not in s.exits


def test_learn_scroll_lines() -> None:
    assert parse_line("You memorize the spell.")["kind"] == "learned"
    assert parse_line("You have learned a new spell!")["kind"] == "learned"
    already = parse_line("You already know that spell.")
    assert already["kind"] == "learned"
    assert already.get("already")
    assert parse_line("You are not high enough level to learn that spell.")[
        "kind"
    ] == "spell_skip"
    assert parse_line("You cannot learn that spell.")["kind"] == "spell_skip"
    low = parse_line("You are not high enough level to cast that spell.")
    assert low["kind"] == "cast_fail" and low.get("reason") == "level"
    yet = parse_line("You cannot cast that spell yet.")
    assert yet["kind"] == "cast_fail" and yet.get("reason") == "level"
    fail = parse_line("Your bless fails!")
    assert fail["kind"] == "cast_fail" and fail.get("reason") == "level"
    s = WorldState()
    s.apply({"kind": "learned"})
    assert s.learned
    s.apply({"kind": "spell_skip"})
    assert s.spell_skip


def test_spells_command_lists_known() -> None:
    from . import spells as S

    assert S.list_command("paladin") == "spells"
    assert S.list_command("mystic") == "powers"
    assert S.parse_book_row("1     1    harm harm") == "harm"
    assert S.parse_book_row("  1     2  mihe  Minor Healing") == "minor healing"
    assert S.parse_book_row("2  2  bles  Bless") == "bless"
    dump = (
        "You have the following spells:\n"
        "Level Mana Short Spell Name\n"
        "1     2    mihe minor healing\n"
        "1     1    harm harm\n"
        "[HP=22]: "
    )
    events = [ev for ev in harvest_screen(dump, set()) if ev.get("kind") == "spellbook"]
    assert events
    assert events[0].get("reset")
    assert events[0].get("names") == ["minor healing", "harm"]
    state = WorldState()
    state.apply(events[0])
    assert state.known_spells == ["minor healing", "harm"]
    empty = parse_line("You don't know any spells.")
    assert empty and empty["kind"] == "spellbook"
    assert empty.get("names") == []
    powers = (
        "You have the following powers:\n"
        "Level Mana Short Spell Name\n"
        "2     3    swan way of the swan\n"
        "3    10    owl  way of the owl\n"
    )
    pev = [ev for ev in harvest_screen(powers, set()) if ev.get("kind") == "spellbook"]
    assert pev and pev[0].get("names") == ["way of the swan", "way of the owl"]
    assert S.parse_book_row("3    10    owl  way of the owl") == "way of the owl"
    assert S.parse_book_row("1     1    vine vine strike") == "vine strike"
    assert S.parse_book_row("1     4    star starlight") == "starlight"


def test_outgoing_hits_feed_dps() -> None:
    now = 1_000.0
    s = WorldState()
    s.apply(parse_line("You whap nasty giant rat for 5 damage!"))
    s.apply(parse_line("You critically whap filthbug for 23 damage!"))
    assert s.dps() == 28
    assert s.dps_label() == "DPS 28"
    assert "DPS 28" in s.stats_label()
    idle = WorldState()
    assert idle.dps() is None
    assert idle.dps_label() == "DPS —"
    idle.note_dealt(10, now=now - SESSION_IDLE - 2)
    assert idle.dps(now) is None
    idle.note_dealt(12, now=now - 1)
    assert idle.dps(now) == 12
    assert idle.dps_label(now) == "DPS 12"


def _steady_loop(state: WorldState, first: float, last: float) -> None:
    t = first
    while t <= last:
        state.note_dealt(24, now=t)
        t += 16.0


def test_dps_long_loop_vs_short_trend() -> None:
    now = 5_000.0
    s = WorldState()
    # 24 dmg each 8s round + 8s walk, long enough for a stable loop norm.
    _steady_loop(s, now - 2400.0, now - 16.0)
    assert s.dps(now) == 12
    assert s.dps_long(now) == 12
    assert s.dps_trend(now) == "even"
    assert s.dps_label(now) == "DPS 12·12"

    s.note_dealt(48, now=now - 1.0)
    assert s.dps(now) == 20
    assert s.dps_long(now) == 12
    assert s.dps_trend(now) == "ahead"
    assert s.dps_label(now) == "DPS 12↑20"

    lag = WorldState()
    _steady_loop(lag, now - 2400.0, now - 64.0)
    lag.note_dealt(8, now=now - 1.0)
    assert lag.dps(now) == 8
    assert lag.dps_long(now) == 12
    assert lag.dps_trend(now) == "behind"
    assert lag.dps_label(now) == "DPS 12↓8"

    quiet = WorldState()
    _steady_loop(quiet, now - 2400.0, now - 64.0)
    assert quiet.dps(now) is None
    assert quiet.dps_long(now) == 12
    assert quiet.dps_label(now) == "DPS 12"

    stale = WorldState()
    stale.note_dealt(24, now=now - SESSION_IDLE - 1)
    assert stale.dps(now) is None
    assert stale.dps_long(now) is None
    assert stale.dps_label(now) == "DPS —"

    ding = WorldState()
    _steady_loop(ding, now - 80.0, now - 16.0)
    assert ding.dps_long(now) is not None
    ding.apply({"kind": "trained", "level": 2})
    assert ding.dps(now) is None
    assert ding.dps_long(now) is None


def test_stat_dump_parses_attack_and_ac() -> None:
    """`stat` lines feed Strength/Agility/Attack/AC. Health: 50 is not HP."""
    assert parse_line("Health:    17/28    [60%]")["kind"] == "hits"
    attr = parse_line("Health: 50")
    assert attr and attr["kind"] == "stats" and attr.get("health_stat") == 50
    pair = parse_line("Strength: 70         Intellect: 50")
    assert pair and pair["kind"] == "stats"
    assert pair.get("strength") == 70 and pair.get("intellect") == 50
    agi = parse_line("Willpower: 50        Agility: 80")
    assert agi and agi["kind"] == "stats" and agi.get("agility") == 80
    atk = parse_line("Attack: 32           AC: 4")
    assert atk and atk["kind"] == "stats"
    assert atk.get("attack") == 32 and atk.get("ac") == 4
    blob = (
        b"Strength: 70         Intellect: 50\r\n"
        b"Willpower: 50        Agility: 80\r\n"
        b"Charm: 50            Health: 50\r\n"
        b"Hits: 22/22                   Armour Class: 4\r\n"
        b"Attack: 32           AC: 14\r\n"
    )
    kinds = [e["kind"] for e in events_from_payload(blob)]
    assert kinds.count("stats") >= 4
    assert "hits" in kinds
    s = WorldState()
    s.apply({"kind": "prompt", "hp": 22, "max_hp": None})
    assert s.max_hp == 22
    assert not s.max_hp_known
    s.apply({"kind": "prompt", "hp": 30, "max_hp": None})
    assert s.max_hp == 30
    assert not s.max_hp_known
    assert s.hp_meter_ratio() == 1.0
    for ev in events_from_payload(blob):
        s.apply(ev)
    assert s.strength == 70 and s.agility == 80
    assert s.attack == 32 and s.ac == 14
    assert s.hp == 22
    assert s.max_hp == 22
    assert s.max_hp_known
    assert s.hp_ratio() == 1.0
    assert s.hp_meter_ratio() == 1.0
    assert s.stat_known
    assert not s.needs_stat()
    s.level = 1
    s.apply({"kind": "level", "level": 2})
    assert not s.stat_known
    assert s.needs_stat()


def test_stat_skill_lines_are_not_rooms() -> None:
    """STAT skill columns must not become room or hunt targets."""
    for line in (
        "Picklocks: 48",
        "Perception: 60        Stealth: 55",
        "Thievery: 70          Traps: 45",
        "Martial Arts: 60",
    ):
        assert parse_line(line) is None
    assert parse_line("Newhaven, Arena") == {"kind": "room", "title": "Newhaven, Arena"}
    assert parse_line("Adventurer's Guild, Main Room") == {
        "kind": "room",
        "title": "Adventurer's Guild, Main Room",
    }
    s = WorldState()
    s.room = "Adventurer's Guild, Main Room"
    s.apply({"kind": "room", "title": "Picklocks: 48"})
    assert s.room == "Adventurer's Guild, Main Room"


def test_party_cadence_lines() -> None:
    rest = parse_line("Matt sits down and begins to rest.")
    assert rest and rest["kind"] == "rest" and rest.get("actor") == "Matt"
    stand = parse_line("Matt stands up.")
    assert stand and stand["kind"] == "stand" and stand.get("actor") == "Matt"
    you_stand = parse_line("You stand up.")
    assert you_stand and you_stand["kind"] == "stand"
    fled = parse_line("Matt panics and flees west!")
    assert fled and fled["kind"] == "flee" and fled.get("name") == "Matt"
    assert fled.get("dir") == "w"
    east = parse_line("Klymacks flees to the east.")
    assert east and east["kind"] == "flee" and east.get("dir") == "e"
    you = parse_line("You flee to the west.")
    assert you and you["kind"] == "flee" and you.get("dir") == "w"
    hurt = parse_line("Klymacks gasps for breath, looking severely wounded.")
    assert hurt and hurt["kind"] == "wounded" and hurt.get("name") == "Klymacks"
    watching = WorldState()
    watching.self_names = {"klymacks"}
    watching.apply(rest)
    assert watching.ally_rest == "Matt"
    assert not watching.resting
    watching.apply(stand)
    assert watching.ally_stood
    assert watching.ally_rest == ""
    watching.apply(fled)
    assert watching.ally_fled == "w"
    leader = WorldState()
    leader.self_names = {"matt"}
    leader.apply(hurt)
    assert leader.ally_wounded == "Klymacks"


def test_party_invite_lines() -> None:
    join = parse_line("Ron has invited you to join him.")
    assert join and join["kind"] == "invited" and join["name"] == "Ron"
    been = parse_line("You have been invited to follow Ron.")
    assert been and been["kind"] == "invited" and been["name"] == "Ron"
    by = parse_line("You have been invited by Ron.")
    assert by and by["kind"] == "invited" and by["name"] == "Ron"
    invites = parse_line("Ron invites you to join him.")
    assert invites and invites["kind"] == "invited" and invites["name"] == "Ron"
    glued = parse_events(
        "You are carrying 9 silver nobles, padded vest (Torso), "
        "Ron has invited you to join him."
    )
    assert any(ev.get("kind") == "invited" and ev.get("name") == "Ron" for ev in glued)
    after_prompt = parse_events("[HP=21/MA=20]:Ron has invited you to join him.")
    kinds = [ev["kind"] for ev in after_prompt]
    assert "prompt" in kinds
    assert any(ev.get("kind") == "invited" and ev.get("name") == "Ron" for ev in after_prompt)
    cr = events_from_payload(b"Ron has invited you to follow him.\r[HP=21/MA=20]:")
    assert any(ev.get("kind") == "invited" and ev.get("name") == "Ron" for ev in cr)
    kept = keep_party_lf(b"Ron has invited you to follow him.\r[HP=21/MA=20]:")
    assert b"\r\n[HP=" in kept
    tr = Transcript()
    assert tr.feed(
        b"You are carrying 9 silver nobles, 27 copper farthings, padded vest (Torso),\n"
    ) == []
    mid = tr.feed(
        b"Ron has invited you to join him.\n"
        b"padded pants (Legs), club (Weapon Hand)\n"
        b"You have no keys.\n"
    )
    names = [
        ev.get("name")
        for line in mid
        for ev in parse_events(line)
        if ev.get("kind") == "invited"
    ]
    assert names == ["Ron"]
    colored = keep_party_lf(
        b"\x1b[1;36mSherry\x1b[0m has invited you to follow her.\r[HP=21/MA=20]:"
    )
    assert b"\r\n[HP=" in colored
    assert b"has invited you to follow her." in colored
    lower = parse_line("sherry has invited you to follow her.")
    assert lower and lower["kind"] == "invited" and lower["name"].lower() == "sherry"
    wrapped = events_from_payload(
        b"Also here: Rhiannon.\x1b[0mSherry has invited you to follow her.[HP=21]:"
    )
    assert any(
        ev.get("kind") == "invited" and str(ev.get("name", "")).lower() == "sherry"
        for ev in wrapped
    )


def test_party_leave_disband_keep_lf() -> None:
    """Disband / unfollow lines use bare CR before [HP=]; keep them on screen."""
    leave = keep_party_lf(b"You are no longer following Matt.\r[HP=45/MA=22]:")
    assert b"\r\n[HP=" in leave
    assert b"You are no longer following Matt." in leave
    removed = keep_party_lf(
        b"klymacks has been removed from your followers.\r[HP=45]:"
    )
    assert b"\r\n[HP=" in removed
    assert b"removed from your followers." in removed
    solo = keep_party_lf(
        b"You are not in a party at the present time.\r[HP=40]:"
    )
    assert b"\r\n[HP=" in solo
    group = keep_party_lf(b"Ryan just left your group (2).\r[HP=40]:")
    assert b"\r\n[HP=" in group
    roster = keep_party_lf(
        b"The following people are in your travel party:\r[HP=40]:"
    )
    assert b"\r\n[HP=" in roster
    # Confirm prompt has no bare CR — leave untouched.
    sure = b"Are you sure you want to disband party? "
    assert keep_party_lf(sure) == sure


def test_already_in_party_invite_lines() -> None:
    mine = parse_line("Klymacks is already in your party.")
    assert mine and mine["kind"] == "followed" and mine.get("name") == "Klymacks"
    follow = parse_line("Ryan is already following you.")
    assert follow and follow["kind"] == "followed" and follow.get("name") == "Ryan"
    grouped = WorldState()
    grouped.apply(mine)
    assert "Klymacks" in grouped.followers
    busy = parse_line("Kevin is already in a party.")
    assert busy and busy["kind"] == "party_fail" and busy.get("reason") == "busy"
    other = parse_line("That person is already following someone.")
    assert other and other["kind"] == "party_fail"


def test_look_reprint_keeps_pcs() -> None:
    """Same-room look without Also here must not wipe standing players."""
    here = WorldState()
    here.mobs = ["klymacks", "Ryan", "giant rat"]
    here.apply({"kind": "room", "title": "Guild Street"})
    here.apply({"kind": "exits", "exits": ["n", "s"]})
    assert "klymacks" in here.mobs
    assert "Ryan" in here.mobs
    assert "giant rat" not in here.mobs
    missed = WorldState()
    missed.mobs = ["klymacks", "acid slime"]
    missed.empty_if_look_missed({"exits"})
    assert missed.mobs == ["klymacks"]


def test_new_room_reparses_same_also_here() -> None:
    """Party walking together reprints 'Also here: klymacks.' — must not stay skipped."""
    seen: set[str] = set()
    first = harvest_screen("Guild Street\nAlso here: klymacks.\n", seen)
    assert any(e.get("kind") == "also_here" for e in first)
    second = harvest_screen(
        "Intersection of Guild St. & River St.\nAlso here: klymacks.\n", seen
    )
    assert any(e.get("kind") == "also_here" for e in second)


def test_guild_street_look_is_not_this_is_a_room() -> None:
    """Wrapped 'This is a cobblestoned street...' must not overwrite Guild Street."""
    assert parse_line("Guild Street") == {"kind": "room", "title": "Guild Street"}
    assert parse_line("This is a cobblestoned street") is None
    assert parse_line("This is a") is None
    assert parse_line("Thi") is None
    blob = (
        "Guild Street\n"
        "    This is a cobblestoned street. It is dimly lit by guttering lanterns hung\n"
        "from tall posts, and continues to the north and south. River street follows\n"
        "the city wall off to the north.\n"
        "Also here: small guardsman.\n"
        "Obvious exits: north, south\n"
        "[HP=51]:\n"
    )
    state = WorldState()
    for ev in parse_events(blob):
        state.apply(ev)
    assert state.room == "Guild Street"
    assert state.exits == ["n", "s"]
    assert "small guardsman" in state.mobs


if __name__ == "__main__":
    test_prompt_and_room()
    test_kill()
    test_falls_dead_and_combat_off()
    test_combat_and_shop()
    test_join_call_say()
    test_party_rest_heal_tags()
    test_backspace_exits()
    test_mana_pool_vs_prompt()
    test_mortal_aid_drag()
    test_train_and_level_forget_maxes()
    test_bless_lucky_and_wear_off()
    test_owl_strong_willed_and_wear_off()
    test_starlight_shimmer_and_fade()
    test_inventory_geared()
    test_inventory_wrapped_padded_set()
    test_inventory_stacked_extras()
    test_already_worn_gloves()
    test_night_vision_kit_rules()
    test_wealth_and_deposit_lines()
    test_nathaniel_steps_south()
    test_general_store_is_torch_shop()
    test_spell_shop_steps()
    test_silvermere_titles_and_skiff()
    test_learn_scroll_lines()
    test_spells_command_lists_known()
    test_outgoing_hits_feed_dps()
    test_dps_long_loop_vs_short_trend()
    test_stat_dump_parses_attack_and_ac()
    test_stat_skill_lines_are_not_rooms()
    test_party_cadence_lines()
    test_party_invite_lines()
    test_party_leave_disband_keep_lf()
    test_already_in_party_invite_lines()
    test_look_reprint_keeps_pcs()
    test_new_room_reparses_same_also_here()
    test_guild_street_look_is_not_this_is_a_room()
    print("ok")
