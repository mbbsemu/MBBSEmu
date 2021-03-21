#include <stdio.h>

int main(int argc, char **argv, char *envp[]) {
  int i;
  char *s;

  printf("Printing %d cmdline args\n", argc);
  
  for (i = 0; i < argc; ++i) {
    printf("%s\n", argv[i]);
  }

  printf("Printing environment variables\n");
  for (i = 0; envp[i] != NULL; ++i) {
    printf("%s\n", envp[i]);
  }

  return 0;
}
