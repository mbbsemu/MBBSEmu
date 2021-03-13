.MODEL huge

.STACK 1024

_DATASUB SEGMENT

Message2 DB 'Hello, there, from sub!',13,10,'$'

_DATASUB ENDS

_TEXTSUB SEGMENT
ASSUME CS:_TEXTSUB
ASSUME ES:_DATASUB
ASSUME DS:_DATASUB

_SUB PROC
PUBLIC _SUB
; setup segments
push ds
push es

mov ax, _DATASUB
mov ds, ax
mov es, ax

; prints a message
mov ah,9
mov dx, OFFSET Message2
int 21h

pop es
pop ds

retf

_TEXTSUB ENDS
ENDP _SUB

END
