(c-import "Utilities.hpp")

(defun main (&return int)
  (return 0))

(add-c-search-directory-module "src" "notsrc")

(add-build-options "-Wall" "-Wextra" "-Wno-unused-parameter" "-O1")

(set-cakelisp-option executable-output "test/buildOptions")
