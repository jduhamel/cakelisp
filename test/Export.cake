(export
 (add-cakelisp-search-directory "runtime")
 (import "CHelpers.cake"))

(export
 (declare-extern-function test ())
 (add-c-build-dependency "dir/Test.c"))

(set-cakelisp-option executable-output "test/Export")
