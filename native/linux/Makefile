CC = gcc
ASM = nasm
ASMFLAGS = -f elf64
CFLAGS = -shared

TARGET = libcalc.so
SRC = library_linux.asm
OBJ = library_linux.o

.PHONY: all clean

all: $(TARGET)

$(TARGET): $(OBJ)
	$(CC) $(CFLAGS) $(OBJ) -o $(TARGET)

$(OBJ): $(SRC)
	$(ASM) $(ASMFLAGS) $(SRC) -o $(OBJ)

clean:
	rm -f $(OBJ) $(TARGET)