#!/usr/bin/python3

import argparse
import sqlite3
import sys

def _create_parser():
  parser = argparse.ArgumentParser(description='Edit your 1.11p MMUD character in MBBSEmu.')
  parser.add_argument('--username', help='Username to edit', required=True)
  parser.add_argument('--experience', help='Experience value to set', type=int)

  return parser.parse_args()

def _main():
  args = _create_parser()

  conn = sqlite3.connect('WCCUSERS.DB')

  c = conn.cursor()
  t = (args.username,)
  c.execute('SELECT data FROM data_t WHERE key_0=?', t)

  data = c.fetchone()
  if data is None:
    print('Username not found in WCCUSERS.DB')
    return

  b = bytearray(data[0])

  #exp
  if args.experience is not None:
    #print('Experience is {}'.format(args.experience))
    b[0x46F] = args.experience & 0xFF   # low byte is validated
    b[0x470] = ((args.experience >> 8) & 0xFF)
    b[0x471] = ((args.experience >> 16) & 0xFF)
    b[0x472] = ((args.experience >> 24) & 0xFF)

  # copper farthings (32-bit int) low-byte @ 0x60F
  # silver low-byte @ 0x60B
  # gold low-byte @ 0x607
  # platinum low-byte @ 0x603
  # runic low-byte @ 0x5FF

  # and write it back
  t = (sqlite3.Binary(b), args.username,)
  c = conn.cursor()
  c.execute('UPDATE data_t SET data=? WHERE key_0=?', t)
  conn.commit()

if __name__ == '__main__':
  _main()
