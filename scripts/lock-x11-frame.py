#!/usr/bin/env python3
"""Pin an X11 window to its mapped pixel size (no maximize / drag-grow)."""
from __future__ import annotations

import argparse
import ctypes
import os
import re
import subprocess
import sys
import time

PMinSize = 1 << 4
PMaxSize = 1 << 5

MWM_HINTS_FUNCTIONS = 1
MWM_HINTS_DECORATIONS = 2
MWM_FUNC_MOVE = 4
MWM_FUNC_MINIMIZE = 8
MWM_FUNC_CLOSE = 32
MWM_DECOR_BORDER = 2
MWM_DECOR_TITLE = 8
MWM_DECOR_MENU = 16
MWM_DECOR_MINIMIZE = 32


class XSizeHints(ctypes.Structure):
    _fields_ = [
        ("flags", ctypes.c_long),
        ("x", ctypes.c_int),
        ("y", ctypes.c_int),
        ("width", ctypes.c_int),
        ("height", ctypes.c_int),
        ("min_width", ctypes.c_int),
        ("min_height", ctypes.c_int),
        ("max_width", ctypes.c_int),
        ("max_height", ctypes.c_int),
        ("width_inc", ctypes.c_int),
        ("height_inc", ctypes.c_int),
        ("min_aspect_x", ctypes.c_int),
        ("min_aspect_y", ctypes.c_int),
        ("max_aspect_x", ctypes.c_int),
        ("max_aspect_y", ctypes.c_int),
        ("base_width", ctypes.c_int),
        ("base_height", ctypes.c_int),
        ("win_gravity", ctypes.c_int),
    ]


class XWindowAttributes(ctypes.Structure):
    _fields_ = [
        ("x", ctypes.c_int),
        ("y", ctypes.c_int),
        ("width", ctypes.c_int),
        ("height", ctypes.c_int),
        ("border_width", ctypes.c_int),
        ("depth", ctypes.c_int),
        ("visual", ctypes.c_void_p),
        ("root", ctypes.c_ulong),
        ("class_", ctypes.c_int),
        ("bit_gravity", ctypes.c_int),
        ("win_gravity", ctypes.c_int),
        ("backing_store", ctypes.c_int),
        ("backing_planes", ctypes.c_ulong),
        ("backing_pixel", ctypes.c_ulong),
        ("save_under", ctypes.c_int),
        ("colormap", ctypes.c_ulong),
        ("map_installed", ctypes.c_int),
        ("map_state", ctypes.c_int),
        ("all_event_masks", ctypes.c_long),
        ("your_event_mask", ctypes.c_long),
        ("do_not_propagate_mask", ctypes.c_long),
        ("override_redirect", ctypes.c_int),
        ("screen", ctypes.c_void_p),
    ]


def _ids_from_xprop() -> list[int]:
    try:
        raw = subprocess.check_output(
            ["xprop", "-root", "_NET_CLIENT_LIST"],
            stderr=subprocess.DEVNULL,
            text=True,
        )
    except (OSError, subprocess.CalledProcessError):
        return []
    return [int(x, 16) for x in re.findall(r"0x[0-9a-fA-F]+", raw)]


def _prop(wid: int, name: str) -> str:
    try:
        return subprocess.check_output(
            ["xprop", "-id", hex(wid), name],
            stderr=subprocess.DEVNULL,
            text=True,
        )
    except (OSError, subprocess.CalledProcessError):
        return ""


def find_window(pid: int, wmclass: str, timeout: float) -> int | None:
    deadline = time.monotonic() + timeout
    needle = wmclass.lower()
    while time.monotonic() < deadline:
        for wid in _ids_from_xprop():
            pid_txt = _prop(wid, "_NET_WM_PID")
            class_txt = _prop(wid, "WM_CLASS")
            if not re.search(rf"= {pid}\b", pid_txt):
                continue
            if needle and needle not in class_txt.lower():
                continue
            return wid
        time.sleep(0.05)
    return None


