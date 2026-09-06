"""Hunt run catalog."""

from __future__ import annotations

from .runs import DEFAULT, list_runs, parse


def test_parse_aliases() -> None:
    assert parse("").id == "gy"
    assert parse("gy").id == "gy"
    assert parse("graveyard").approach == "farm"
    sewer = parse("sewer")
    assert sewer is not None
    assert sewer.id == "sewer-east"
    assert sewer.approach == "sewer"
    assert sewer.loop == "sewer"
    assert sewer.torch
    assert sewer.tape
    assert parse("pipes") is sewer
    arena = parse("pit")
    assert arena is not None
    assert arena.id == "arena"
    assert arena.loop == "pit"
    assert not arena.torch
    assert parse("nope") is None
    assert DEFAULT.id == "gy"


def test_list_runs_names_the_catalog() -> None:
    text = list_runs()
    assert text.startswith("runs:")
    assert "gy" in text
    assert "sewer-east" in text
    assert "arena" in text


if __name__ == "__main__":
    test_parse_aliases()
    test_list_runs_names_the_catalog()
    print("ok")
