"""MegaMud / klymacks map path loader."""

from __future__ import annotations

from pathlib import Path

from .megapath import (
    ALLPATHS,
    DEFAULT_SEWER,
    KLYMACKS_MAPS,
    VENDOR_PATHS,
    catalog,
    goto_step,
    load_mp,
    load_sewer_path,
    seed_klymacks_maps,
)


def test_load_winterhawk_east_half() -> None:
    path = load_mp(VENDOR_PATHS / "GAV1LOO2.mp")
    assert "Silvermere Sewers" in path.title
    assert "Winterhawk" in path.author
    assert "Junction" in path.start
    assert path.steps
    assert path.steps[0] == "n"
    assert "u" not in path.steps[:20]


def test_seed_klymacks_maps_retitles() -> None:
    dest = seed_klymacks_maps()
    assert dest.is_file()
    assert dest.name == DEFAULT_SEWER
    path = load_sewer_path()
    assert "klymacks" in path.author.lower()
    assert "Finn" in path.title or "finn" in path.title.lower()
    assert path.steps[0] == "n"
    assert path.resolve(0) == "n"
    assert (KLYMACKS_MAPS / "README.md").is_file()


def test_resolve_st_uses_last_compass() -> None:
    path = load_mp(VENDOR_PATHS / "GAV1LOOP.mp")
    # Find an st beat if present; otherwise skip.
    if "st" not in path.steps:
        return
    i = path.steps.index("st")
    assert path.resolve(i, "w") == "w"


def test_allpaths_pack_is_present() -> None:
    mps = list(ALLPATHS.glob("*.MP")) + list(ALLPATHS.glob("*.mp"))
    assert len(mps) >= 400
    assert (ALLPATHS / "JAGGED.TXT").is_file()
    assert len(catalog().paths) >= 400


def test_junction_to_town_square_goes_up() -> None:
    assert (
        goto_step(
            "Sewer Tunnel, Junction",
            ["Town Square"],
            ["u", "n"],
        )
        == "u"
    )


def test_graveyard_bridge_loop_first_step_is_ne() -> None:
    assert (
        goto_step(
            "Graveyard Bridge",
            ["Graveyard"],
            ["n", "ne", "sw"],
        )
        == "ne"
    )
    assert (
        goto_step(
            "Bridge",
            ["Graveyard"],
            ["n", "ne", "sw"],
        )
        == "ne"
    )


def test_town_square_farm_does_not_dive_manhole() -> None:
    assert (
        goto_step(
            "Town Square",
            [
                "Graveyard Entrance",
                "Graveyard",
                "Graveyard Bridge",
                "Bridge",
                "Town Square",
            ],
            ["n", "s", "e", "w"],
        )
        is None
    )


if __name__ == "__main__":
    test_load_winterhawk_east_half()
    test_seed_klymacks_maps_retitles()
    test_resolve_st_uses_last_compass()
    test_allpaths_pack_is_present()
    test_junction_to_town_square_goes_up()
    test_graveyard_bridge_loop_first_step_is_ne()
    test_town_square_farm_does_not_dive_manhole()
    print("ok")
