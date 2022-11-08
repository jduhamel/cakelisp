(c-import "EvaluatorEnums.hpp")

(defun main (&return int)
  (return 0))

(add-c-search-directory-module "src" "notsrc")

(comptime-cond
 ('Unix
  (add-build-options "-Wall" "-Wextra" "-Wno-unused-parameter" "-O0")))

(set-cakelisp-option executable-output "test/buildOptions")
