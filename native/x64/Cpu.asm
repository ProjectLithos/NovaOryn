bits 64
default rel
section .text

global NovaOrynX64DisableInterrupts
global NovaOrynX64EnableInterrupts
global NovaOrynX64AreInterruptsEnabled
global NovaOrynX64Halt
global NovaOrynX64WritePort8
global NovaOrynX64ReadPort8

NovaOrynX64DisableInterrupts:
    cli
    mov al, 1
    ret
NovaOrynX64EnableInterrupts:
    sti
    mov al, 1
    ret
NovaOrynX64AreInterruptsEnabled:
    pushfq
    pop rax
    shr rax, 9
    and rax, 1
    ret
NovaOrynX64Halt:
    cli
.halt_forever:
    hlt
    jmp .halt_forever
NovaOrynX64WritePort8:
    mov r8b, dl
    mov dx, cx
    mov al, r8b
    out dx, al
    mov al, 1
    ret
NovaOrynX64ReadPort8:
    mov r8, rdx
    mov dx, cx
    in al, dx
    mov [r8], al
    mov al, 1
    ret
