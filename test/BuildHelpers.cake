(add-cakelisp-search-directory "runtime")
(import &defs-only "NoHeader.cake" &comptime-only "BuildTools.cake")

(c-import "<stdio.h>")

(defun-nodecl my-test ()
  (fprintf stderr "Does nothing\n"))

(defun main (&return int)
  (fprintf stderr "Hello, build tools!\n")
  (return 0))

(defun-comptime run-3rd-party-build (manager (ref ModuleManager) module (addr Module) &return bool)
  (var cakelisp-executable (addr (const char)) "bin/cakelisp")
  (comptime-cond ('Windows (set cakelisp-executable "bin/cakelisp.exe")))
  ;; Sequential
  (run-process-sequential-or (cakelisp-executable "--list-built-ins")
                             (Log "failed to run process\n")
                             (return false))
  ;; Parallel
  (var cakelisp-status int -1)
  (run-process-start-or (addr cakelisp-status) (cakelisp-executable "--list-built-ins")
                        (return false))
  (var cakelisp-2-status int -1)
  (run-process-start-or (addr cakelisp-2-status) (cakelisp-executable "--list-built-ins")
                        (return false))
  (waitForAllProcessesClosed null)
  (return (and (= cakelisp-status 0) (= cakelisp-2-status 0))))

(add-compile-time-hook-module pre-build run-3rd-party-build)

(set-cakelisp-option executable-output "test/BuildHelpers")
