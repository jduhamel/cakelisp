(import "runtime/ComptimeHelpers.cake")
(c-import "<stdio.h>" "<stdbool.h>")

(defun secret-print ()
  (if false
      (return))
  (fprintf stderr "I got away\n"))

(defun main (&return int)
  (fprintf stderr "Hello Code modification!\n")

  (simple-macro)
  (secret-print)

  (return 0))

;;
;; Code modification tests
;;

(defun-comptime compile-time-call-before (&return int)
  (return (/ (- 4 2) 2)))

(defmacro simple-macro ()
  (fprintf stderr "simple-macro: %d, %d!\n" (compile-time-call) (compile-time-call-before))
  (tokenize-push output (fprintf stderr "Hello, macros!\n") (magic-number))
  (return true))

(defun-comptime compile-time-call (&return int)
  (return 42))

;; Uncomment to test special case bad build (and reference it somewhere)
;; (defun-comptime bad-compile-time-call ()
;;   ;; (reference) ;; Comment and this function will build every round
;;   (return 42))

(defmacro magic-number ()
  (get-or-create-comptime-var test-var std::string)
  (get-or-create-comptime-var test-crazy-var (addr (const (addr (template (in std vector) int)))))
  (set (deref test-var) "Yeah")
  (tokenize-push output (fprintf stderr "The magic number is 42\n"))
  (return true))

(defun-comptime sabotage-main-printfs (environment (ref EvaluatorEnvironment)
                                       &return bool)
  (get-or-create-comptime-var test-var std::string)
  (fprintf stderr "%s is the message\n" (call-on-ptr c_str test-var))
  (var old-definition-tags (template (in std vector) std::string))
  ;; Scope to ensure that definition-it and definition are not referred to after
  ;; ReplaceAndEvaluateDefinition is called, because they will be invalid
  (scope
   (var definition-it (in ObjectDefinitionMap iterator)
        (call-on find (field environment definitions) "main"))
   (when (= definition-it (call-on end (field environment definitions)))
     (fprintf stderr "sabotage-main-printfs: could not find main!\n")
     (return false))

   (fprintf stderr "sabotage-main-printfs: found main\n")
   (var definition (ref ObjectDefinition) (path definition-it > second))
   (when (!= (FindInContainer (field definition tags) "sabotage-main-printfs-done")
             (call-on end (field definition tags)))
     (fprintf stderr "sabotage-main-printfs: already modified\n")
     (return true))

   ;; Other modification functions should do this lazily, i.e. only create the expanded definition
   ;; if a modification is necessary
   ;; This must be allocated on the heap because it will be evaluated and needs to stick around
   (var modified-main-tokens (addr (template (in std vector) Token)) (new (template (in std vector) Token)))
   (unless (CreateDefinitionCopyMacroExpanded definition (deref modified-main-tokens))
     (delete modified-main-tokens)
     (return false))

   ;; Environment will handle freeing tokens for us
   (call-on push_back (field environment comptimeTokens) modified-main-tokens)

   ;; Before
   (prettyPrintTokens (deref modified-main-tokens))

   (var prev-token (addr Token) null)
   (for-in token (ref Token) (deref modified-main-tokens)
           (when (and prev-token
                      (= 0 (call-on compare (path prev-token > contents) "stderr"))
                      (ExpectTokenType "sabotage-main-printfs" token TokenType_String))
             (set (field token contents) "I changed your print! Mwahahaha!\\n"))
           (set prev-token (addr token)))

   (fprintf stderr "sabotage-main-printfs: modified main!\n")
   ;; After
   (prettyPrintTokens (deref modified-main-tokens))

   ;; Copy tags for new definition
   (PushBackAll old-definition-tags (field definition tags))

   ;; Definition references invalid after this!
   (unless (ReplaceAndEvaluateDefinition environment "main" (deref modified-main-tokens))
     (return false)))

  ;; Find the new (replacement) definition and add a tag saying it is done replacement
  ;; Note that I also push the tags of the old definition
  (var definition-it (in ObjectDefinitionMap iterator)
        (call-on find (field environment definitions) "main"))
  (when (= definition-it (call-on end (field environment definitions)))
    (fprintf stderr "sabotage-main-printfs: could not find main after replacement!\n")
    (return false))
  (PushBackAll (path definitionIt > second . tags) old-definition-tags)
  (call-on push_back (path definitionIt > second . tags) "sabotage-main-printfs-done")
  (return true))

(add-compile-time-hook post-references-resolved sabotage-main-printfs)

;; ;; TODO: If this calls a function which needs var, that's a circular dependency
;; ;; Silly example, but shows user can replace built-in with a custom macro
;; ;; Would this be better as a code-modification thing?
;; (rename-builtin "var" "badvar")
;; (defmacro var ()
;;   ;; Var cannot be used within var, because it's undefined. This excludes a lot of macros
;;   ;; (get-or-create-comptime-var var-replacements (template (in std vector) (addr (const Token))))
;;   ;; (for-in replaced-token (addr (const Token)) (addr var-replacements)
;;           ;; (NoteAtToken (deref replaced-token) "Replaced already"))

;;   (PushBackTokenExpression output (addr (at startTokenIndex tokens)))
;;   ;; TODO: This is no good, because var looks at its invocation name
;;   (set (field (at 1 output) contents) "badvar")
;;   (return true))

(set-cakelisp-option executable-output "test/CodeModification")
