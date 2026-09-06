from __future__ import annotations

from client.map import Map, is_corridor, is_unique
from client.realm_map import Atlas


def test_unique_pins() -> None:
    world = Map()
    assert is_unique("Helfgrim's Blades")
    assert is_unique("Guild Street, Southern End")
    assert is_unique("Town Square")
    assert is_unique("Intersection of Temple St. & Stone St.")
    assert not is_unique("Guild Street")
    assert is_corridor("Guild Street")
    assert world.arrived("Helfgrim's Blades")
    assert not is_unique("Secret Passage")
    assert is_corridor("Secret Passage")


def test_helfgrim_leaves_east_unless_buying_blades() -> None:
    world = Map()
    exits = ["e"]
    assert world.step("farm", "Helfgrim's Blades", exits) == "e"
    assert world.step("store", "Helfgrim's Blades", exits) == "e"
    assert world.step("square", "Helfgrim's Blades", exits) == "e"
    assert world.step("weapons", "Helfgrim's Blades", exits) is None


def test_secret_passage_walks_live_exits() -> None:
    world = Map()
    assert world.step("farm", "Secret Passage", ["n"]) == "n"
    assert world.step("farm", "Secret Passage", ["w", "se"], last_step="e") == "se"
    assert world.step("farm", "Secret Passage", ["w", "se"], last_step="se") == "w"


def test_river_eastern_end_walks_west() -> None:
    """Locked tower is east. Graveyard is back west, not another e."""
    world = Map()
    assert is_unique("River Street, Eastern End")
    assert world.step("farm", "River Street, Eastern End", ["s", "w"]) == "w"
    assert world.step("farm", "River Street, Eastern End", ["e", "w"]) == "w"
    assert world.step("square", "River Street, Eastern End", ["s", "w"]) == "w"
    assert world.step("store", "River Street, Eastern End", ["s", "w"]) == "w"
    river = ["e", "n", "s", "w"]
    assert world.step("store", "River Street", river) == "w"
    assert world.step("store", "River Street", river, last_step="w") == "w"
    # GY is east to the River/Bridge gates. Never n into shops.
    assert world.step("farm", "River Street", river, last_step="e") == "e"
    assert world.step("graveyard", "River Street", river, last_step="e") == "e"
    assert world.step("farm", "River Street", river, last_step="w") == "w"
    ew = ["e", "w"]
    assert world.step("farm", "River Street", ew, last_step="w") == "w"
    assert world.step("farm", "River Street", ew, last_step="e") == "e"
    assert is_unique("Intersection of River St. & Bridge St.")
    assert world.step(
        "farm", "Intersection of River St. & Bridge St.", ["e", "s", "w"]
    ) == "bash north"
    assert world.step(
        "farm",
        "Intersection of River St. & Bridge St.",
        ["e", "s", "w"],
        closed=["n"],
        klass="paladin",
    ) == "bash north"
    assert (
        world.step(
            "farm",
            "Intersection of River St. & Bridge St.",
            ["e", "s", "w"],
            closed=["n"],
            klass="ninja",
        )
        == "picklock north"
    )
    assert (
        world.step(
            "farm", "Intersection of River St. & Bridge St.", ["n", "e", "s", "w"]
        )
        == "n"
    )
    assert is_unique("Bridge")
    assert world.step("farm", "Bridge", ["ne", "s", "sw"]) == "ne"
    assert world.step("farm", "Bridge", ["n", "ne", "s"]) == "ne"
    assert world.step("farm", "Graveyard Bridge", ["ne", "s", "sw"]) == "ne"
    assert world.step("farm", "Bridge Street", ["e", "n", "s"]) == "n"
    # Open north on the n/s cobbles: walk in. Do not bash an open gate.
    assert world.step("graveyard", "Bridge Street", ["n", "s"], klass="paladin") == "n"
    assert world.step("farm", "Bridge Street", ["n", "s"]) == "n"
    assert (
        world.step(
            "graveyard",
            "Bridge Street",
            ["s"],
            closed=["n"],
            klass="ninja",
        )
        == "picklock north"
    )
    poisoned = Map()
    poisoned.atlas.edges[
        ("intersection of river st. & bridge st.", "s")
    ] = "graveyard entrance"
    assert (
        poisoned.step(
            "farm", "Intersection of River St. & Bridge St.", ["e", "s", "w"]
        )
        == "bash north"
    )
    assert world.step(
        "graveyard", "Intersection of River St. & Bridge St.", ["e", "s", "w"]
    ) == "bash north"
    assert world.step(
        "graveyard", "Bridge Street", ["e", "s", "w"], klass="paladin"
    ) == "bash north"
    assert world.step("graveyard", "Bridge Street", ["e", "n", "s"]) == "n"
    town = Map()
    town.atlas.edges[("bridge street", "s")] = "town square"
    town.atlas.edges[("intersection of river st. & bridge st.", "s")] = "town square"
    assert (
        town.step(
            "graveyard", "Intersection of River St. & Bridge St.", ["e", "s", "w"]
        )
        == "bash north"
    )
    assert town.step("graveyard", "Bridge Street", ["e", "s"]) == "bash north"


def test_silver_eastern_end_walks_west() -> None:
    """Town Square is west. A poisoned atlas e→TS must not win."""
    world = Map()
    world.atlas.edges[("silver street eastern end", "e")] = "town square"
    assert is_unique("Silver Street, Eastern End")
    east = ["e", "n", "s", "w"]
    assert world.step("farm", "Silver Street, Eastern End", east) == "w"
    assert world.step("farm", "Silver Street, Eastern End", east, last_step="e") == "w"
    assert world.step("graveyard", "Silver Street, Eastern End", east) == "w"
    assert world.step("square", "Silver Street, Eastern End", east) == "w"
    assert world.step("store", "Silver Street, Eastern End", east) == "w"
    assert world.step("farm", "Silver Street", east) == "w"
    assert world.step("farm", "Intersection of Silver St. & Brass St.", east) == "w"


