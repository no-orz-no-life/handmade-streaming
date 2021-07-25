module Posix 
    open System.Runtime.InteropServices
    [<Literal>]
    let LibRt = "librt.so.1"
    [<Literal>]
    let LibC = "libc.so.6"
    [<Literal>]
    let LibPthread = "libpthread.so.0"

    // fsharplint:disable TypePrefixing
    type size_t = int64
    type off_t = int64

    [<DllImport(LibRt)>]
    extern int shm_open(string name, int oflag, int mode)

    [<DllImport(LibRt)>]
    extern int shm_unlink(string name)

    [<DllImport(LibC)>]
    extern nativeint mmap(nativeint addr, size_t length, int prot, int flags, int fd, off_t offset)

    [<DllImport(LibC)>]
    extern int munmap(nativeint addr, size_t length)

    [<DllImport(LibC)>]
    extern int close(int fd)

    [<Struct>]
    [<StructLayout(LayoutKind.Sequential)>]
    type timespec =
        val mutable tv_sec: int64
        val mutable tv_nsec: int64

    [<Struct>]
    [<StructLayout(LayoutKind.Sequential)>]
    type sem_t =
        [<MarshalAs(UnmanagedType.ByValArray, SizeConst=32)>] val mutable opaque: byte array

    [<DllImport(LibPthread)>]
    extern int sem_destroy(nativeint sem)

    [<DllImport(LibPthread)>]
    extern int sem_init(nativeint sem, int pshared, uint value)

    [<DllImport(LibPthread)>]
    extern int sem_post(nativeint sem)

    [<DllImport(LibPthread)>]
    extern int sem_wait(nativeint sem)

    [<DllImport(LibPthread)>]
    extern int sem_trywait(nativeint sem)

    [<DllImport(LibPthread)>]
    extern int sem_timedwait(nativeint  sem, timespec& abs_timeout)

    [<DllImport(LibC)>]
    extern void perror(string s)

    type mqd_t = int
    type ssize_t = int64

    [<DllImport(LibRt)>]
    extern mqd_t mq_open(string name, int oflag)

    [<DllImport(LibRt)>]
    extern ssize_t mq_receive(mqd_t mqdes, nativeint msg_ptr, size_t msg_len, nativeint msg_prio)

    [<DllImport(LibRt)>]
    extern ssize_t mq_timedreceive(mqd_t mqdes, nativeint msg_ptr, size_t msg_len, nativeint msg_prio, timespec& abs_timeout)

    [<DllImport(LibRt)>]
    extern int mq_close(mqd_t mqdes)

    [<DllImport(LibC)>]
    extern int timespec_get (timespec& ts, int baseTz)

    let O_RDWR = 2
    let O_CREAT = 0o100
    let PROT_READ = 1
    let PROT_WRITE = 2
    let MAP_SHARED = 1

    let TIME_UTC = 1

    // fsharplint:enable