def lock(wid: int, args: argparse.Namespace) -> None:
    x11 = ctypes.CDLL("libX11.so.6")
    x11.XOpenDisplay.restype = ctypes.c_void_p
    x11.XOpenDisplay.argtypes = [ctypes.c_char_p]
    dpy = x11.XOpenDisplay(None)
    if not dpy:
        raise RuntimeError("no DISPLAY")

    attrs = XWindowAttributes()
    x11.XGetWindowAttributes.argtypes = [
        ctypes.c_void_p,
        ctypes.c_ulong,
        ctypes.POINTER(XWindowAttributes),
    ]
    for _ in range(20):
        if x11.XGetWindowAttributes(dpy, wid, ctypes.byref(attrs)) == 0:
            time.sleep(0.05)
            continue
        if attrs.width >= 80 and attrs.height >= 80:
            break
        time.sleep(0.05)

    w, h = int(attrs.width), int(attrs.height)
    hints = XSizeHints()
    supplied = ctypes.c_long()
    x11.XGetWMNormalHints.argtypes = [
        ctypes.c_void_p,
        ctypes.c_ulong,
        ctypes.POINTER(XSizeHints),
        ctypes.POINTER(ctypes.c_long),
    ]
    x11.XGetWMNormalHints(dpy, wid, ctypes.byref(hints), ctypes.byref(supplied))
    hints.flags = PMinSize | PMaxSize
    hints.min_width = hints.max_width = w
    hints.min_height = hints.max_height = h
    x11.XSetWMNormalHints.argtypes = [
        ctypes.c_void_p,
        ctypes.c_ulong,
        ctypes.POINTER(XSizeHints),
    ]
    x11.XSetWMNormalHints(dpy, wid, ctypes.byref(hints))

    intern = x11.XInternAtom
    intern.restype = ctypes.c_ulong
    intern.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_int]
    motif = intern(dpy, b"_MOTIF_WM_HINTS", 0)
    data = (ctypes.c_ulong * 5)(
        MWM_HINTS_FUNCTIONS | MWM_HINTS_DECORATIONS,
        MWM_FUNC_MOVE | MWM_FUNC_MINIMIZE | MWM_FUNC_CLOSE,
        MWM_DECOR_BORDER | MWM_DECOR_TITLE | MWM_DECOR_MENU | MWM_DECOR_MINIMIZE,
        0,
        0,
    )
    x11.XChangeProperty.argtypes = [
        ctypes.c_void_p,
        ctypes.c_ulong,
        ctypes.c_ulong,
        ctypes.c_ulong,
        ctypes.c_int,
        ctypes.c_int,
        ctypes.c_void_p,
        ctypes.c_int,
    ]
    x11.XChangeProperty(dpy, wid, motif, motif, 32, 0, ctypes.byref(data), 5)

    allowed = intern(dpy, b"_NET_WM_ALLOWED_ACTIONS", 0)
    xa_atom = intern(dpy, b"ATOM", 0)
    keep_names = (
        b"_NET_WM_ACTION_MOVE",
        b"_NET_WM_ACTION_MINIMIZE",
        b"_NET_WM_ACTION_CLOSE",
        b"_NET_WM_ACTION_CHANGE_DESKTOP",
        b"_NET_WM_ACTION_ABOVE",
        b"_NET_WM_ACTION_BELOW",
    )
    keep = (ctypes.c_ulong * len(keep_names))(
        *[intern(dpy, n, 0) for n in keep_names]
    )
    x11.XChangeProperty(dpy, wid, allowed, xa_atom, 32, 0, ctypes.byref(keep), len(keep_names))
    held = []
    if args.icon:
        # Before class-hint: a 256px _NET_WM_ICON used to exceed Xmaxrequest
        # and leave the titlebar empty. Taskbar still used the .desktop Icon=.
        _set_net_wm_icon(x11, dpy, intern, wid, args.icon)
        _set_wm_icon_pixmap(x11, dpy, wid, args.icon)
    if args.wmclass:
        _set_class_hint(x11, dpy, wid, args.wmclass, held)
    desk = (args.desktop or desktop_id_for_class(args.wmclass)).strip()
    if desk:
        _set_desktop_file(x11, dpy, intern, wid, desk)
    if args.icon:
        _set_net_wm_icon(x11, dpy, intern, wid, args.icon)
        _set_wm_icon_pixmap(x11, dpy, wid, args.icon)
    x11.XFlush(dpy)
    x11.XSync.argtypes = [ctypes.c_void_p, ctypes.c_int]
    x11.XSync(dpy, 0)
    x11.XCloseDisplay(dpy)
    del held


