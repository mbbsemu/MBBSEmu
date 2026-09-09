"""Mud verbs the play modules emit. Clients type these; they do not invent them."""

from __future__ import annotations

from .party import HEAL_ASK, HEALED_SAY, JOIN_CALL, REST_CALL, RESTED_SAY, rank_cmd


def follow(name: str) -> str:
    who = (name or "").strip()
    return f"follow {who}" if who else ""


def invite(name: str) -> str:
    who = (name or "").strip()
    return f"invite {who}" if who else ""


def rank(row: str) -> str:
    return rank_cmd(row)


def heal_ask() -> str:
    return HEAL_ASK


def healed() -> str:
    return HEALED_SAY


def rest_call() -> str:
    return REST_CALL


def rested() -> str:
    return RESTED_SAY


def join_call() -> str:
    return JOIN_CALL
