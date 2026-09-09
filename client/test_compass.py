from __future__ import annotations

from client.compass import CONFIRM_WAIT, Compass, is_move


def test_is_move() -> None:
    assert is_move("n")
    assert is_move("go manhole")
    assert is_move("borrow skiff")
    assert is_move("open north")
    assert is_move("bash north")
    assert is_move("picklock north")
    assert not is_move("bash kobold thief")
    assert is_move("drag klymacks n")
    assert not is_move("look")
    assert not is_move("buy torch")


def test_step_confirms_on_new_room() -> None:
    """Dir, then a new title / exits — next dir, no look."""
    c = Compass()
    c.note("n", "Town Square", 10, now=100.0, travel_seq=1, exits=["n", "s", "e", "w"])
    assert c.see("Town Square", 10, now=100.5, travel_seq=1) == "wait"
    assert (
        c.see(
            "Guild Street, Southern End",
            11,
            now=100.6,
            travel_seq=2,
            exits=["n", "s"],
        )
        == "ok"
    )
    assert not c.pending
    assert c.phase == ""


def test_same_title_confirms_when_exits_reprint() -> None:
    """Same-title halls land on the next prompt even if the exits line is a dup."""
    c = Compass()
    c.note("e", "Secret Passage", 10, now=100.0, travel_seq=4, exits=["n"])
    assert c.see("Secret Passage", 11, now=100.4, travel_seq=4, exits=["n"]) == "ok"
    assert not c.pending


def test_guild_street_same_title_confirms_without_new_exits_line() -> None:
    """Two Guild Street tiles share title + n/s. Do not look-fail into south."""
    c = Compass()
    c.note("n", "Guild Street", 10, now=100.0, travel_seq=4, exits=["n", "s"])
    assert (
        c.see("Guild Street", 11, now=100.4, travel_seq=4, exits=["n", "s"]) == "ok"
    )
    assert not c.pending


def test_move_exits_count_as_landing() -> None:
    c = Compass()
    c.note("e", "Secret Passage", 10, now=100.0, travel_seq=4, exits=["n"])
    assert (
        c.see("Secret Passage", 11, now=100.4, travel_seq=5, exits=["w", "se"])
        == "ok"
    )
    assert not c.pending


def test_unique_title_change_is_enough() -> None:
    c = Compass()
    c.note("n", "Town Square", 10, now=100.0)
    assert c.see("Guild Street, Southern End", 11, now=100.6) == "ok"


def test_timeout_asks_for_look() -> None:
    c = Compass()
    c.note("w", "Guild Street, Southern End", 10, now=100.0)
    assert c.see("Guild Street, Southern End", 10, now=100.0 + CONFIRM_WAIT - 0.1) == "wait"
    # Still no new prompt — stay gated. Peek timeout is after we asked for look.
    assert c.see("Guild Street, Southern End", 10, now=100.0 + CONFIRM_WAIT) == "wait"
    assert c.see("Guild Street, Southern End", 11, now=100.0 + CONFIRM_WAIT) == "look"
    assert c.see("Guild Street, Southern End", 11, now=100.0 + CONFIRM_WAIT * 2, travel_seq=0) == "look"
    assert not c.pending
    assert c.failed


def test_unlatch_asks_look_until_dir_lists() -> None:
    """Bash stays in the room. Land only when a look lists the latch dir."""
    c = Compass()
    c.note(
        "bash north",
        "Intersection of River St. & Bridge St.",
        10,
        now=100.0,
        travel_seq=3,
        exits=["e", "s", "w"],
    )
    assert c.see(
        "Intersection of River St. & Bridge St.",
        10,
        now=100.2,
        travel_seq=3,
        exits=["e", "s", "w"],
    ) == "wait"
    assert (
        c.see(
            "Intersection of River St. & Bridge St.",
            11,
            now=100.4,
            travel_seq=3,
            exits=["e", "s", "w"],
        )
        == "look"
    )
    assert c.pending == "bash north"
    assert (
        c.see(
            "Intersection of River St. & Bridge St.",
            12,
            now=100.6,
            travel_seq=4,
            exits=["n", "e", "s", "w"],
        )
        == "ok"
    )
    assert not c.pending
    assert not c.failed


