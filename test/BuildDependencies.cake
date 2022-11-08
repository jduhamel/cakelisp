(add-cakelisp-search-directory "runtime")
(import "Duplicate.cake" "CHelpers.cake"
        "CPreprocessorDefine.cake")

(c-import "<stdio.h>")

(declare-extern-function test ())

(defun main (&return int)
  (test)
  (fprintf stderr "%d\n" (my-fun))
  (return 0))

(add-c-build-dependency "Test.c")

(add-c-search-directory-module "test/dir")

(set-cakelisp-option executable-output "test/BuildDependencies")
