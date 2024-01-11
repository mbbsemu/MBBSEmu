#!/usr/bin/python3

import argparse
import base64
import hashlib
import os
import sqlite3
import sys

def _create_parser():
  parser = argparse.ArgumentParser(description='Create MBBSEmu user accounts.')
  parser.add_argument('--username', help='Username to create', required=True)
  parser.add_argument('--password', help='Password to use', required=True)
  parser.add_argument('--keys', help='Account keys to add to the new account', action='append', default=['NORMAL','PAYING'])
  parser.add_argument('--email', help='Email address to use', default='test@test.bbs')

  return parser.parse_args()

def _make_password_hash(password, salt_bytes):
 m = hashlib.sha512()
 m.update(password.encode(encoding = 'UTF-8', errors = 'strict'))
 m.update(salt_bytes)
 return m.digest()

def _main():
  args = _create_parser()

  conn = sqlite3.connect('mbbs.db')

  passwordSaltBytes=os.urandom(32)
  passwordHashBytes=_make_password_hash(args.password, passwordSaltBytes)

  cur = conn.cursor()
  t = (args.username, str(base64.b64encode(passwordHashBytes), encoding='utf-8'), str(base64.b64encode(passwordSaltBytes), encoding='utf-8'), args.email)
  cur.execute('INSERT INTO Accounts (userName, passwordHash, passwordSalt, email, createDate, updateDate) VALUES (?,?,?,?, datetime(\'now\'), datetime(\'now\'))', t)
  conn.commit()

  account_id = cur.lastrowid

  for user_key in args.keys:
    t = (account_id, user_key)
    cur.execute('INSERT INTO AccountKeys (accountId, accountKey, createDate, updateDate) VALUES (?,?,datetime(\'now\'), datetime(\'now\'))', t)
    conn.commit()

if __name__ == '__main__':
  _main()
