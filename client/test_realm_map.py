from __future__ import annotations

import tempfile
from pathlib import Path

from .realm_map import SILVERMERE, Atlas, NEWHAVEN, _mappable, room_key


def test_seed_has_newhaven() -> None:
    atlas = Atlas()
    assert atlas.known("Newhaven, Arena")
    assert atlas.known("Newhaven Arena")
    assert atlas.known("Newhaven, Village Entrance")
    assert atlas.room_count() >= len(NEWHAVEN)
    assert atlas.path("Newhaven, Village Entrance", "Newhaven, Arena") == [
        "w",
        "w",
        "d",
    ]
    assert atlas.path("Newhaven, Arena", "Newhaven, Guild") == ["u", "n"]
    assert atlas.path("Newhaven, Narrow Road", "Newhaven, Guild") == ["n"]
    assert atlas.path("Newhaven, Village Entrance", "Newhaven, Docks") == ["se", "s"]
    assert atlas.path("Newhaven, Docks", "Pier") == ["borrow skiff"]
    assert atlas.path("Pier", "Newhaven, Docks") == ["borrow skiff"]
    assert atlas.path("Newhaven, Guild", "Newhaven, Docks")[0] == "s"
    assert "borrow skiff" in atlas.path("Newhaven, Guild", "Pier")
    # After the skiff, TS is a counted 3s 6e 10s walk — not a Docks shortcut.
    assert atlas.path("Pier", "Town Square") == []
    assert atlas.known("Town Square")
    assert atlas.room_count() >= len(NEWHAVEN) + len(SILVERMERE)
    gy = atlas.path(
        "Intersection of Guild St. & River St.", "Graveyard Entrance"
    )
    assert gy[0] == "e"
    assert gy[-1] == "ne"
    assert "n" in gy
    assert atlas.path("Bridge Street", "Graveyard Entrance") == ["n", "n", "ne"]
    assert atlas.path("Graveyard Entrance", "Bridge") == ["sw"]
    assert atlas.path("Shack", "Bridge") == ["sw"]
    assert atlas.path("Guild Street, Northern End", "Adventurer's Guild, Foyer") == [
        "e"
    ]
    assert atlas.path(
        "Adventurer's Guild, Foyer", "Adventurer's Guild, Universal Trainer"
    ) == ["e", "e"]


def test_record_edge_bfs() -> None:
    atlas = Atlas()
    atlas.observe("Room A", ["d"])
    atlas.observe("Newhaven, Arena", ["u"], via="d", prev="Room A")
    assert atlas.path("Room A", "Newhaven, Arena") == ["d"]
    assert atlas.way_home("Room A") == ["d"]


def test_unknown_room_no_crash() -> None:
    atlas = Atlas()
    assert atlas.path("???", "Newhaven, Arena") == []
    assert atlas.way_home("") == []
    assert atlas.way_home("no such hall") == []
    key = atlas.observe("", ["n"], via="s", prev="Somewhere Dark")
    assert key.startswith("?")
    hint = atlas.suggest("Mystery Cave", ["n", "s"], last_step="n", scanned=True)
    assert hint.action in {"guess", "look"}
    assert hint.step in {"", "n", "s", "u"}


def test_maps_title_case_a_hall() -> None:
    assert _mappable("A Dark Hall")
    assert _mappable("Secret Passage")
    assert _mappable("Guild Street")
    assert _mappable("Pier")
    assert not _mappable("A large rat")
    assert not _mappable("You swing at the rat")
    assert not _mappable("Thi")
    assert not _mappable("This is a")
    assert not _mappable("This is a cobblestoned street")


def test_unmapped_doors_are_explored() -> None:
    atlas = Atlas()
    atlas.observe("Hidden Gallery", ["e", "w"])
    assert atlas.known("Hidden Gallery")
    assert atlas.unmapped("Hidden Gallery", ["e", "w"]) == ["e", "w"]
    hint = atlas.suggest("Hidden Gallery", ["e", "w"], scanned=True)
    assert hint.action == "guess"
    assert hint.step in {"e", "w"}
    atlas.observe("Dusty Alcove", ["e"], via="e", prev="Hidden Gallery")
    assert atlas.path("Hidden Gallery", "Dusty Alcove") == ["e"]
    assert "e" not in atlas.unmapped("Hidden Gallery", ["e", "w"])
    assert atlas.unmapped("Hidden Gallery", ["e", "w"]) == ["w"]


