default rel

global calculate_book_priority
export calculate_book_priority

section .text

; uint64_t calculate_book_priority(uint64_t edition_count, uint64_t ratings_count, uint64_t book_age)
calculate_book_priority:
    ; rcx = edition_count
    ; rdx = ratings_count
    ; r8  = book_age

    ; 1. rcx = edition_count * 10
    imul rcx, 10

    ; 2. rdx = ratings_count / 2
    shr rdx, 1

    ; 3. rcx = (edition_count * 10) + (ratings_count / 2)
    add rcx, rdx

    ; 4. rcx = rcx - book_age (r8)
    sub rcx, r8


    cmp rcx, 0
    jge .done
    xor rcx, rcx

.done:
    mov rax, rcx
    ret