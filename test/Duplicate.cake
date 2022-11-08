(add-c-build-dependency "Test.c")

(defun my-fun (&return int)
  (return 0))

(add-c-search-directory-module "test/dir2")
