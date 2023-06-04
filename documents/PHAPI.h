/* PHAPI.H - 286|DOS-Extender Phar Lap API definition file */

/************************************************************************/
/* Copyright (C) 1986-1990 Phar Lap Software, Inc. */
/* Unpublished - rights reserved under the Copyright Laws of the */
/* United States. Use, duplication, or disclosure by the */
/* Government is subject to restrictions as set forth in */
/* subparagraph (c)(1)(ii) of the Rights in Technical Data and */
/* Computer Software clause at 252.227-7013. */
/* Phar Lap Software, Inc., 60 Aberdeen Ave., Cambridge, MA 02138 */
/************************************************************************/

/* $Id: phapi.h 1.7 92/03/11 11:05:07 rms Exp $ */

/*

This file provides C data structure declarations and function
prototypes that define the Phar Lap Application Program Interface
(PHAPI) for use in 286|DOS-Extender programs. The functions declared
in this file provide the following services:

o manipulating protected mode selectors
o manipulating pages on the 80386
o allocated conventional (DOS) memory
o interrupt and exception handling
o communicating with real mode code
o dynamic linking
o miscellaneous services

The code for PHAPI is located in PHAPI.DLL, a dynamic link
library that is part of 286|DOS-Extender. In addition to #including
PHAPI.H, programs that use the PHAPI should also link in the import
library, PHAPI.LIB.

All PHAPI functions must be invoked with a FAR call. With the
exception of three functions that allow a variable number of
arguments, all PHAPI routines use the Pascal calling convention. In
the following function prototypes, APIENTRY designates a FAR PASCAL
function.

286|DOS-Extender provides character-mode OS/2 API calls for
use by protected mode MS-DOS programs. The OS/2 API data structures
and function prototypes are declared in the file OS2.H, provided with
Microsoft C and compatible compilers (you DO NOT need the OS/2 SDK!).

The functions declared in this file, PHAPI.H, are those PHAPI
functions which are not already declared in OS2.H or in its helper
include files such as OS2DEF.H, BSEDOS.H, or BSESUB.H. This file,
PHAPI.H, automatically includes (and requires) the Microsoft C
OS2DEF.H include file. Most programs that #include will
probably also need to #include .

*/
#ifndef PHAPI_H_INCLUDED
#define PHAPI_H_INCLUDED

/*

If the OS/2 .H files haven't been included, then define the
following base types.

*/

#ifndef OS2DEF_INCLUDED
#define OS2DEF_INCLUDED