def test_suggest_one_exit_walks_not_look() -> None:
    atlas = Atlas()
    hint = atlas.suggest("Secret Passage", ["n"], last_step="", scanned=True)
    assert hint.action == "guess"
    assert hint.step == "n"


def test_suggest_look_when_unscanned() -> None:
    atlas = Atlas()
    hint = atlas.suggest("Room A", ["d"], scanned=False)
    assert hint.action == "look"
    assert hint.chrome.startswith("map:")


def test_suggest_path_chrome() -> None:
    atlas = Atlas()
    atlas.observe("Room A", ["d"])
    atlas.observe("Newhaven, Arena", ["u"], via="d", prev="Room A")
    hint = atlas.suggest("Room A", ["d"], scanned=True)
    assert hint.action == "path"
    assert hint.route == ["d"]
    assert hint.chrome == "path: d"


def test_normalize_title() -> None:
    assert room_key("Newhaven, Arena") == room_key("Newhaven Arena")
    assert room_key("  Newhaven,  Arena ") == "newhaven arena"


def test_persist_roundtrip() -> None:
    with tempfile.TemporaryDirectory() as tmp:
        path = Path(tmp) / "realm-map.json"
        atlas = Atlas(path)
        atlas.observe("Room A", ["d"])
        atlas.observe("Newhaven, Arena", ["u"], via="d", prev="Room A")
        again = Atlas(path)
        assert again.path("Room A", "Newhaven, Arena") == ["d"]


def test_silver_eastern_end_path_is_west() -> None:
    atlas = Atlas()
    atlas.edges[("silver street eastern end", "e")] = "town square"
    atlas._drop_impossible_edges()
    assert atlas.path("Silver Street, Eastern End", "Town Square")[0] == "w"
    atlas.observe(
        "Town Square",
        ["n", "s", "e", "w"],
        via="e",
        prev="Silver Street, Eastern End",
    )
    assert atlas.edges.get(("silver street eastern end", "e")) != "town square"


def test_bridge_street_south_is_not_the_gates() -> None:
    atlas = Atlas()
    atlas.edges[("bridge street", "s")] = "intersection of river st. & bridge st."
    atlas._drop_impossible_edges()
    assert atlas.edges.get(("bridge street", "n")) == "intersection of river st. & bridge st."
    assert atlas.edges.get(("bridge street", "s")) != "intersection of river st. & bridge st."


def test_docks_are_not_a_guild_river_shortcut() -> None:
    atlas = Atlas()
    assert atlas.edges.get(("docks", "e")) != "intersection of guild st. & river st."
    assert atlas.edges.get(("docks", "s")) != "intersection of guild st. & river st."
    atlas.observe(
        "Intersection of Guild St. & River St.",
        ["e", "s", "w"],
        via="e",
        prev="Docks",
    )
    assert atlas.edges.get(("docks", "e")) != "intersection of guild st. & river st."


def test_look_scraps_do_not_poison_guild_street() -> None:
    atlas = Atlas()
    atlas.observe(
        "This is a cobblestoned street",
        ["n", "s"],
        via="n",
        prev="Guild Street",
    )
    assert atlas.edges.get(("guild street", "n")) != "thi"
    assert atlas.edges.get(("guild street", "n")) != "this is a cobblestoned street"
    live = Path("data/realm-map.json")
    if live.is_file():
        loaded = Atlas(live)
        assert loaded.edges.get(("guild street", "n")) != "thi"


if __name__ == "__main__":
    test_seed_has_newhaven()
    test_record_edge_bfs()
    test_unknown_room_no_crash()
    test_suggest_look_when_unscanned()
    test_suggest_path_chrome()
    test_normalize_title()
    test_persist_roundtrip()
    test_silver_eastern_end_path_is_west()
    test_suggest_one_exit_walks_not_look()
    test_maps_title_case_a_hall()
    test_unmapped_doors_are_explored()
    test_bridge_street_south_is_not_the_gates()
    test_docks_are_not_a_guild_river_shortcut()
    test_look_scraps_do_not_poison_guild_street()
    print("ok")