def desktop_id_for_class(wmclass: str) -> str:
    """.desktop id (no path) so Plasma's taskbar does not bind these to xterm."""
    c = (wmclass or "").strip()
    special = {
        "FinnsRealmWGBBS": "finns-realm-bbs",
        "FinnsRealmWGNew": "finns-realm-new",
        "FinnsRealmKlymacks": "finns-realm-dos-klymacks",
        "FinnsRealmDosReboot": "finns-realm-dos-reboot",
        "FinnsRealmMatt": "finns-realm-matt",
        "FinnsRealm": "finns-realm-dos-klymacks",
    }
    if c in special:
        return special[c]
    if c.startswith("FinnsRealmWG"):
        return "finns-realm-" + c[len("FinnsRealmWG") :].lower()
    return ""


class XClassHint(ctypes.Structure):
    _fields_ = [
        ("res_name", ctypes.c_char_p),
        ("res_class", ctypes.c_char_p),
    ]


def _set_class_hint(x11, dpy, wid: int, wmclass: str, held: list) -> None:
    raw = wmclass.encode("utf-8")
    name_buf = ctypes.create_string_buffer(raw)
    class_buf = ctypes.create_string_buffer(raw)
    held.append(name_buf)
    held.append(class_buf)
    hint = XClassHint(name_buf, class_buf)
    held.append(hint)
    x11.XSetClassHint.argtypes = [
        ctypes.c_void_p,
        ctypes.c_ulong,
        ctypes.POINTER(XClassHint),
    ]
    x11.XSetClassHint(dpy, wid, ctypes.byref(hint))


def _set_desktop_file(x11, dpy, intern, wid: int, desktop: str) -> None:
    name = desktop.removesuffix(".desktop").encode("utf-8")
    if not name:
        return
    atom = intern(dpy, b"_KDE_NET_WM_DESKTOP_FILE", 0)
    utf8 = intern(dpy, b"UTF8_STRING", 0)
    buf = ctypes.create_string_buffer(name)
    x11.XChangeProperty(dpy, wid, atom, utf8, 8, 0, buf, len(name))


# Titlebar icons. 256×256 alone is ~262KB and Xmaxrequest is often 256KB, so
# the whole _NET_WM_ICON replace used to fail and KWin drew an empty title.
_NET_WM_ICON_SIZES = (16, 32, 48)


def _argb_icon(path: str, sizes: tuple[int, ...] = _NET_WM_ICON_SIZES) -> list[int]:
    """CARDINAL payload: width, height, ARGB pixels, repeated per size."""
    from PIL import Image

    out: list[int] = []
    src = Image.open(path).convert("RGBA")
    for size in sizes:
        im = src.resize((size, size), Image.Resampling.NEAREST)
        out.append(size)
        out.append(size)
        for r, g, b, a in im.getdata():
            out.append((a << 24) | (r << 16) | (g << 8) | b)
    return out