def test_unknown_one_door_leaves() -> None:
    world = Map()
    assert world.step("farm", "Dusty Alcove", ["w"]) == "w"


def test_unknown_hall_walks_unmapped_door() -> None:
    world = Map()
    assert world.step("farm", "Hidden Gallery", ["e", "w"]) in {"e", "w"}
    world.observe("Dusty Alcove", ["e"], via="e", prev="Hidden Gallery")
    assert world.step("farm", "Hidden Gallery", ["e", "w"], last_step="w") == "w"


def test_guild_southern_end_never_takes_shop_doors_on_farm() -> None:
    world = Map()
    exits = ["n", "s", "e", "w"]
    assert world.step("farm", "Guild Street, Southern End", exits) == "n"
    assert world.step("graveyard", "Guild Street, Southern End", exits) == "n"
    assert world.step("store", "Guild Street, Southern End", exits) == "s"
    assert world.step("weapons", "Guild Street, Southern End", exits) == "w"


def test_mid_guild_street_walks_north_not_into_shops() -> None:
    world = Map()
    exits = ["n", "s", "e", "w"]
    assert world.step("farm", "Guild Street", exits) == "n"
    assert world.step("store", "Guild Street", exits) == "s"
    assert world.step("square", "Guild Street", exits) == "s"
    assert world.step("guild", "Guild Street", exits, klass="paladin") == "n"
    assert world.step("guild", "Guild Street", ["n", "s"], klass="paladin") == "n"
    assert world.step(
        "guild", "Town Square", ["n", "s", "e", "w"], klass="paladin"
    ) == "n"
    assert world.step(
        "guild",
        "Guild Street, Northern End",
        ["e", "n", "s"],
        klass="paladin",
    ) == "e"
    assert world.step(
        "guild", "Town Square", ["n", "s", "e", "w"], klass="cleric"
    ) == "w"
    assert world.step(
        "guild",
        "Adventurer's Guild, Foyer",
        ["e", "w"],
        klass="paladin",
    ) == "e"
    assert (
        world.step(
            "guild",
            "Adventurer's Guild, Universal Trainer",
            ["w"],
            klass="paladin",
        )
        is None
    )
    assert (
        world.step(
            "guild",
            "Adventurer's Guild, Main Room",
            ["e", "n", "s", "w", "d"],
            klass="paladin",
        )
        == "e"
    )
    assert (
        world.step(
            "guild",
            "Adventurer's Guild, Main Room",
            ["e", "n", "s", "w", "push button"],
            klass="ninja",
        )
        == "push button"
    )


def test_narrow_road_farm_is_down_not_healer() -> None:
    world = Map()
    exits = ["n", "e", "w", "d"]
    assert world.step("farm", "Newhaven, Narrow Road", exits, level=1) == "d"
    assert world.step("farm", "Newhaven, Narrow Road", exits, level=4) == "e"
    assert world.step("healer", "Newhaven, Narrow Road", exits) == "w"
    assert world.step("farm", "Newhaven, Arena", ["u"], level=1) is None


def test_square_to_store_and_farm() -> None:
    world = Map()
    exits = ["n", "s", "e", "w"]
    assert world.step("store", "Town Square", exits) == "e"
    assert world.step("farm", "Town Square", exits) == "n"
    assert world.step("square", "Intersection of Temple St. & Stone St.", ["n", "s", "e", "w"]) == "e"
    assert world.step("spells", "Intersection of Temple St. & Stone St.", ["n", "s", "e", "w"]) == "w"
    assert world.step("graveyard", "Town Square", exits) == "n"
    assert world.step("sewer", "Town Square", exits) == "go manhole"
    assert world.step("sewer", "Graveyard Entrance", ["e", "w"]) == "w"
    assert world.step("sewer", "Sewer Tunnel, Junction", ["u", "n"]) is None


def test_goto_graveyard_leaves_sewers() -> None:
    """GY as a landmark is the grass, not 'already farming' in the pipes."""
    world = Map()
    assert world.step("graveyard", "Sewer Tunnel, Junction", ["u", "n"]) == "u"
    assert world.step("graveyard", "Town Square", ["n", "s", "e", "w"]) == "n"
    assert world.step("graveyard", "Graveyard", ["e", "w", "n"]) is None
    assert world.step("farm", "Sewer Tunnel, Junction", ["u", "n"]) is None


def test_observe_does_not_steal_unique_north() -> None:
    atlas = Atlas()
    world = Map(atlas)
    south = "Guild Street, Southern End"
    assert atlas.path(south, "Guild Street, Northern End") == ["n"]
    world.observe("Guild Street", ["n", "s", "e", "w"], via="n", prev=south)
    assert atlas.path(south, "Guild Street, Northern End") == ["n"]


if __name__ == "__main__":
    test_unique_pins()
    test_helfgrim_leaves_east_unless_buying_blades()
    test_guild_southern_end_never_takes_shop_doors_on_farm()
    test_mid_guild_street_walks_north_not_into_shops()
    test_narrow_road_farm_is_down_not_healer()
    test_square_to_store_and_farm()
    test_goto_graveyard_leaves_sewers()
    test_secret_passage_walks_live_exits()
    test_river_eastern_end_walks_west()
    test_silver_eastern_end_walks_west()
    test_unknown_one_door_leaves()
    test_unknown_hall_walks_unmapped_door()
    test_observe_does_not_steal_unique_north()
    print("ok")
