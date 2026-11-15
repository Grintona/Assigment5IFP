default rel

global calculate_book_priority
export calculate_book_priority

section .text

calculate_book_priority:
    imul rcx, 10
    shr rdx, 1
    add rcx, rdx
    sub rcx, r8
    jnc .done
    xor rcx, rcx

.done:
    mov rax, rcx
    ret
