#!/usr/bin/python3

import argparse
import sqlite3
import sys
from pathlib import Path


def _create_parser():
  parser = argparse.ArgumentParser(description='Edit your 1.11p MMUD character in MBBSEmu.')
  parser.add_argument('--username', help='Username to edit', required=True)
  parser.add_argument('--experience', help='Experience value to set', type=int)
  parser.add_argument('--db', help='WCCUSERS sqlite file', default='')

  return parser.parse_args()


def _open_users(path: str) -> sqlite3.Connection:
  candidates = []
  if path:
    candidates.append(Path(path))
  else:
    cwd = Path('.')
    candidates.extend([cwd / 'WCCUSERS.db', cwd / 'WCCUSERS.DB'])
  for p in candidates:
    if not p.is_file() or p.stat().st_size == 0:
      continue
    conn = sqlite3.connect(p)
    try:
      conn.execute('SELECT 1 FROM data_t LIMIT 1')
    except sqlite3.Error:
      conn.close()
      continue
    print(f'using {p}')
    return conn
  raise SystemExit('no WCCUSERS sqlite with data_t (board must be down; use WCCUSERS.db)')


def _main():
  args = _create_parser()

  conn = _open_users(args.db)

  c = conn.cursor()
  t = (args.username,)
  c.execute('SELECT data FROM data_t WHERE key_0=?', t)

  data = c.fetchone()
  if data is None:
    print('Username not found in WCCUSERS')
    return 1

  b = bytearray(data[0])

  #exp
  if args.experience is not None:
    b[0x46F] = args.experience & 0xFF   # low byte is validated
    b[0x470] = ((args.experience >> 8) & 0xFF)
    b[0x471] = ((args.experience >> 16) & 0xFF)
    b[0x472] = ((args.experience >> 24) & 0xFF)

  t = (sqlite3.Binary(b), args.username,)
  c = conn.cursor()
  c.execute('UPDATE data_t SET data=? WHERE key_0=?', t)
  conn.commit()
  print(f'{args.username} experience -> {args.experience}')
  return 0

if __name__ == '__main__':
  raise SystemExit(_main())
