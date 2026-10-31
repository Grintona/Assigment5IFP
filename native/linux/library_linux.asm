default rel

global calculate_book_priority:function

section .text

; uint64_t calculate_book_priority(uint64_t edition_count, uint64_t ratings_count, uint64_t book_age)
calculate_book_priority:
    ; rdi = edition_count
    ; rsi = ratings_count
    ; rdx = book_age

    ; 1. rdi = edition_count * 10
    imul rdi, 10

    ; 2. rsi = ratings_count / 2 (bit shift to 1)
    shr rsi, 1

    ; 3. rdi = (edition_count * 10) + (ratings_count / 2)
    add rdi, rsi

    ; 4. rdi = rdi - book_age
    sub rdi, rdx

    ; 5. if rdi < 0, return 0
    cmp rdi, 0
    jge .done
    xor rdi, rdi                

.done:
    mov rax, rdi                ; return to rax
    ret