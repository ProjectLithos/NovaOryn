bits 64
default rel

section .text

global NovaOrynUefiEntry
global NovaOrynCaptureUefiFramebuffer
global NovaOrynCaptureFinalUefiMemoryMap
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
; 38 UInt64 final UEFI memory-map address
; 40 UInt64 final UEFI memory-map byte length
; 48 UInt64 final UEFI map key accepted by ExitBootServices
; 50 UInt64 UEFI memory descriptor size
; 58 UInt32 UEFI memory descriptor version
; 5C UInt32 GetMemoryMap/ExitBootServices capture attempts
; 60 UInt64 final EFI_STATUS (zero on success)
; 68 UInt64 final-map flag (one only after ExitBootServices succeeds)
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
    dq NovaOrynFinalMemoryMapBuffer
    dq 0
    dq 0
    dq 0
    dd 0
    dd 0
    dq 0
    dq 0

; The final memory map must be captured into storage allocated before the last
; GetMemoryMap call. A fixed 512 KiB buffer provides descriptor-growth headroom
; without calling AllocatePool between GetMemoryMap and ExitBootServices.
section .bss align=4096
NovaOrynFinalMemoryMapBuffer:
    resb 524288
NovaOrynFinalMemoryMapBufferEnd:

section .text
NovaOrynUefiEntry:
    ; UEFI x64 enters with ImageHandle in RCX and EFI_SYSTEM_TABLE* in RDX.
    ; Preserve both values and maintain Windows x64 shadow space/alignment.
    push rbp
    mov rbp, rsp
    push r12
    push r13
    sub rsp, 32
    mov r12, rcx
    mov r13, rdx

    mov rcx, r13
    call NovaOrynCaptureUefiFramebuffer
    test al, al
    jz NovaOrynX64Halt

    ; This routine obtains the map whose key is passed immediately to
    ; ExitBootServices. A stale key causes a fresh GetMemoryMap retry.
    mov rcx, r12
    mov rdx, r13
    call NovaOrynCaptureFinalUefiMemoryMap
    test al, al
    jz NovaOrynX64Halt

    call NovaOrynRuntimeInitialize
    test al, al
    jz NovaOrynX64Halt

    cli
    lea rcx, [rel NovaOrynBootContext]
    call NovaOrynManagedEntry
    jmp NovaOrynX64Halt

NovaOrynCaptureFinalUefiMemoryMap:
    ; RCX = EFI_HANDLE ImageHandle, RDX = EFI_SYSTEM_TABLE*.
    push rbx
    push rsi
    push rdi
    push r12
    sub rsp, 40                 ; 32-byte shadow space plus fifth argument.

    mov rbx, rcx
    test rbx, rbx
    jz .final_failed_no_status
    test rdx, rdx
    jz .final_failed_no_status

    mov rsi, [rdx + 0x60]       ; EFI_SYSTEM_TABLE.BootServices
    test rsi, rsi
    jz .final_failed_no_status
    mov rdi, [rsi + 0x38]       ; EFI_BOOT_SERVICES.GetMemoryMap
    mov r12, [rsi + 0xE8]       ; EFI_BOOT_SERVICES.ExitBootServices
    test rdi, rdi
    jz .final_failed_no_status
    test r12, r12
    jz .final_failed_no_status

.retry_final_map:
    inc dword [rel NovaOrynBootContext + 0x5C]
    cmp dword [rel NovaOrynBootContext + 0x5C], 8
    ja .final_failed_no_status

    mov qword [rel NovaOrynBootContext + 0x40], NovaOrynFinalMemoryMapBufferEnd - NovaOrynFinalMemoryMapBuffer
    mov qword [rel NovaOrynBootContext + 0x48], 0
    mov qword [rel NovaOrynBootContext + 0x50], 0
    mov dword [rel NovaOrynBootContext + 0x58], 0

    lea rcx, [rel NovaOrynBootContext + 0x40]
    lea rdx, [rel NovaOrynFinalMemoryMapBuffer]
    lea r8, [rel NovaOrynBootContext + 0x48]
    lea r9, [rel NovaOrynBootContext + 0x50]
    lea rax, [rel NovaOrynBootContext + 0x58]
    mov [rsp + 32], rax
    call rdi
    test rax, rax
    jnz .final_failed_status

    cmp qword [rel NovaOrynBootContext + 0x40], 0
    je .final_failed_no_status
    cmp qword [rel NovaOrynBootContext + 0x40], NovaOrynFinalMemoryMapBufferEnd - NovaOrynFinalMemoryMapBuffer
    ja .final_failed_no_status
    cmp qword [rel NovaOrynBootContext + 0x50], 40
    jb .final_failed_no_status
    test qword [rel NovaOrynBootContext + 0x50], 7
    jnz .final_failed_no_status
    mov rax, [rel NovaOrynBootContext + 0x40]
    xor edx, edx
    div qword [rel NovaOrynBootContext + 0x50]
    test rdx, rdx
    jnz .final_failed_no_status
    test rax, rax
    jz .final_failed_no_status

    ; No allocation or firmware operation occurs between the successful
    ; GetMemoryMap above and this ExitBootServices call.
    mov rcx, rbx
    mov rdx, [rel NovaOrynBootContext + 0x48]
    call r12
    test rax, rax
    jz .final_succeeded

    mov [rel NovaOrynBootContext + 0x60], rax
    mov rdx, 0x8000000000000002 ; EFI_INVALID_PARAMETER: stale map key.
    cmp rax, rdx
    je .retry_final_map
    jmp .final_failed

.final_succeeded:
    mov qword [rel NovaOrynBootContext + 0x60], 0
    mov qword [rel NovaOrynBootContext + 0x68], 1
    mov al, 1
    jmp .final_return

.final_failed_status:
    mov [rel NovaOrynBootContext + 0x60], rax
    jmp .final_failed

.final_failed_no_status:
    mov qword [rel NovaOrynBootContext + 0x60], -1
.final_failed:
    xor eax, eax
.final_return:
    add rsp, 40
    pop r12
    pop rdi
    pop rsi
    pop rbx
    ret

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