#ifdef __cplusplus
extern "C"
{
#endif

#ifndef CHAR
    typedef char CHAR;
#endif
#ifndef INT
    typedef int INT;
#endif
    typedef short SHORT;
    typedef long LONG;
    typedef int BOOL;
    typedef unsigned char BYTE;

    typedef unsigned char UCHAR;
    typedef unsigned int UINT;
    typedef unsigned short USHORT;
    typedef unsigned long ULONG;

    typedef char _far *PCHAR;
    typedef int _far *PINT;
    typedef short _far *PSHORT;
    typedef long _far *PLONG;

    typedef unsigned char _far *PUCHAR;
    typedef unsigned int _far *PUINT;
    typedef unsigned short _far *PUSHORT;
    typedef unsigned long _far *PULONG;

    typedef unsigned short SEL;
    typedef unsigned short _far *PSEL;
    typedef unsigned short HMODULE;
    typedef unsigned short _far *PHMODULE;
    typedef void _far *PVOID;
    typedef unsigned char _far *PSZ;
    typedef void(pascal _far *PFN)();
    typedef PFN _far *PPFN;

#define VOID void
#define APIENTRY pascal _far

    /*

    Utility macros

    */

#define MAKEP(sel, off) ((PVOID)((((ULONG)(sel)) << 16) + (off)))

#define SELECTOROF(fp) ((SEL)(((ULONG)(fp)) >> 16))

#define OFFSETOF(fp) ((UINT)(ULONG)(fp))

#define MAKETYPE(var, type) (*((type _far *)&var))

#define FIELDOFFSET(type, field) ((UINT) & (((type *)0)->field))

#define MAKELONG(lo, hi) ((((LONG)(hi)) << 16) + (USHORT)(lo))

#define MAKEULONG(lo, hi) ((((ULONG)(hi)) << 16) + (USHORT)(lo))

#define MAKESHORT(lo, hi) ((((USHORT)(hi)) << 8) + (UCHAR)(lo))

#define MAKEUSHORT(lo, hi) ((((USHORT)(hi)) << 8) + (UCHAR)(lo))

#define LOBYTE(val) ((UCHAR)(val))

#define HIBYTE(val) ((UCHAR)(((USHORT)(val)) >> 8))

#define LOUCHAR(val) ((UCHAR)(val))

#define HIUCHAR(val) ((UCHAR)(((USHORT)(val)) >> 8))

#define LOWORD(val) ((USHORT)(val))

#define HIWORD(val) ((USHORT)(((ULONG)(val)) >> 16))

#define LOUSHORT(val) ((USHORT)(val))

#define HIUSHORT(val) ((USHORT)(((ULONG)(val)) >> 16))

#endif

    /*

    Force PHAPI.LIB to be linked into the resulting .EXE

    */

#pragma comment(lib, "PHAPI")

    /*

    Types

    */

    typedef unsigned long REALPTR;

    /*

    Idealized segment descriptor

    */

    typedef struct
    {
        ULONG base;    /* Segment linear base address */
        ULONG size;    /* Size in bytes of segment */
        USHORT attrib; /* Attribute byte */
    } DESC;

    typedef DESC _far *PDESC;

    /*

    Segment types

    */

#define CODE16 1         /* Code segment */
#define DATA16 2         /* Data segment */
#define CODE16_NOREAD 3  /* Execute only code segment */
#define DATA16_NOWRITE 4 /* Read only data segment */

    /*

    Mask values returned by "DosVerifyAccess"

    */

#define IS_SEL 0x0001       /* Is a valid selector */
#define IS_READABLE 0x0002  /* Is readable */
#define IS_WRITEABLE 0x0004 /* Is writeable */
#define IS_CODE 0x0008      /* Is executable */

    /*

    Registers structure

    */

    typedef struct
    {
        USHORT es;
        USHORT ds;
        USHORT di;
        USHORT si;
        USHORT bp;
        USHORT sp;
        USHORT bx;
        USHORT dx;
        USHORT cx;
        USHORT ax;
        USHORT ip;
        USHORT cs;
        USHORT flags;
    } REGS16;

    typedef REGS16 _far *PREGS;

    /*

    Borland C registers for an interrupt function

    */

#ifdef __BORLANDC__
    typedef struct
    {
        USHORT bp;
        USHORT di;
        USHORT si;
        USHORT ds;
        USHORT es;
        USHORT dx;
        USHORT cx;
        USHORT bx;
        USHORT ax;
        USHORT ip;
        USHORT cs;
        USHORT flags;
    } REGS_BINT;

    typedef REGS_BINT _far *PREGS_BINT;

#define DosIsRealIntr BorIsRealIntr
#define DosIsProtIntr BorIsProtIntr
#define DosChainToRealIntr BorChainToRealIntr
#define DosChainToProtIntr BorChainToProtIntr

#endif

    /*

    DosSetExceptionHandler Stack Frame

    */

#ifndef __BORLANDC__
    typedef struct
    {
        USHORT es;
        USHORT ds;
        USHORT di;
        USHORT si;
        USHORT bp;
        USHORT sp;
        USHORT bx;
        USHORT dx;
        USHORT cx;
        USHORT ax;
        USHORT rsv1; /* reserved - glue ip */
        USHORT rsv2; /* reserved - glue cs */
        USHORT rsv3; /* reserved - glue flags */
        USHORT error_code;
        USHORT ret_ip;
        USHORT ret_cs;
        USHORT ret_flags;
        USHORT ret_rsv4; /* reserved - int sp */
        USHORT ret_rsv5; /* reserved - int ss */
    } EXCEP_FRAME;
#else
typedef struct
{
    USHORT bp;
    USHORT di;
    USHORT si;
    USHORT ds;
    USHORT es;
    USHORT dx;
    USHORT cx;
    USHORT bx;
    USHORT ax;
    USHORT rsv1; /* reserved - glue ip */
    USHORT rsv2; /* reserved - glue cs */
    USHORT rsv3; /* reserved - glue flags */
    USHORT error_code;
    USHORT ret_ip;
    USHORT ret_cs;
    USHORT ret_flags;
    USHORT ret_rsv4; /* reserved - int sp */
    USHORT ret_rsv5; /* reserved - int ss */
} EXCEP_FRAME;
#endif

    typedef EXCEP_FRAME _far *PEXCEP_FRAME;

    /*

    Interrupt and Exception handler typedef's

    */

#ifndef __BORLANDC__
    typedef void(_interrupt _far *PIHANDLER)(REGS16 regs);
#else
typedef void(_interrupt _far *PIHANDLER)(REGS_BINT regs);
#endif

    typedef void(_interrupt _far *PEHANDLER)(EXCEP_FRAME regs);

    /*

    Function prototypes

    */

    USHORT APIENTRY DosCreateDSAlias(SEL sel, PSEL aselp);

    USHORT APIENTRY DosMapLinSeg(ULONG lin_addr, ULONG size, PSEL selp);

    USHORT APIENTRY DosMapRealSeg(USHORT rm_para, ULONG size, PSEL selp);

    USHORT APIENTRY DosMapPhysSeg(ULONG phys_addr, ULONG size, PSEL selp);

    USHORT APIENTRY DosGetBIOSSeg(PSEL selp);

    USHORT APIENTRY DosGetSegDesc(SEL sel, PDESC descp);

    USHORT APIENTRY DosSetSegAttrib(SEL sel, USHORT attrib);

    USHORT APIENTRY DosVerifyAccess(SEL sel, PUSHORT flagp);

    USHORT APIENTRY DosLockSegPages(SEL sel);

    USHORT APIENTRY DosUnlockSegPages(SEL sel);

    USHORT APIENTRY DosGetPhysAddr(PVOID addr, PULONG phys_addrp, PULONG countp);

    USHORT APIENTRY DosAllocRealSeg(ULONG size, PUSHORT parap, PSEL selp);

    USHORT APIENTRY DosRealAvail(PULONG max_sizep);

    USHORT APIENTRY DosSetRealVec(USHORT int_no, REALPTR new_ptr,
                                  REALPTR _far *old_ptrp);

    USHORT APIENTRY DosGetRealVec(USHORT int_no, REALPTR _far *ptrp);

    USHORT APIENTRY DosSetProtVec(USHORT int_no, PIHANDLER new_ptr,
                                  PIHANDLER _far *old_ptrp);

    USHORT APIENTRY DosGetProtVec(USHORT int_no, PIHANDLER _far *ptrp);

    USHORT APIENTRY DosGetSavedProtVec(USHORT int_no, PIHANDLER _far *ptrp);

    USHORT APIENTRY DosSetExceptionHandler(USHORT excep_no, PEHANDLER new_ptr,
                                           PEHANDLER _far *old_ptrp);

    USHORT APIENTRY DosGetExceptionHandler(USHORT excep_no, PEHANDLER _far *ptrp);

    USHORT APIENTRY DosSetRealProtVec(USHORT int_no, PIHANDLER prot_new_ptr,
                                      REALPTR real_new_ptr,
                                      PIHANDLER _far *prot_old_ptrp,
                                      REALPTR _far *real_old_ptrp);

    USHORT APIENTRY DosGetRealProtVec(USHORT int_no, PIHANDLER _far *prot_ptrp,
                                      REALPTR _far *real_ptrp);

    USHORT APIENTRY DosSetPassToProtVec(USHORT int_no, PIHANDLER prot_new_ptr,
                                        PIHANDLER _far *prot_old_ptrp,
                                        REALPTR _far *real_old_ptrp);

    USHORT APIENTRY DosSetPassToRealVec(USHORT int_no, REALPTR real_new_ptr,
                                        PIHANDLER _far *prot_old_ptrp,
                                        REALPTR _far *real_old_ptrp);

    USHORT _far _cdecl DosRealIntr(USHORT int_no, PREGS regsp, REALPTR reserved,
                                   SHORT word_count, ...);

    USHORT APIENTRY DosVRealIntr(USHORT int_no, PREGS regsp, REALPTR reserved,
                                 SHORT word_count, PUSHORT argsp);

    USHORT _far _cdecl DosRealFarCall(REALPTR funcp, PREGS regsp,
                                      REALPTR reserved, SHORT word_count,
                                      ...);

    USHORT APIENTRY DosVRealFarCall(REALPTR funcp, PREGS regsp, REALPTR reserved,
                                    SHORT word_count, PUSHORT argsp);

    USHORT _far _cdecl DosRealICall(REALPTR funcp, PREGS regsp,
                                    REALPTR reserved, SHORT word_count,
                                    ...);

    USHORT APIENTRY DosVRealICall(REALPTR funcp, PREGS regsp, REALPTR reserved,
                                  SHORT word_count, PUSHORT argsp);

    USHORT _far _cdecl DosRealFarJump(REALPTR funcp, PREGS regsp,
                                      REALPTR reserved, SHORT word_count, ...);

    USHORT APIENTRY DosVRealFarJump(REALPTR funcp, PREGS regsp, REALPTR reserved,
                                    SHORT word_count, PUSHORT argsp);

    USHORT APIENTRY DosFreeRealStack(REALPTR stack_ptr);

    USHORT _far _cdecl DosProtIntr(USHORT int_no, PREGS regsp, PVOID reserved,
                                   SHORT word_count, ...);

    USHORT APIENTRY DosVProtIntr(USHORT int_no, PREGS regsp, PVOID reserved,
                                 SHORT word_count, PUSHORT argsp);

    USHORT _far _cdecl DosProtFarCall(PFN funcp, PREGS regsp, PVOID reserved,
                                      SHORT word_count, ...);

    USHORT APIENTRY DosVProtFarCall(PFN funcp, PREGS regsp, PVOID reserved,
                                    SHORT word_count, PUSHORT argcp);

    USHORT _far _cdecl DosProtFarJump(PFN funcp, PREGS regsp, PVOID reserved,
                                      SHORT word_count, ...);

    USHORT APIENTRY DosVProtFarJump(PFN funcp, PREGS regsp, PVOID reserved,
                                    SHORT word_count, PUSHORT argsp);

    USHORT APIENTRY DosFreeProtStack(PVOID stack_ptr);

    USHORT APIENTRY DosEnumMod(PSZ name_buff, USHORT name_len, PHMODULE handp);

    USHORT APIENTRY DosEnumProc(USHORT mod_handle, PSZ name_buff,
                                PUSHORT ordinalp);

    USHORT APIENTRY DosIsPharLap(void);

    REALPTR APIENTRY DosProtToReal(PVOID ptr);

    PVOID APIENTRY DosRealToProt(REALPTR ptr);

    BOOL APIENTRY DosIsRealIntr(PVOID stack_addr);

    BOOL APIENTRY DosIsProtIntr(PVOID stack_addr);

    USHORT APIENTRY DosChainToRealIntr(REALPTR hand_ptr);

    USHORT APIENTRY DosChainToProtIntr(PIHANDLER hand_ptr);

    USHORT APIENTRY DosGetRealProcAddr(USHORT mhand, PUCHAR namep,
                                       REALPTR _far *paddrp);

    USHORT APIENTRY DosSetProcAddr(HMODULE mhand, PSZ pnamep,
                                   PFN paddr);

    /*

    Low level memory management functions

    */

    USHORT APIENTRY DosRemapLinSeg(ULONG lin_addr, ULONG size, SEL sel);

    USHORT APIENTRY DosRemapRealSeg(USHORT rm_para, ULONG size, SEL sel);

    USHORT APIENTRY DosRemapPhysSeg(ULONG phys_addr, ULONG size, SEL sel);

    USHORT APIENTRY DosAllocLinMem(ULONG size, PULONG lin_addp);

    USHORT APIENTRY DosFreeLinMem(ULONG lin_add);

    USHORT APIENTRY DosReallocLinMem(ULONG old_lin_add, ULONG new_size,
                                     PULONG new_lin_addp);

    USHORT APIENTRY DosMapLinMemToSelector(USHORT sel, ULONG lin_addr,
                                           ULONG size);

    USHORT APIENTRY DosAllocSpecificSelectors(SEL sel, USHORT count);

    USHORT APIENTRY DosFreeSelectors(SEL sel, USHORT count);

    /*

    OS/2 compatible API's

    */

#ifndef INCL_DOSPROCESS_INCLUDED
#define INCL_DOSPROCESS_INCLUDED

    typedef struct _RESULTCODES
    {
        USHORT codeTerminate;
        USHORT codeResult;
    } RESULTCODES;

    typedef RESULTCODES _far *PRESULTCODES;

    typedef void(pascal _far *PFNEXITLIST)(USHORT);

#define EXLST_ADD 1
#define EXLST_REMOVE 2
#define EXLST_EXIT 3

#define MODE_REAL 0
#define MODE_PROTECTED 1

#define TC_EXIT 0
#define TC_HARDERROR 1
#define TC_TRAP 2
#define TC_KILLPROCESS 3

#define EXEC_SYNC 0
#define EXEC_ASYNC 1
#define EXEC_ASYNCRESULT 2
#define EXEC_TRACE 3
#define EXEC_BACKGROUND 4

    USHORT APIENTRY DosExecPgm(PCHAR failp, SHORT failc,
                               USHORT flags, PSZ argsp, PSZ envp,
                               PRESULTCODES resultp, PSZ namep);

    VOID APIENTRY DosExit(USHORT flag, USHORT rc);

    USHORT APIENTRY DosExitList(USHORT code, PFNEXITLIST funcp);

    USHORT APIENTRY DosGetMachineMode(PUCHAR modep);

#endif

#ifndef INCL_DOSMEMMGR_INCLUDED
#define INCL_DOSMEMMGR_INCLUDED

    USHORT APIENTRY DosAllocHuge(USHORT nseg, USHORT lcount, PSEL selp,
                                 USHORT maxsel, USHORT flags);

    USHORT APIENTRY DosAllocSeg(USHORT size, PSEL selp, USHORT flags);

    USHORT APIENTRY DosCreateCSAlias(SEL dsel, PSEL cselp);

    USHORT APIENTRY DosGetHugeShift(PUSHORT countp);

    USHORT APIENTRY DosFreeSeg(SEL sel);

    USHORT APIENTRY DosMemAvail(PULONG availp);

    USHORT APIENTRY DosReallocHuge(USHORT nseg, USHORT lcount, SEL sel);

    USHORT APIENTRY DosReallocSeg(USHORT nsize, SEL sel);

#endif

#ifndef INCL_DOSMODULEMGR_INCLUDED
#define INCL_DOSMODULEMGR_INCLUDED

    USHORT APIENTRY DosFreeModule(HMODULE mhand);

    USHORT APIENTRY DosGetModHandle(PSZ namep, PHMODULE mhandp);

    USHORT APIENTRY DosGetModName(HMODULE mhand, USHORT buffc, PCHAR buffp);

    USHORT APIENTRY DosGetProcAddr(HMODULE mhand, PSZ pnamep,
                                   PPFN paddrp);

    USHORT APIENTRY DosLoadModule(PSZ failp, USHORT failc,
                                  PSZ modnamep, PHMODULE mhandp);

#ifdef __cplusplus
}
#endif

#endif
#endif