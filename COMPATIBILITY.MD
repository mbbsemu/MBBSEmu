# MBBSEmu Compatibility List
Because MBBSEmu is technically two emulators in one (MajorBBS Host Process Emulator & x86_16 Processor Emulator), not every module will work correctly until MBBSEmu has greater code implementatoin of the 1000+ MajorBBS/Worldgroup imported functions and the full x86_16 instruction set.

# Compatible Modules
|Module Name|ISV|Compatibility Notes|
|---|---|---
|Archery v1.5|GWW|High Scores & Payout Not Calculating Properly|

# Incompatible Modules
|Module Name|ISV|Compatibility Notes|
|---|---|---|
|Farwest Trivia|Farwest BBS|EXTERN to custom DLL's. They will need to be loaded in addition to the module and setup as imported modules|