def _set_net_wm_icon(x11, dpy, intern, wid: int, path: str) -> None:
    if not os.path.isfile(path):
        return
    try:
        values = _argb_icon(path)
    except Exception:
        return
    if not values:
        return
    atom = intern(dpy, b"_NET_WM_ICON", 0)
    cardinal = intern(dpy, b"CARDINAL", 0)
    buf = (ctypes.c_ulong * len(values))(*values)
    x11.XChangeProperty(dpy, wid, atom, cardinal, 32, 0, ctypes.byref(buf), len(values))


IconPixmapHint = 1 << 2
IconMaskHint = 1 << 5


class XWMHints(ctypes.Structure):
    _fields_ = [
        ("flags", ctypes.c_long),
        ("input", ctypes.c_int),
        ("initial_state", ctypes.c_int),
        ("icon_pixmap", ctypes.c_ulong),
        ("icon_window", ctypes.c_ulong),
        ("icon_x", ctypes.c_int),
        ("icon_y", ctypes.c_int),
        ("icon_mask", ctypes.c_ulong),
        ("window_group", ctypes.c_ulong),
    ]


def _xpm_beside_png(path: str) -> str:
    folder = os.path.dirname(path)
    for name in ("finns-realm_32x32.xpm", "finns-realm_16x16.xpm"):
        candidate = os.path.join(folder, name)
        if os.path.isfile(candidate):
            return candidate
    return ""


def _set_wm_icon_pixmap(x11, dpy, wid: int, png_path: str) -> None:
    """WM_HINTS pixmap — Breeze titlebar still reads this when _NET_WM_ICON is late."""
    xpm_path = _xpm_beside_png(png_path)
    if not xpm_path:
        return
    lib = None
    for soname in ("libXpm.so.4", "libXpm.so.1", "libXpm.so"):
        try:
            lib = ctypes.CDLL(soname)
            break
        except OSError:
            continue
    if lib is None:
        return
    pixmap = ctypes.c_ulong(0)
    mask = ctypes.c_ulong(0)
    lib.XpmReadFileToPixmap.argtypes = [
        ctypes.c_void_p,
        ctypes.c_ulong,
        ctypes.c_char_p,
        ctypes.POINTER(ctypes.c_ulong),
        ctypes.POINTER(ctypes.c_ulong),
        ctypes.c_void_p,
    ]
    lib.XpmReadFileToPixmap.restype = ctypes.c_int
    if lib.XpmReadFileToPixmap(
        dpy, wid, xpm_path.encode("utf-8"), ctypes.byref(pixmap), ctypes.byref(mask), None
    ):
        return
    x11.XGetWMHints.restype = ctypes.POINTER(XWMHints)
    x11.XGetWMHints.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
    x11.XAllocWMHints.restype = ctypes.POINTER(XWMHints)
    x11.XAllocWMHints.argtypes = []
    x11.XSetWMHints.argtypes = [
        ctypes.c_void_p,
        ctypes.c_ulong,
        ctypes.POINTER(XWMHints),
    ]
    hints = x11.XGetWMHints(dpy, wid)
    if not hints:
        hints = x11.XAllocWMHints()
        if not hints:
            return
    hints.contents.icon_pixmap = pixmap.value
    hints.contents.flags |= IconPixmapHint
    if mask.value:
        hints.contents.icon_mask = mask.value
        hints.contents.flags |= IconMaskHint
    x11.XSetWMHints(dpy, wid, hints)
    x11.XFree.argtypes = [ctypes.c_void_p]
    x11.XFree(hints)


def main() -> int:
    if not os.environ.get("DISPLAY"):
        return 0
    p = argparse.ArgumentParser()
    p.add_argument("--pid", type=int, required=True)
    p.add_argument("--class", dest="wmclass", default="")
    p.add_argument("--desktop", default="")
    p.add_argument("--icon", default="")
    p.add_argument("--timeout", type=float, default=4.0)
    args = p.parse_args()
    wid = find_window(args.pid, args.wmclass, args.timeout)
    if not wid:
        return 0
    try:
        lock(wid, args)
    except Exception as exc:
        print(f"lock-x11-frame: {exc}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
