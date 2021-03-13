EXTRN _SUB

.MODEL huge

.STACK 1024

_DATA SEGMENT

Message DB 'Hello, there!',13,10,'$'
Number DW 43707
Update DW 17459

_DATA ENDS

ORG 0100h

_TEXT SEGMENT
ASSUME CS:_TEXT
ASSUME ES:_DATA
ASSUME DS:_DATA

_main:
; setup segments
mov ax, _DATA
mov ds, ax
mov es, ax

; prints a message
mov ah,9
mov dx, OFFSET Message
int 21h

call far ptr _SUB

; exits the program
mov ah,4ch
int 21h

_TEXT ENDS
end _main

END
