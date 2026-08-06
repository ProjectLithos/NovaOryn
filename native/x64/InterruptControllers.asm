bits 64
default rel
section .text

global NovaOrynX64ReadPort8
global NovaOrynX64WritePort8
global NovaOrynX64ReadMsr
global NovaOrynX64WriteMsr
global NovaOrynX64ReadMmio32
global NovaOrynX64WriteMmio32

NovaOrynX64ReadPort8:
    mov dx, cx
    xor eax, eax
    in al, dx
    ret
NovaOrynX64WritePort8:
    mov eax, edx
    mov dx, cx
    out dx, al
    mov eax, 1
    ret
NovaOrynX64ReadMsr:
    mov ecx, ecx
    rdmsr
    shl rdx, 32
    or rax, rdx
    ret
NovaOrynX64WriteMsr:
    mov r8, rdx
    mov eax, r8d
    shr r8, 32
    mov edx, r8d
    wrmsr
    mov eax, 1
    ret
NovaOrynX64ReadMmio32:
    mov eax, [rcx]
    ret
NovaOrynX64WriteMmio32:
    mov [rcx], edx
    mfence
    mov eax, 1
    ret
