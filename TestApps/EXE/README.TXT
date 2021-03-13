=====================
Overview
=====================

A sample DOS program written in assembly which contains multiple segments.

The entry point uses a distinct code and data segment and makes a far call to
a subroutine that exists in a separate code segment which uses a different data
segment.

=====================
Compilation
=====================

Run each .ASM through tasm, and then tlink both when done.

TASM EXE.ASM
TASM SUB.ASM

TLINK EXE.OBJ SUB.OBJ

# now you have EXE.EXE
