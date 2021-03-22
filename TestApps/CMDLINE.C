#include <stdio.h>
#include <dos.h>

void printInfo(void);
void printPSP(char __far *psp);

unsigned cs(void) {
  __asm {
    mov ax,cs
  }
}

unsigned es(void) {
  __asm {
    mov ax,es
  }
}

unsigned ds(void) {
  __asm {
    mov ax,ds
  }
}

int main(int argc, char **argv, char *envp[]) {
  int i;
  char *s;

  printInfo();

  printf("\nPrinting %d cmdline args from %p\n", argc, argv);
  
  for (i = 0; i < argc; ++i) {
    printf("%p:%s\n", argv[i], argv[i]);
  }

  printf("\nPrinting environment variables from %p\n", envp);
  for (i = 0; envp[i] != NULL; ++i) {
    printf("%p:%s\n", envp[i], envp[i]);
  }

  return 0;
}

void printInfo(void) {
  char __far *psp = MK_FP(getpsp(), 0);
  char __far *dta = getdta();

  printf(
    "ds: %x\n"
    "es: %x\n"
    "cs: %x\n"
    "main: %Fp\n" 
    "_environ: %p\n"
    "psp: %Fp\n" 
    "dta: %Fp\n",
    ds(), es(), cs(), MK_FP(cs(),main), _environ, psp, dta);

  printPSP(psp);
}

typedef struct _tagPSP {
  unsigned cpm_exit;          // 0
  unsigned first_byte_beyond; // 2
  char fill1[40]; // don't care
  unsigned env_segment; // 2 0x2c
  char fill2[82]; // 
  unsigned char cmdTailLength;
  char *cmdTail[127];
} PSP;

char __far *copy(char *buf, char __far *farsrc) {
  char __far *s;
  for (s = farsrc; *s; ++s) {
    *buf++ = *s;
  }
  *buf = 0;
  return s + 1;
}

void printPSP(char __far *psp_ptr) {
  int i = 0;
  char buf[128];
  PSP __far *psp = (PSP __far*)psp_ptr;
  char __far *env = MK_FP(psp->env_segment, 0);

  printf("\n");
  printf("PSP: first byte beyond: %Fp\n", MK_FP(psp->first_byte_beyond, 0));
  printf("PSP: env_segment: %Fp\n", env);
  printf("PSP: cmdTailLength: %d\n", (int)psp->cmdTailLength);
  
  while (*env) {
    env = copy(buf, env);
    printf("PSP: env[%d]:%s\n", i++, buf);
  }
  printf("PSP: env[0]:%d env[1]:%d env[2]:%d env[3]:%d env[4]:%d env[5]:%d\n",
    env[0], env[1], env[2], env[3], env[4], env[5]);
}
