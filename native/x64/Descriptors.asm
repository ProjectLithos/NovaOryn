bits 64
default rel
section .text

global NovaOrynX64LoadGlobalDescriptorTable
global NovaOrynX64LoadTaskRegister

; Windows x64 ABI: RCX=base, DX=limit, R8W=code selector, R9W=data selector.
NovaOrynX64LoadGlobalDescriptorTable:
    sub rsp, 16
    mov [rsp], dx
    mov [rsp + 2], rcx
    lgdt [rsp]
    mov ax, r9w
    mov ds, ax
    mov es, ax
    mov ss, ax
    xor eax, eax
    mov fs, ax
    mov gs, ax
    lea rax, [rel .segments_reloaded]
    push r8
    push rax
    retfq
.segments_reloaded:
    add rsp, 16
    mov al, 1
    ret

; Windows x64 ABI: CX=selector.
NovaOrynX64LoadTaskRegister:
    mov ax, cx
    ltr ax
    mov al, 1
    ret
