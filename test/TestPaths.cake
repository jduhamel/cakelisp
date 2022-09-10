(c-import "<stdio.h>"
        ;; realpath
        "<stdlib.h>")
(add-cakelisp-search-directory "runtime")
(import "ComptimeHelpers.cake")

(defun main (&return int)
  (var path-tests (array (addr (const char))) (array
                                               "."
                                               "./runtime/TestPaths.cake"
                                               "/home/macoy/Development/code/repositories/cakelisp"
                                               "../cakelisp/runtime/TestPaths.cake"
                                               "runtime/ComptimeHelpers.cake"
                                               "src/ComptimeHelpers.cake"))
  (var i int 0)
  (while (< i (array-size path-tests))
    (var result (addr (const char)) (realpath (at i path-tests) null))
    (unless result
      (perror "realpath: ")
      (incr i)
      (continue))
    (fprintf stderr "%s = %s\n" (at i path-tests) result)
    (free (type-cast result (addr void)))
    (incr i))
  (return 0))
