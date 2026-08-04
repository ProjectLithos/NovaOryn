bits 64
default rel
section .text

global NovaOrynUefiEntry
extern NovaOrynRuntimeInitialize
extern NovaOrynManagedEntry
extern NovaOrynX64Halt

NovaOrynUefiEntry:
    cli
    and rsp, -16
    sub rsp, 32
    call NovaOrynRuntimeInitialize
    test al, al
    jz NovaOrynX64Halt
    xor rcx, rcx
    call NovaOrynManagedEntry
    jmp NovaOrynX64Halt
