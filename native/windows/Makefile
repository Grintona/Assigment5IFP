CC = x86_64-w64-mingw32-gcc
ASM = nasm
ASMFLAGS = -f win64
CFLAGS = -shared

TARGET = libcalc.dll
SRC = library_windows.asm
OBJ = library_windows.obj

.PHONY: all clean

all: $(TARGET)

$(TARGET): $(OBJ)
	$(CC) $(CFLAGS) $(OBJ) -o $(TARGET)

$(OBJ): $(SRC)
	$(ASM) $(ASMFLAGS) $(SRC) -o $(OBJ)

clean:
	rm -f $(OBJ) $(TARGET)