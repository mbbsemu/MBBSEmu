# x86_16 (80286) Opcode Support
The following symbols denote the specific level of support:
|Symbol|Meaning|
|:---:|:--|
|:white_check_mark:|Full Support|
|:large_orange_diamond:|Partial Support|
|:red_circle:|Not Supported|

Below are all supported 80286 Opcodes and their level of support in MBBSEmu:

|Opcode|Definition|Supported|
|:---:|:---|:---:|
|AAA|ASCII adjust AL after addition|:red_circle:|
|AAD|ASCII adjust AX before division|:red_circle:|
|AAM|ASCII adjust AX after multiplication|:red_circle:|
|AAS|ASCII adjust AL after subtraction|:red_circle:|
|ADC|Add with carry|:white_check_mark:|
|ADD|Add|:white_check_mark:|
|AND|Logical AND|:white_check_mark:|
|BOUND|Check array index against bounds|:red_circle:|
|CALL|Call procedure|:large_orange_diamond:|
|CBW|Convert byte to word|:white_check_mark:|
|CLC|Clear carry flag|:white_check_mark:|
|CLD|Clear direction flag|:white_check_mark:|
|CLI|Clear interrupt flag|:white_check_mark:|
|CMC|Complement carry flag|:red_circle:|
|CMP|Compare operands|:white_check_mark:|
|CMPSB|Compare bytes in memory|:red_circle:|
|CMPSW|Compare words|:red_circle:|
|CWD|Convert word to doubleword|:white_check_mark:|
|DAA|Decimal adjust AL after addition|:red_circle:|
|DAS|Decimal adjust AL after subtraction|:red_circle:|
|DEC|Decrement by 1|:white_check_mark:|
|DIV|Unsigned divide|:white_check_mark:|
|ENTER|Enter stack frame|:white_check_mark:|
|ESC|Used with floating-point unit|:red_circle:|
|HLT|Enter halt state|:red_circle:|
|IDIV|Signed divide|:white_check_mark:|
|IMUL|Signed multiply|:white_check_mark:|
|IN|Input from port|:red_circle:|
|INC|Increment by 1|:white_check_mark:|
|INS|Input from port to string|:red_circle:|
|INT|Call interrupt|:large_orange_diamond:|
|INTO|Call interrupt if overslow|:red_circle:|
|IRET|Return from interrupt|:red_circle:|
|Jcc|Jump if condition|:white_check_mark:|
|JCXZ|Jump if CX is zero|:white_check_mark:|
|JMP|Unconditional Jump|:white_check_mark:|
|LAHF|Load FLAGS into AH register|:red_circle:|
|LDS|Load pointer using DS|:white_check_mark:|
|LEA|Load Effective Address|:white_check_mark:|
|LEAVE|Leave stack frame|:white_check_mark:|
|LES|Lead ES with pointer|:white_check_mark:|
|LOCK|Assert BUS LOCK# signal|:red_circle:|
|LODSB|Load string byte|:white_check_mark:|
|LODSW|Load string word|:red_circle:|
|LOOP|Loop control|:white_check_mark:|
|MOV|Move|:white_check_mark:|
|MOVSB|Move byte from string to string|:red_circle:|
|MOVSW|Move word from string to string|:red_circle:|
|MUL|Unsigned multiply|:red_circle:|
|NEG|Two's complement negation|:white_check_mark:|
|NOP|No operation|:white_check_mark:|
|NOT|Negate the operand, logical NOT|:white_check_mark:|
|OR|Logical OR|:white_check_mark:|
|OUT|Output to port|:red_circle:|
|OUTS|Output string to port|:red_circle:|
|POP|Pop data from stack|:white_check_mark:|
|POPA|Pop call general purpose registers from stack|:red_circle:|
|POPF|Pop FLAGS register from stack|:red_circle:|
|PUSH|Push data onto stack|:white_check_mark:|
|PUSHA|Push all general purpose registers onto stack|:red_circle:|
|PUSHF|Push flags onto stack|:white_check_mark:|
|RCL|Rotate left (with carry)|:white_check_mark:|
|RCR|Rotate right (with carry)|:white_check_mark:|
|REPxx|Repeat MOVS/STOS/CMPS/LODS/SCAS|:red_circle:|
|RET|Return from procedure|:white_check_mark:|
|RETF|Return from far procedure|:white_check_mark:|
|RETN|Return from near procedure|:red_circle:|
|ROL|Rotate left|:red_circle:|
|ROR|Rotate right|:red_circle:|
|SAHF|Store AH into FLAGS|:white_check_mark:|
|SAL|Shift Arithmetically left|:red_circle:|
|SAR|Shift Arithmetically right|:white_check_mark:|
|SBB|Subtract with borrow|:white_check_mark:|
|SCASB|Compare byte string|:red_circle:|
|SCASW|Compare word string|:red_circle:|
|SHL|Shift left (unsigned shift left)|:white_check_mark:|
|SHR|Shift right (unsight shift right)|:white_check_mark:|
|STC|Set carry flag|:red_circle:|
|STD|Set direction flag|:red_circle:|
|STR|Set interrupt flag|:red_circle:|
|STOSB|Store byte in string|:white_check_mark:|
|STOSW|Store word in string|:white_check_mark:|
|SUB|Subtraction|:white_check_mark:|
|TEST|Logical compare (AND)|:white_check_mark:|
|WAIT|Wait until not busy|:red_circle:|
|XCHG|Exchange data|:white_check_mark:|
|XLAT|Table look-up translation|:red_circle:|
|XOR|Exclusive OR|:white_check_mark:|
