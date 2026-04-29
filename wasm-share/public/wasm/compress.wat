;; compress.wat
;; Applies XOR obfuscation to a block of memory.
;; In a real project replace this with a proper compression/encryption algorithm.
;;
;; Exported functions:
;;   xor_buffer(offset i32, length i32, key i32) -> void
;;     XORs every byte in memory[offset .. offset+length] with key (low 8 bits).

(module
  (memory (export "mem") 16)   ;; 16 pages = 1 MB shared linear memory

  (func $xor_buffer (export "xor_buffer")
    (param $offset i32)
    (param $length i32)
    (param $key    i32)
    (local $i i32)
    (local $byte i32)

    (local.set $i (i32.const 0))

    (block $break
      (loop $loop
        ;; if i >= length → break
        (br_if $break (i32.ge_u (local.get $i) (local.get $length)))

        ;; byte = mem[offset + i]
        (local.set $byte
          (i32.load8_u
            (i32.add (local.get $offset) (local.get $i))
          )
        )

        ;; mem[offset + i] = byte XOR key
        (i32.store8
          (i32.add (local.get $offset) (local.get $i))
          (i32.xor (local.get $byte) (local.get $key))
        )

        (local.set $i (i32.add (local.get $i) (i32.const 1)))
        (br $loop)
      )
    )
  )
)
