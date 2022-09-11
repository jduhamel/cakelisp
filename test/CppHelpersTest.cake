(add-cakelisp-search-directory "runtime")
(import "CHelpers.cake" "FileUtilities.cake")

(defenum my-enum
  my-enum-a
  my-enum-b
  my-enum-c)

(defenum-local my-enum-local
  my-enum-local-a
  my-enum-local-b
  my-enum-local-c)

(defclass my-class
    ;; my-type int
  (defun my-member-func (&return bool)
    (return true)))

(defun main (&return int)
  (var state my-enum my-enum-a)

  ;; Add test some regular C helpers while we are at it
  (defstruct nested
    a int
    b int)
  (defstruct my-struct
    a int
    b int
    c nested)
  (var thing my-struct (array 0))
  (set-fields thing
    a 42
    b 64
    (c a) 92
    (c b) 72)

  (if-open-file-scoped "fail.txt" "rb" fail-file
    (scope
     (fprintf stderr "error: I didn't expect to succeed, but I did\n")
     (return 1))
    (fprintf stderr "I expect to fail, and I did\n"))
  (var filename (addr (const char)) "CppHelpersTest.cake")
  (if-open-file-scoped filename "rb" succeed-file
    (fprintf stderr "successfully opened %s\n" filename)
    (scope
     (fprintf stderr "error: Failed to open %s\n" filename)
     (return 1)))
  (return 0))

(set-cakelisp-option executable-output "test/CppHelpers")