def test_unlatch_still_closed_after_look_allows_rebash() -> None:
    """Look reprinted the same exits. Clear pending so the next tick can bash."""
    c = Compass()
    c.note(
        "bash north",
        "Bridge Street",
        10,
        now=100.0,
        travel_seq=3,
        exits=["e", "s", "w"],
    )
    assert (
        c.see(
            "Bridge Street",
            11,
            now=100.4,
            travel_seq=3,
            exits=["e", "s", "w"],
        )
        == "look"
    )
    assert (
        c.see(
            "Bridge Street",
            12,
            now=100.8,
            travel_seq=4,
            exits=["e", "s", "w"],
        )
        == "ok"
    )
    assert not c.pending
    assert not c.failed


def test_unlatch_walks_when_tick_already_lists_dir() -> None:
    """A room tick that lists north is enough — no extra look."""
    c = Compass()
    c.note(
        "bash north",
        "Bridge Street",
        10,
        now=100.0,
        travel_seq=3,
        exits=["e", "s", "w"],
    )
    assert (
        c.see(
            "Bridge Street",
            11,
            now=100.4,
            travel_seq=3,
            exits=["n", "e", "s", "w"],
        )
        == "ok"
    )
    assert not c.pending


def test_blocked_clears_pending() -> None:
    c = Compass()
    c.note("w", "Guild Street", 10, now=100.0)
    assert c.see("Guild Street", 11, now=100.2, blocked=True) == "look"
    assert not c.pending
    assert c.failed


def test_blocked_looks_even_without_pending() -> None:
    c = Compass()
    assert c.see("River Street, Eastern End", 12, now=100.2, blocked=True) == "look"
    assert not c.pending
    assert c.failed


def test_sneak_empty_room() -> None:
    c = Compass()
    assert c.can_sneak([], {"klymacks", "matt"})


def test_sneak_with_party_only() -> None:
    c = Compass()
    assert c.can_sneak(["Matt"], {"klymacks", "matt"})
    assert c.strangers(["Matt"], {"klymacks", "matt"}) == []
    assert c.can_sneak(["Matt."], {"matt"})
    assert c.can_sneak(["Matt the Paladin"], {"matt"})


def test_sneak_blocked_by_stranger() -> None:
    c = Compass()
    assert not c.can_sneak(["Coorwyn"], {"klymacks", "matt"})
    assert c.strangers(["Coorwyn", "Matt"], {"klymacks", "matt"}) == ["Coorwyn"]


def test_sneak_blocked_by_named_npc() -> None:
    c = Compass()
    from client.paths import occupants_in

    here = occupants_in(["Corwyn", "Matt"])
    assert "Corwyn" in here
    assert not c.can_sneak(here, {"klymacks", "matt"})


def test_sneak_blocked_in_combat_or_lops() -> None:
    c = Compass()
    assert not c.can_sneak([], {"klymacks"}, combat=True)
    assert not c.can_sneak([], {"klymacks"}, has_lop=True)


if __name__ == "__main__":
    test_is_move()
    test_step_confirms_on_new_room()
    test_same_title_confirms_when_exits_reprint()
    test_guild_street_same_title_confirms_without_new_exits_line()
    test_move_exits_count_as_landing()
    test_unique_title_change_is_enough()
    test_timeout_asks_for_look()
    test_unlatch_asks_look_until_dir_lists()
    test_unlatch_still_closed_after_look_allows_rebash()
    test_unlatch_walks_when_tick_already_lists_dir()
    test_blocked_clears_pending()
    test_blocked_looks_even_without_pending()
    test_sneak_empty_room()
    test_sneak_with_party_only()
    test_sneak_blocked_by_stranger()
    test_sneak_blocked_by_named_npc()
    test_sneak_blocked_in_combat_or_lops()
    print("ok")
