"""Loopback-only telnet. This is a local test, not a public BBS."""

from __future__ import annotations

import ipaddress

LOOPBACK_HOSTS = frozenset(
    {
        "127.0.0.1",
        "localhost",
        "::1",
        "[::1]",
        "0:0:0:0:0:0:0:1",
    }
)

_REFUSE = "local telnet only (loopback or this box's KVM LAN) — not a public BBS"

# libvirt default NAT. The Win11 Worldgroup guest lives here (FINN).
_KVM_LAN = ipaddress.ip_network("192.168.122.0/24")


class LocalOnly(OSError):
    """Raised when a host or bind is not on the loopback."""


def _strip_host(host: str) -> str:
    name = host.strip().lower().rstrip(".")
    if name.startswith("[") and name.endswith("]"):
        return name[1:-1]
    return name


def is_loopback_host(host: str) -> bool:
    """True for localhost names and loopback IPs. Does not resolve DNS."""
    name = _strip_host(host)
    if not name:
        return False
    if name in LOOPBACK_HOSTS:
        return True
    try:
        return ipaddress.ip_address(name).is_loopback
    except ValueError:
        return False


def is_loopback_addr(addr: object) -> bool:
    """True for a peer/bind IP (no DNS)."""
    if addr is None:
        return False
    return is_loopback_host(str(addr))


def is_local_play_host(host: str) -> bool:
    """Loopback, or the default libvirt NAT (Worldgroup VM). Not the internet."""
    if is_loopback_host(host):
        return True
    name = _strip_host(host)
    try:
        return ipaddress.ip_address(name) in _KVM_LAN
    except ValueError:
        return False


def require_loopback_host(host: str) -> str:
    """Return host, or raise LocalOnly. Never opens a socket."""
    if is_loopback_host(host):
        return host.strip()
    raise LocalOnly(_REFUSE)


def require_loopback_bind(host: str) -> str:
    """Bind address for a listener. Rejects 0.0.0.0 / :: / any public IP."""
    name = _strip_host(host)
    if name in {"0.0.0.0", "::", "[::]", "*"}:
        raise LocalOnly(_REFUSE)
    return require_loopback_host(host)
