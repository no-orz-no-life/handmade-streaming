CFLAGS=-I/home/zinnia/build/frei0r/include

all: memorymap.so

memorymap.so: memorymap.o
	$(CC) -fPIC -shared -o memorymap.so memorymap.o -lrt -lpthread -luuid

.c.o:
	$(CC) -c $(CFLAGS) $<