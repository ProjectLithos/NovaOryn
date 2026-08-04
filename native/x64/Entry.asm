bits 64
default rel

section .text

global NovaOrynUefiEntry
global NovaOrynCaptureUefiFramebuffer
extern NovaOrynRuntimeInitialize
extern NovaOrynManagedEntry
extern NovaOrynX64Halt

; EFI_GRAPHICS_OUTPUT_PROTOCOL_GUID
section .rdata align=16
NovaOrynGraphicsOutputProtocolGuid:
    db 0xDE, 0xA9, 0x42, 0x90, 0xDC, 0x23, 0x38, 0x4A
    db 0x96, 0xFB, 0x7A, 0xDE, 0xD0, 0x80, 0x51, 0x6A

; Native boot context consumed by the managed no-CoreLib bootstrap.
; 00 UInt64 signature (ASCII "NOVAORYN")
; 08 UInt64 framebuffer address
; 10 UInt64 framebuffer size
; 18 UInt32 width
; 1C UInt32 height
; 20 UInt32 pixels per scan line
; 24 UInt32 UEFI pixel format
; 28 UInt32 red mask
; 2C UInt32 green mask
; 30 UInt32 blue mask
; 34 UInt32 reserved mask
section .data align=16
NovaOrynBootContext:
    dq 0x4E59524F41564F4E
    dq 0
    dq 0
    dd 0
    dd 0
    dd 0
    dd 0
    dd 0
    dd 0
    dd 0
    dd 0

section .text
NovaOrynUefiEntry:
    ; UEFI x64 enters with ImageHandle in RCX and EFI_SYSTEM_TABLE* in RDX.
    ; Preserve ABI stack alignment and the mandatory 32-byte shadow space.
    push rbp
    mov rbp, rsp
    sub rsp, 32

    mov rcx, rdx
    call NovaOrynCaptureUefiFramebuffer
    test al, al
    jz NovaOrynX64Halt

    call NovaOrynRuntimeInitialize
    test al, al
    jz NovaOrynX64Halt

    ; Firmware services are no longer called after this point.
    cli
    lea rcx, [rel NovaOrynBootContext]
    call NovaOrynManagedEntry
    jmp NovaOrynX64Halt

NovaOrynCaptureUefiFramebuffer:
    ; RCX = EFI_SYSTEM_TABLE*. Preserve RBX and allocate shadow space plus
    ; one local qword used as the LocateProtocol output slot.
    push rbx
    sub rsp, 48
    mov qword [rsp + 32], 0

    test rcx, rcx
    jz .failed

    ; EFI_SYSTEM_TABLE.BootServices is at offset 0x60 on x64.
    mov rax, [rcx + 0x60]
    test rax, rax
    jz .failed

    ; EFI_BOOT_SERVICES.LocateProtocol is at offset 0x140.
    mov rax, [rax + 0x140]
    test rax, rax
    jz .failed

    lea rcx, [rel NovaOrynGraphicsOutputProtocolGuid]
    xor edx, edx
    lea r8, [rsp + 32]
    call rax
    test rax, rax
    jnz .failed

    mov rbx, [rsp + 32]
    test rbx, rbx
    jz .failed

    ; EFI_GRAPHICS_OUTPUT_PROTOCOL.Mode is at offset 0x18.
    mov rbx, [rbx + 0x18]
    test rbx, rbx
    jz .failed

    ; EFI_GRAPHICS_OUTPUT_PROTOCOL_MODE.Info is at 0x08.
    mov rdx, [rbx + 0x08]
    test rdx, rdx
    jz .failed

    ; FrameBufferBase and FrameBufferSize are at 0x18 and 0x20.
    mov rax, [rbx + 0x18]
    mov [rel NovaOrynBootContext + 0x08], rax
    mov rax, [rbx + 0x20]
    mov [rel NovaOrynBootContext + 0x10], rax

    ; EFI_GRAPHICS_OUTPUT_MODE_INFORMATION fields.
    mov eax, [rdx + 0x04]
    mov [rel NovaOrynBootContext + 0x18], eax
    mov eax, [rdx + 0x08]
    mov [rel NovaOrynBootContext + 0x1C], eax
    mov eax, [rdx + 0x20]
    mov [rel NovaOrynBootContext + 0x20], eax
    mov eax, [rdx + 0x0C]
    mov [rel NovaOrynBootContext + 0x24], eax
    mov eax, [rdx + 0x10]
    mov [rel NovaOrynBootContext + 0x28], eax
    mov eax, [rdx + 0x14]
    mov [rel NovaOrynBootContext + 0x2C], eax
    mov eax, [rdx + 0x18]
    mov [rel NovaOrynBootContext + 0x30], eax
    mov eax, [rdx + 0x1C]
    mov [rel NovaOrynBootContext + 0x34], eax

    mov al, 1
    add rsp, 48
    pop rbx
    ret

.failed:
    xor eax, eax
    add rsp, 48
    pop rbx
    ret
