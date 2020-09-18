# MBBSEmu Memory Map
Each Module in MBBSEmu is provided a dedicated, full 16-bit address space. Meaning each module is capable of addressing up to 16GB of Memory per module. 

This markdown file is to map how the memory is allocated within MBBSEmu.

## Active Memory Map
Addresses in the Active Memory Map can be accessed and manipulated during program operation as they represent underlying data the given address

|Start|End|Size|Definition|
|--|--|--|--|
|0x0000:0x0000|0x0000:0xFFFF|65KB|CPU Stack|
|0x0001:0x0000|0x00FF:0xFFFF|~16MB|Code/Data Segments|
|0x0100:0x0000|0x01FF:0xFFFF|16MB|Variable Data|
|0x0200:0x0000|0x02FF:0xFFFF|16MB|Real Mode Data|
|0x1000:0x0000|0x1000:0x00C0|192B|GENBB Btrieve Struct (Set in MAJORBBS.CS)|
|0x1001:0x0000|0x1001:0x00C0|192B|ACCBB Btrieve Struct (Set in MAJORBBS.CS)|

## Virtual Memory Map
Virtual Memory Addresses are used to denote a translation to another function within MBBSEmu.

### Exported Ordinals
During relocation, records in the Imported Name Table are replaced with Segments that correspond to their Imported Function. These functions (Exported by MajorBBS/Worldgroup), provide additional functionality for Modules compiled against the SDK.

**Segment 0xFFFF** - Corresponds to the Exported Module **MAJORBBS**, where the offset of the address corresponds to the ordinal of the function/property being used.

**Segment 0xFFFE** - Corresponds to the Exported Module **GALGSBL**, where the offset of the address corresponds to the ordinal of the function/property being used.

**Segment 0xFFFD** - Corresponds to the Exported Module **PHAPI**, where the offset of the address corresponds to the ordinal of the function/property being used.

**Segment 0xFFFC** - Corresponds to the Exported Module **GALME**, where the offset of the address corresponds to the ordinal of the function/property being used.

**Segment 0xFFFC** - Corresponds to the Exported Module **DOSCALLS**, where the offset of the address corresponds to the ordinal of the function/property being used.