default rel

global calculate_book_priority:function

section .text

calculate_book_priority:
    imul rdi, 10
    shr rsi, 1
    add rdi, rsi
    sub rdi, rdx
    jnc .done
    xor rdi, rdi

.done:
    mov rax, rdi
    ret

section .note.GNU-stack noalloc noexec nowrite progbits
