# Helper Scripts

MBBSEmu includes various scripts to help maintain your configuration.

## mmud_edit.py

**mmud_edit.py** is a [Python3](http://www.python.org) script to edit player data from within **WCCUSERS.DB**. It supports MajorMUD 1.11p and should not be used with any other versions. It should be run when your system is offline because it won't be able to access the sqlite3 database at the same time as MBBSEmu.

### Usage

```
mmud_edit.py --username=USERNAME [--experience=EXPERIENCE]
```

> **Note:** **mmud_edit.py** should be run from the same directory that **WCCUSERS.DB** resides.

