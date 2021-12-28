;; Use Cakelisp's RunProcess code during your project's runtime
;; Note: this code is GPLv3 because RunProcess is in src/, not runtime/
(import "BuildTools.cake" ;; For run-process macros
        "CHelpers.cake")

(export-and-evaluate
 (add-cakelisp-src-as-search-dir)
 (c-import "RunProcess.hpp" "FileUtilities.hpp" "Build.hpp" "Utilities.hpp")
 (import "BuildTools.cake"))

(add-c-build-dependency "RunProcess.cpp" "Utilities.cpp" "Logging.cpp")

(comptime-cond
 ('Unix
  (add-build-options "-DUNIX"))
 ('Windows
  (add-build-options "/DWINDOWS")))

(forward-declare (struct RunProcessArguments))

(defun run-process-wait-for-completion (run-arguments (* RunProcessArguments)
                                        &return int)
  (run-process-wait-for-completion-body))

;; TODO: Kill once comptime can also be runtime
(defmacro runtime-run-process-sequential-or (command array &rest on-failure array)
  (tokenize-push output
    (scope
     (run-process-make-arguments
      process-command
      ;; Don't use cakelisp resolve because we don't want to have to bundle Cakelisp's FindVS
      'no-resolve
      ;; +1 because we want the inside of the command
      (token-splice-rest (+ 1 command) tokens))
     (unless (= 0 (run-process-wait-for-completion (addr process-command)))
       (token-splice-rest on-failure tokens))))
  (return true))

;; We do this to avoid having the search directory on every single compile command. Not ideal.
(defmacro add-cakelisp-src-as-search-dir ()
  (when (call-on empty (field environment cakelispSrcDir))
    (ErrorAtToken (at startTokenIndex tokens) "expected cakelisp-src-dir to be defined before this import")
    (return false))
  (call-on push_back (path context . module > cSearchDirectories) (field environment cakelispSrcDir))
  (return true))