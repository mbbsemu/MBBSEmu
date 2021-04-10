#include <stdio.h>
#include <dos.h>
#include <stdlib.h>
#include <string.h>

#define BTRIEVE_INTERRUPT 0x7b
#define INTERFACE_ID 0x6176
#define MAXKEYS 54
#define MAX_PATH 82

typedef struct _tagBtrieveData {
  char far *data_buffer;
  unsigned int data_buffer_length;

  char far *position_block;
  char far *fcb;

  unsigned int operation;

  char far *key_buffer;
  unsigned char key_buffer_length;
  
  unsigned char key_number;

  unsigned int far *status;

  unsigned int interface_id;
} BTRIEVEDATA;

#if sizeof(BTRIEVEDATA) != 28
  #error "BtrieveData struct not 28 bytes"
#endif

void btrieveInterrupt(BTRIEVEDATA far *data) {
  // to make the compiler happy since it doesn't
  // think data is referenced.
  // NARRATOR: It is, by [bp + 4/6]
  data = data; 
  __asm {
    push ds
    push dx

    mov ds, word ptr [bp + 6]
    mov dx, word ptr [bp + 4]
    int BTRIEVE_INTERRUPT

    pop dx
    pop ds
  }
}

int isBtrieveLoaded(void) {
  union REGS regs; 
  
  regs.x.ax = 0x3500 | BTRIEVE_INTERRUPT;
  int86(0x21, &regs, &regs); 
  return regs.x.bx == 0x33;
}

#define OPEN_MODE_NORMAL 0
#define OPEN_MODE_READ_ONLY -2
#define OPEN_MODE_VERIFY_WRITE_OPERATIONS -3
#define OPEN_MODE_EXCLUSIVE -4

typedef struct _tagBTRIEVEFILE {
  char posBlock[128];
} BTRIEVEFILE;

int openBtrieve(const char *filepath, int openMode, BTRIEVEFILE *file) {
  BTRIEVEDATA data;
  unsigned int status = 0xFFFF;

  memset(&data, 0, sizeof(data));
  memset(file->posBlock, 0, 128);

  data.position_block = file->posBlock;
  data.operation = 0;
  data.status = &status;
  data.interface_id = INTERFACE_ID;
  
  data.data_buffer_length = strlen(filepath) + 1;
  data.key_buffer = (char far*)filepath;
  data.key_buffer_length = strlen(filepath) + 1;
  data.key_number = openMode;
  
  btrieveInterrupt(&data);

  return status;
}

int closeBtrieve(BTRIEVEFILE *file) {
  BTRIEVEDATA data;
  unsigned int status = 0xFFFF;

  memset(&data, 0, sizeof(data));

  data.position_block = file->posBlock;
  data.operation = 1;
  data.status = &status;
  data.interface_id = INTERFACE_ID;
  
  btrieveInterrupt(&data);

  return status;
}

typedef struct _tagFILESPEC {
  int record_length;
  int page_size;
  int number_of_keys;
  long number_of_records;
  int flags;
  int reserved; // actually (byte) duplicate_pointers | (byte) unused
  int unused_pages;
} FILESPEC;

typedef struct _tagKEYSPEC {
  int position;
  int length;
  int flags;
  long number_of_keys;
  unsigned char data_type;
  unsigned char null;
  int unused;
  unsigned char number_only_if_explicit_key_flag_is_set;
  unsigned char acs_number;
} KEYSPEC;

typedef struct _tagSTATBUF {
  char file_name_if_multifile[MAX_PATH];
  FILESPEC filespec;
  KEYSPEC keys[MAXKEYS];
  // in CREATE call, the ACS follows this, but not in STAT
} STATBUF;

void logStatBuf(STATBUF *buf) {
  int i;
  
  printf(
    "record_length:     %d\n"
    "page_size:         %d\n"
    "number_of_keys:    %d\n"
    "number_of_records: %ld\n"
    "flags:             0x%x\n\n",
    buf->filespec.record_length,
    buf->filespec.page_size,
    buf->filespec.number_of_keys,
    buf->filespec.number_of_records,
    buf->filespec.flags);

  for (i = 0; i < buf->filespec.number_of_keys; ++i) {
    printf(
      "key%d_position:  %d\n"
      "key%d_length:    %d\n"
      "key%d_flags:     0x%x\n"
      "key%d_data_type: %d\n",
      i, buf->keys[i].position,
      i, buf->keys[i].length,
      i, buf->keys[i].flags,
      i, buf->keys[i].data_type);
  }
}

int statBtrieve(BTRIEVEFILE *file, STATBUF *statBuf) {
  BTRIEVEDATA data;
  unsigned int status = 0xFFFF;
  
  memset(&data, 0, sizeof(data));
  memset(statBuf, 0, sizeof(statBuf));

  data.position_block = file->posBlock;
  data.operation = 15;
  data.status = &status;
  data.interface_id = INTERFACE_ID;
  
  data.data_buffer = (char far*)&statBuf->filespec;
  data.data_buffer_length = sizeof(STATBUF) - MAX_PATH;
  data.key_buffer = statBuf->file_name_if_multifile;
  data.key_buffer_length = MAX_PATH;

  btrieveInterrupt(&data);

  return status;
}

void logError(const char *fileName, int errorCode) {
  fprintf(stderr, "Error from BTRIEVE (%s): %d\n", fileName, errorCode);
}

int dumpBtrieve(const char *fileName) {
  int r;
  BTRIEVEFILE file;
  STATBUF statBuf;
  
  r = openBtrieve(fileName, OPEN_MODE_NORMAL, &file); 
  if (r) {
    logError(fileName, r);
    return r;
  }

  printf("Successfully opened %s!\n", fileName);

  r = statBtrieve(&file, &statBuf);
  if (r) {
    logError(fileName, r);
  } else {
    logStatBuf(&statBuf);
  }

  r = closeBtrieve(&file);
  if (r) {
    logError(fileName, r);
  }

  return r;
}

int main(int argc, char **argv) {
  int i;
  
  if (!isBtrieveLoaded()) {
    fprintf(stderr, "Run BTRIEVE /P:2048 first before running this program\n");
    return 1;
  }

  for (i = 1; i < argc; ++i) {
    dumpBtrieve(argv[i]);
  }

  return 0;
}
