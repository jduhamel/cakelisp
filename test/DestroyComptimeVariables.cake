(set-cakelisp-option cakelisp-src-dir "src")
(add-cakelisp-search-directory "runtime")
(import "ComptimeHelpers.cake" "CHelpers.cake")

(defun main (&return int)
  (return 0))

(defun-comptime build-test (manager (ref ModuleManager) module (addr Module) &return bool)
  (get-or-create-comptime-var (field manager environment) a (in std string) "empty")
  (Logf "The variable 'a' is %s\n" (call-on-ptr c_str a))
  (return true))

(defun-comptime set-build-vars (manager (ref ModuleManager) module (addr Module) &return bool)
  (get-or-create-comptime-var (field manager environment) a (in std string) "empty")
  (set (deref a) "This is a test")
  (return true))

(add-compile-time-hook-module pre-build build-test)
(add-compile-time-hook-module pre-build set-build-vars :priority-increase 1)
