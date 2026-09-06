"""Loopback-only: no public BBS bind or dial-out."""

from __future__ import annotations

from client.localnet import (
    LocalOnly,
    is_loopback_addr,
    is_loopback_host,
    require_loopback_bind,
    require_loopback_host,
)


def test_loopback_hosts() -> None:
    assert is_loopback_host("127.0.0.1")
    assert is_loopback_host("localhost")
    assert is_loopback_host("LOCALHOST")
    assert is_loopback_host("::1")
    assert is_loopback_host("[::1]")
    assert is_loopback_host("127.0.0.99")
    assert is_loopback_addr("127.0.0.1")


def test_refuse_public() -> None:
    for host in (
        "0.0.0.0",
        "::",
        "*",
        "playmajormud.com",
        "adeptbbs.com",
        "8.8.8.8",
        "192.168.1.10",
        "192.168.122.33",
        "",
    ):
        assert not is_loopback_host(host), host
        try:
            require_loopback_host(host)
        except LocalOnly:
            pass
        else:
            raise AssertionError(f"allowed remote host {host!r}")


def test_refuse_wildcard_bind() -> None:
    for host in ("0.0.0.0", "::", "*"):
        try:
            require_loopback_bind(host)
        except LocalOnly:
            continue
        raise AssertionError(f"allowed public bind {host!r}")
    assert require_loopback_bind("127.0.0.1") == "127.0.0.1"


def test_kvm_lan_play_host() -> None:
    from client.localnet import is_local_play_host

    assert is_local_play_host("127.0.0.1")
    assert is_local_play_host("192.168.122.33")
    assert is_local_play_host("192.168.122.1")
    assert not is_local_play_host("192.168.1.10")
    assert not is_local_play_host("8.8.8.8")
    assert not is_local_play_host("playmajormud.com")


if __name__ == "__main__":
    test_loopback_hosts()
    test_refuse_public()
    test_refuse_wildcard_bind()
    test_kvm_lan_play_host()
    print("localnet checks ok")
