.section .text,"ax",@progbits
.global NovaOrynX64DisableInterrupts
.type NovaOrynX64DisableInterrupts,@function
NovaOrynX64DisableInterrupts:
    cli
    movb $1, %al
    ret

.global NovaOrynX64EnableInterrupts
.type NovaOrynX64EnableInterrupts,@function
NovaOrynX64EnableInterrupts:
    sti
    movb $1, %al
    ret

.global NovaOrynX64AreInterruptsEnabled
.type NovaOrynX64AreInterruptsEnabled,@function
NovaOrynX64AreInterruptsEnabled:
    pushfq
    popq %rax
    shrq $9, %rax
    andq $1, %rax
    ret

.global NovaOrynX64Halt
.type NovaOrynX64Halt,@function
NovaOrynX64Halt:
    cli
.LNovaOrynHaltForever:
    hlt
    jmp .LNovaOrynHaltForever
.size NovaOrynX64Halt, .-NovaOrynX64Halt

.global NovaOrynX64WritePort8
.type NovaOrynX64WritePort8,@function
NovaOrynX64WritePort8:
    movw %di, %dx
    movb %sil, %al
    outb %al, %dx
    movb $1, %al
    ret

.global NovaOrynX64ReadPort8
.type NovaOrynX64ReadPort8,@function
NovaOrynX64ReadPort8:
    movw %di, %dx
    inb %dx, %al
    movb %al, (%rsi)
    movb $1, %al
    ret
