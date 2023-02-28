(import "ComptimeHelpers.cake")

;; Unlike scope, this does not create a scope, which is useful when you don't want a scope but do
;; want multiple statements
;; Like Lisp's progn but without a name that doesn't make sense in C
(defmacro body (&rest statements any)
  (tokenize-push output
    (token-splice-rest statements tokens))
  (return true))

(defmacro array-size (array-token any)
  (tokenize-push output (/ (sizeof (token-splice array-token))
                           (sizeof (at 0 (token-splice array-token)))))
  (return true))

;; Necessary to create e.g. in C PREFIX "_my_thing"
(defgenerator static-string-combine (string-A (arg-index any) string-B (arg-index any))
  (var statement (const (array CStatementOperation))
    (array
     (array Expression null string-A)
     (array Keyword " " -1)
     (array Expression null string-B)))
  (return (c-statement-out statement)))

;; e.g. (negate 1) outputs (-1)
(defgenerator negate (to-negate (arg-index any))
  (var negate-statement (const (array CStatementOperation))
    (array
     (array OpenParen null -1)
     (array Keyword "-" -1)
     (array Expression null to-negate)
     (array CloseParen null -1)))
  (return (CStatementOutput environment context tokens startTokenIndex
                            negate-statement (array-size negate-statement)
                            output)))

;; A global and module-local variable "defer"
(defmacro before-exit (work-body array)
  (get-or-create-comptime-var environment before-exit-work
                              (template (in std vector) (addr (const Token))))
  (call-on push_back (deref before-exit-work) work-body)
  (return true))

;; Run the deferred code
(defmacro run-before-exit-work ()
  (get-or-create-comptime-var environment before-exit-work
                              (template (in std vector) (addr (const Token))))
  ;; TODO: Should we sort it eventually?
  (for-in start-work-token (addr (const Token)) (deref before-exit-work)
    (tokenize-push output
      (token-splice start-work-token)))
  (return true))

;;
;; Declarations
;;

;; Especially useful for casting from malloc:
;; (var my-thing (addr thing) (type-cast (addr thing) (malloc (sizeof thing))))
;; vs.
;; (var-cast-to my-thing (addr thing) (malloc (sizeof thing)))
(defmacro var-cast-to (var-name symbol type any expression-to-cast any)
  (tokenize-push output
    (var (token-splice var-name) (token-splice type)
      (type-cast (token-splice expression-to-cast)
                 (token-splice type))))
  (return true))

;; Given
;; (declare-extern-function my-func (i int &return bool))
;; Output
;; bool myFunc(int i);
;; This is useful for forward declarations of functions or declaring functions linked dynamically
(defgenerator declare-extern-function (name-token (ref symbol) signature-index (index array))
  (quick-token-at signature-token signature-index)
  (var return-type-start int -1)
  (var is-variadic-index int -1)
  (var arguments (template (in std vector) FunctionArgumentTokens))
  (unless (parseFunctionSignature tokens signature-index arguments
                                  return-type-start is-variadic-index)
    (return false))

  (var end-signature-index int (FindCloseParenTokenIndex tokens signature-index))
  (unless (outputFunctionReturnType environment context tokens output return-type-start
                                    startTokenIndex
                                    end-signature-index
                                    true ;; Output to source
                                    false ;; Output to header
                                    RequiredFeatureExposure_ModuleLocal)
    (return false))

  (addStringOutput (path output . source) (field name-token contents)
                   StringOutMod_ConvertFunctionName
                   (addr name-token))

  (addLangTokenOutput (field output source) StringOutMod_OpenParen (addr signature-token))

  (unless (outputFunctionArguments environment context tokens output arguments
                                   is-variadic-index
                                   true ;; Output to source
                                   false ;; Output to header
                                   RequiredFeatureExposure_ModuleLocal)
    (return false))

  (addLangTokenOutput (field output source) StringOutMod_CloseParen (addr signature-token))

  (addLangTokenOutput (field output source) StringOutMod_EndStatement (addr signature-token))
  (return true))

;; Creates struct/class forward declarations in header files.
;; Example usage:
;; (forward-declare (namespace Ogre (class item) (struct my-struct)))
;; Outputs namespace Ogre { class item; struct my-struct;}
(defgenerator forward-declare (&rest start-body-index (index any))
  ;; TODO: Support global vs local?
  (var is-global bool true)
  (var output-dest (ref (template (in std vector) StringOutput))
    (? is-global (field output header) (field output source)))

  (var end-invocation-index int (FindCloseParenTokenIndex tokens startTokenIndex))
  (var current-index int start-body-index)
  (var namespace-stack (template (in std vector) int))
  (while (< current-index end-invocation-index)
    (var current-token (ref (const Token)) (at current-index tokens))
    ;; Invocations
    (when (= TokenType_OpenParen (field current-token type))
      (var invocation-token (ref (const Token)) (at (+ 1 current-index) tokens))
      (cond
        ((= 0 (call-on compare (field invocation-token contents) "namespace"))
         (unless (< (+ 3 current-index) end-invocation-index)
           (ErrorAtToken invocation-token "missing name or body arguments")
           (return false))
         (var namespace-name-token (ref (const Token)) (at (+ 2 current-index) tokens))
         (addStringOutput output-dest "namespace"
                          StringOutMod_SpaceAfter (addr invocation-token))
         (addStringOutput output-dest (field namespace-name-token contents)
                          StringOutMod_None (addr namespace-name-token))
         (addLangTokenOutput output-dest StringOutMod_OpenBlock (addr namespace-name-token))
         (call-on push_back namespace-stack (FindCloseParenTokenIndex tokens current-index))
         (RequiresCppFeature
          (field context module)
          (findObjectDefinition environment (call-on c_str (path context . definitionName > contents)))
          (? is-global RequiredFeatureExposure_Global
             RequiredFeatureExposure_ModuleLocal)
          (addr current-token)))

        ((= 0 (call-on compare (field invocation-token contents) "struct"))
         (unless (< (+ 2 current-index) end-invocation-index)
           (ErrorAtToken invocation-token "missing name argument")
           (return false))
         (var type-name-token (ref (const Token)) (at (+ 2 current-index) tokens))
         (unless (ExpectTokenType "forward-declare" type-name-token TokenType_Symbol)
           (return false))
         (addStringOutput output-dest "typedef"
                          StringOutMod_SpaceAfter (addr invocation-token))
         (addStringOutput output-dest (field invocation-token contents)
                          StringOutMod_SpaceAfter (addr invocation-token))
         (addStringOutput output-dest (field type-name-token contents)
                          StringOutMod_ConvertTypeName (addr type-name-token))
         (addLangTokenOutput output-dest StringOutMod_SpaceAfter (addr type-name-token))
         (addStringOutput output-dest (field type-name-token contents)
                          StringOutMod_ConvertTypeName (addr type-name-token))
         (addLangTokenOutput output-dest StringOutMod_EndStatement (addr type-name-token)))

        ((= 0 (call-on compare (field invocation-token contents) "class"))
         (unless (< (+ 2 current-index) end-invocation-index)
           (ErrorAtToken invocation-token "missing name argument")
           (return false))
         (var type-name-token (ref (const Token)) (at (+ 2 current-index) tokens))
         (unless (ExpectTokenType "forward-declare" type-name-token TokenType_Symbol)
           (return false))
         (addStringOutput output-dest (field invocation-token contents)
                          StringOutMod_SpaceAfter (addr invocation-token))
         (addStringOutput output-dest (field type-name-token contents)
                          StringOutMod_ConvertTypeName (addr type-name-token))
         (addLangTokenOutput output-dest StringOutMod_EndStatement (addr type-name-token))
         (RequiresCppFeature
            (field context module)
            (findObjectDefinition environment (call-on c_str (path context . definitionName > contents)))
            (? is-global RequiredFeatureExposure_Global
               RequiredFeatureExposure_ModuleLocal)
            (addr invocation-token)))
        (true
         (ErrorAtToken invocation-token "unknown forward-declare type")
         (return false))))

    (when (= TokenType_CloseParen (field current-token type))
      (for-in close-block-index int namespace-stack
              (when (= close-block-index current-index)
                (addLangTokenOutput output-dest StringOutMod_CloseBlock
                                    (addr (at current-index tokens))))))
    ;; TODO: Support function calls so we can do this recursively?
    ;; (set current-index
    ;; (getNextArgument tokens current-index end-invocation-index))
    (incr current-index))
  (return true))

(defgenerator declare-external (statement-token (arg-index array))
  (var statement (const (array CStatementOperation))
    (array
     (array Keyword "extern" -1)
     (array Statement null statement-token)))
  (return (c-statement-out statement))

  (return true))

(defgenerator extern-c (&rest body (arg-index any))
  (var extern-c-wrapper (const (array CStatementOperation))
    (array
     (array Keyword "#ifdef __cplusplus\n" -1)
     (array Keyword "extern \"C\"" -1)
     (array OpenBlock null -1)
     (array Keyword "#endif\n" -1)
     (array Body null body)
     (array Keyword "#ifdef __cplusplus\n" -1)
     (array CloseBlock null -1)
     (array Keyword "#endif\n" -1)))
  (return (CStatementOutput environment context tokens startTokenIndex
                            extern-c-wrapper (array-size extern-c-wrapper)
                            output)))

;; TODO: Better way to handle global vs. local
(defun-comptime defenum-internal (environment (ref EvaluatorEnvironment)
                                  context (ref (const EvaluatorContext))
                                  tokens (ref (const (template (in std vector) Token)))
                                  startTokenIndex int
                                  output (ref GeneratorOutput)
                                  is-global bool
                                  name (addr (const Token))
                                  enum-values int
                                  &return bool)
  (var output-dest (ref (template (in std vector) StringOutput))
    (? is-global (field output header) (field output source)))

  (addStringOutput output-dest "typedef enum" StringOutMod_SpaceAfter name)
  (addStringOutput output-dest (path name > contents) StringOutMod_ConvertTypeName name)
  (addLangTokenOutput output-dest StringOutMod_OpenBlock name)

  (var end-invocation-index int (FindCloseParenTokenIndex tokens startTokenIndex))
  (each-token-argument-in tokens enum-values end-invocation-index current-index
    (var current-token (addr (const Token)) (addr (at current-index tokens)))
    (unless (ExpectTokenType "defenum" (deref current-token) TokenType_Symbol)
      (return false))
    (addStringOutput output-dest (path current-token > contents)
                     ;; Use variable name so they aren't cumbersome to reference
                     StringOutMod_ConvertVariableName
                     current-token)
    (addLangTokenOutput output-dest StringOutMod_ListSeparator current-token))

  (addLangTokenOutput output-dest StringOutMod_NewlineAfter name)
  (addLangTokenOutput output-dest StringOutMod_CloseBlock name)
  (addStringOutput output-dest (path name > contents) StringOutMod_ConvertTypeName name)
  (addLangTokenOutput output-dest StringOutMod_EndStatement name)
  (return true))

(defgenerator defenum (name symbol
                       &rest enum-values (index any))
  (var is-global bool (!= (field context scope) EvaluatorScope_Body))
  (return (defenum-internal environment
          context
          tokens
          startTokenIndex
          output
          is-global
          name
          enum-values)))

(defgenerator defenum-local (name symbol
                             &rest enum-values (index any))
  (var is-global bool false)
  (return (defenum-internal environment
          context
          tokens
          startTokenIndex
          output
          is-global
          name
          enum-values)))

(defmacro defenum-and-string-table (name symbol &rest body symbol)
  (tokenize-push output
    (defenum (token-splice name)
      (token-splice-rest body tokens)))
  (var end-invocation-index int (FindCloseParenTokenIndex tokens startTokenIndex))
  (var strings (template (in std vector) Token))
  (each-token-argument-in tokens (- body (addr (at 0 tokens))) end-invocation-index current-index
    (var current-token (addr (const Token)) (addr (at current-index tokens)))
    (var new-string Token (deref current-token))
    (set (field new-string type) TokenType_String)
    (call-on push_back strings new-string))
  (var strings-name Token (deref name))
  (call-on append (field strings-name contents) "--strings")
  (var strings-count-name Token (deref name))
  (call-on append (field strings-count-name contents) "--strings-count")
  (var num-strings Token (deref name))
  (token-contents-snprintf num-strings "%d"
                           (type-cast (call-on size strings) int))
  (tokenize-push output
    (var-global (token-splice-addr strings-name) (array (addr (const char)))
      (array (token-splice-array strings)))
    (var-global (token-splice-addr strings-count-name) int (token-splice-addr num-strings)))
  (return true))

(defmacro defenum-and-string-table-local (name symbol &rest body symbol)
  (tokenize-push output
    (defenum-local (token-splice name)
      (token-splice-rest body tokens)))
  (var end-invocation-index int (FindCloseParenTokenIndex tokens startTokenIndex))
  (var strings (template (in std vector) Token))
  (each-token-argument-in tokens (- body (addr (at 0 tokens))) end-invocation-index current-index
    (var current-token (addr (const Token)) (addr (at current-index tokens)))
    (var new-string Token (deref current-token))
    (set (field new-string type) TokenType_String)
    (call-on push_back strings new-string))
  (var strings-name Token (deref name))
  (call-on append (field strings-name contents) "--strings")
  (tokenize-push output
    (var (token-splice-addr strings-name) (array (addr (const char)))
      (array (token-splice-array strings))))
  (return true))

;; Declare a variable which can be accessed globally, but is not exposed in the header.
(defgenerator var-hidden-global (name symbol
                                 ;; global-type-index (index array)
                                 module-type-index (index array)
                                 value-index (index array))
  ;; Define an array in the module and expose it as a pointer
  ;; e.g. in .c:
  ;;   int myArray[] = {1 2 3};
  ;; in .h:
  ;;   int* myArray;
  ;; This is a hack to allow arrays to be declared without needing the type in the header.
  ;; I originally thought to declare it as a pointer, but C doesn't like that.
  ;; Instead, you'll need to declare the extern array in the referencing module.
  ;; (var global-type-output (template (in std vector) StringOutput))
  ;; (var global-type-output-after-name (template (in std vector) StringOutput))
  ;; (unless (tokenizedCTypeToString_Recursive
  ;;          environment context tokens global-type-index
  ;;          true global-type-output global-type-output-after-name
  ;;          RequiredFeatureExposure_Global)
  ;;   (return false))
  ;; (addModifierToStringOutput (call-on back global-type-output) StringOutMod_SpaceAfter)
  ;; (addStringOutput (field output header) "extern" StringOutMod_SpaceAfter
  ;;   	           (addr (at global-type-index tokens)))
  ;; (PushBackAll (field output header) global-type-output)
  ;; (addStringOutput (field output header) (path name > contents)
  ;;                  StringOutMod_ConvertVariableName name)
  ;; (PushBackAll (field output header) global-type-output-after-name)
  ;; (addLangTokenOutput (field output header) StringOutMod_EndStatement name)

  (var module-type-output (template (in std vector) StringOutput))
  (var module-type-output-after-name (template (in std vector) StringOutput))
  (unless (tokenizedCTypeToString_Recursive
           environment context tokens module-type-index
	       true module-type-output module-type-output-after-name
	       RequiredFeatureExposure_ModuleLocal)
    (return false))
  (addModifierToStringOutput (call-on back module-type-output) StringOutMod_SpaceAfter)
  (PushBackAll (field output source) module-type-output)
  (addStringOutput (field output source) (path name > contents)
                   StringOutMod_ConvertVariableName name)
  (PushBackAll (field output source) module-type-output-after-name)

  ;; Value
  (addLangTokenOutput (field output source) StringOutMod_SpaceAfter (addr (at value-index tokens)))
  (addStringOutput (field output source) "=" StringOutMod_SpaceAfter (addr (at value-index tokens)))
  (var expression-context EvaluatorContext context)
  (set (field expression-context scope) EvaluatorScope_ExpressionsOnly)
  (unless (= 0 (EvaluateGenerate_Recursive
                environment expression-context tokens value-index
		        output))
    (return false))
  (addLangTokenOutput (field output source) StringOutMod_EndStatement name)
  (return true))

;;
;; Iteration/Looping
;;

;; Pass (none) for initializer if it is unused
(defgenerator c-for (initializer (arg-index any)
                     conditional (arg-index any)
                     update (arg-index any)
                     ;; Cannot be optional due to CStatementOutput limitation
                     &rest body (arg-index any))
  (when (= 0 (call-on compare (field (at (+ 2 initializer startTokenIndex) tokens) contents) "none"))
    (var no-initializer-statement (array (const CStatementOperation))
      (array
       (array Keyword "for" -1)
       (array OpenParen null -1)
       (array Keyword ";" -1)
       (array Expression null conditional)
       (array Keyword ";" -1)
       (array Expression null update)
       (array CloseParen null -1)
       (array OpenContinueBreakableScope null -1)
       (array Body null body)
       (array CloseContinueBreakableScope null -1)))
    (return (c-statement-out no-initializer-statement)))
  (var statement (array (const CStatementOperation))
    (array
     (array Keyword "for" -1)
     (array OpenParen null -1)
     (array Statement null initializer)
     (array Expression null conditional)
     (array Keyword ";" -1)
     (array Expression null update)
     (array CloseParen null -1)
     (array OpenContinueBreakableScope null -1)
     (array Body null body)
     (array CloseContinueBreakableScope null -1)))
  (return (c-statement-out statement)))

;; This only works for arrays where the size is known at compile-time
(defmacro each-in-array (array-name any iterator-name symbol &rest body any)
  (tokenize-push output
    (each-in-range (array-size (token-splice array-name)) (token-splice iterator-name)
      (token-splice-rest body tokens)))
  (return true))

(defmacro each-item-addr-in-array (array-name any iterator-name symbol
                                   item-name symbol item-type any &rest body any)
  (tokenize-push output
    (each-in-range (array-size (token-splice array-name)) (token-splice iterator-name)
      (var (token-splice item-name) (token-splice item-type)
        (addr (at (token-splice iterator-name) (token-splice array-name))))
      (token-splice-rest body tokens)))
  (return true))

(defmacro each-item-in-array (array-name any iterator-name symbol
                              item-name symbol item-type any &rest body any)
  (tokenize-push output
    (each-in-range (array-size (token-splice array-name)) (token-splice iterator-name)
      (var (token-splice item-name) (token-splice item-type)
        (at (token-splice iterator-name) (token-splice array-name)))
      (token-splice-rest body tokens)))
  (return true))

;; Note: Will reevaluate the range expression each iteration
(defmacro each-in-range (range any iterator-name symbol &rest body any)
  (tokenize-push output
    (c-for (var (token-splice iterator-name) int 0)
        (< (token-splice iterator-name) (token-splice range))
        (incr (token-splice iterator-name))
      (token-splice-rest body tokens)))
  (return true))

;; [start, end) a.k.a. standard C for loop with non-zero start
(defmacro each-in-interval (start any end any iterator-name symbol &rest body any)
  (tokenize-push output
    (c-for (var (token-splice iterator-name) int (token-splice start))
        (< (token-splice iterator-name) (token-splice end))
        (incr (token-splice iterator-name))
      (token-splice-rest body tokens)))
  (return true))

;; [start, end] where start >= end
(defmacro each-in-closed-interval-descending (start-max any end-min any
                                              iterator-name symbol
                                              &rest body any)
  (tokenize-push output
    (c-for (var (token-splice iterator-name) int (token-splice start-max))
        (>= (token-splice iterator-name) (token-splice end-min))
        (decr (token-splice iterator-name))
      (token-splice-rest body tokens)))
  (return true))

(defmacro each-char-in-string (start-char any iterator-name symbol &rest body any)
  (tokenize-push output
    (c-for (var (token-splice iterator-name) (addr char) (token-splice start-char))
        (deref (token-splice iterator-name))
        (incr (token-splice iterator-name))
      (token-splice-rest body tokens)))
  (return true))

(defmacro each-char-in-string-const (start-char any iterator-name symbol &rest body any)
  (tokenize-push output
    (c-for (var (token-splice iterator-name) (addr (const char)) (token-splice start-char))
        (deref (token-splice iterator-name))
        (incr (token-splice iterator-name))
      (token-splice-rest body tokens)))
  (return true))

;;
;; String helpers
;;

;; Ensure that the null terminator is written, which strncpy does NOT do when hitting buffer size
(defmacro safe-strncpy (dest any source any dest-buffer-size any)
  (tokenize-push output
    (strncpy (token-splice dest source) (- (token-splice dest-buffer-size) 1))
    (set (at (- (token-splice dest-buffer-size) 1) (token-splice dest)) 0))
  (return true))

;;
;; Preprocessor
;;

(defgenerator c-preprocessor-define-constant (define-name (arg-index symbol) value (arg-index any))
  (var define-statement (const (array CStatementOperation))
    (array
     (array Keyword "#define" -1)
     (array Expression null define-name)
     (array Keyword " " -1)
     (array Expression null value)
     (array KeywordNoSpace "\n" -1)))
  (return (c-statement-out define-statement)))

(defgenerator c-preprocessor-define-constant-global (define-name symbol value symbol)
  (addStringOutput (field output header) "#define" StringOutMod_SpaceAfter define-name)
  (addStringOutput (field output header) (path define-name > contents)
                   (type-cast (bit-or StringOutMod_ConvertVariableName StringOutMod_SpaceAfter)
                              StringOutputModifierFlags)
                   define-name)
  (addStringOutput (field output header) (path value > contents)
                   StringOutMod_None
                   value)
  ;; TODO: Cannot actually evaluate into the header. Stupid.
  ;; (var expression-context EvaluatorContext context)
  ;; (set (field expression-context scope) EvaluatorScope_ExpressionsOnly)
  ;; (unless (= (EvaluateGenerate_Recursive environment expression-context
  ;;                                        tokens value output)
  ;;            0)
  ;;   (return false))
  (addLangTokenOutput (field output header) StringOutMod_NewlineAfter define-name)
  (return true))

(defgenerator c-preprocessor-undefine (define-name symbol)
  (var define-statement (const (array CStatementOperation))
    (array
     (array Keyword "#undef" -1)
     (array Expression null 1)
     (array KeywordNoSpace "\n" -1)))
  (return (c-statement-out define-statement)))

(defgenerator if-c-preprocessor (conditional (arg-index array)
                                   true-block (arg-index any)
                                   &optional false-block (arg-index any))
  (if (!= -1 false-block)
      (scope
       (var statement (const (array CStatementOperation))
         (array
          (array Keyword "#if" -1)
          (array Expression null conditional)
          (array KeywordNoSpace "\n" -1)
          (array Statement null true-block)
          (array KeywordNoSpace "#else" -1)
          (array KeywordNoSpace "\n" -1)
          (array Statement null false-block)
          (array KeywordNoSpace "#endif" -1)
          (array KeywordNoSpace "\n" -1)))
       (return (c-statement-out statement)))
      (scope
       (var statement (const (array CStatementOperation))
         (array
          (array Keyword "#if" -1)
          (array Expression null conditional)
          (array KeywordNoSpace "\n" -1)
          (array Statement null true-block)
          (array KeywordNoSpace "#endif" -1)
          (array KeywordNoSpace "\n" -1)))
       (return (c-statement-out statement)))))

(defgenerator if-c-preprocessor-defined (preprocessor-symbol (arg-index symbol)
                                         true-block (arg-index any)
                                         &optional false-block (arg-index any))
  (if (!= -1 false-block)
      (scope
       (var statement (const (array CStatementOperation))
         (array
          (array Keyword "#ifdef" -1)
          (array Expression null preprocessor-symbol)
          (array KeywordNoSpace "\n" -1)
          (array Statement null true-block)
          (array KeywordNoSpace "#else" -1)
          (array KeywordNoSpace "\n" -1)
          (array Statement null false-block)
          (array KeywordNoSpace "#endif" -1)
          (array KeywordNoSpace "\n" -1)))
       (return (c-statement-out statement)))
      (scope
       (var statement (const (array CStatementOperation))
         (array
          (array Keyword "#ifdef" -1)
          (array Expression null preprocessor-symbol)
          (array KeywordNoSpace "\n" -1)
          (array Statement null true-block)
          (array KeywordNoSpace "#endif" -1)
          (array KeywordNoSpace "\n" -1)))
       (return (c-statement-out statement)))))

;;
;; Aliasing
;;

;; When encountering references of (alias), output C function invocation underlyingFuncName()
;; Note that this circumvents the reference system in order to reduce compile-time cost of using
;; aliased functions. This will probably have to be fixed eventually
(defgenerator output-aliased-c-function-invocation (&optional &rest arguments any)
  (var invocation-name (ref (const (in std string))) (field (at (+ 1 startTokenIndex) tokens) contents))
  ;; TODO Hack: If I was referenced directly, don't do anything, because it's only for dependencies
  (when (= 0 (call-on compare invocation-name
                      "output-aliased-c-function-invocation"))
    (return true))
  (get-or-create-comptime-var environment c-function-aliases
                              (template (in std unordered_map) (in std string) (in std string)))
  (def-type-alias FunctionAliasMap (template (in std unordered_map) (in std string) (in std string)))

  (var alias-func-pair (in FunctionAliasMap iterator)
    (call-on-ptr find c-function-aliases invocation-name))
  (unless (!= alias-func-pair (call-on-ptr end c-function-aliases))
    (ErrorAtToken (at (+ 1 startTokenIndex) tokens)
                  "unknown function alias. This is likely a code error, as it should never have " \
                  "gotten this far")
    (return false))

  (var underlying-func-name (ref (const (in std string))) (path alias-func-pair > second))

  ;; (NoteAtTokenf (at (+ 1 startTokenIndex) tokens) "found %s, outputting %s. Output is %p"
  ;;               (call-on c_str invocation-name) (call-on c_str underlying-func-name)
  ;;               (addr output))

  (if arguments
      (scope
       (var invocation-statement (const (array CStatementOperation))
         (array
          (array KeywordNoSpace (call-on c_str underlying-func-name) -1)
          (array OpenParen null -1)
          (array ExpressionList null 1)
          (array CloseParen null -1)
          (array SmartEndStatement null -1)))
       (return (CStatementOutput environment context tokens startTokenIndex
                                 invocation-statement (array-size invocation-statement)
                                 output)))
      (scope
       (var invocation-statement (const (array CStatementOperation))
         (array
          (array KeywordNoSpace (call-on c_str underlying-func-name) -1)
          (array OpenParen null -1)
          (array CloseParen null -1)
          (array SmartEndStatement null -1)))
       (return (CStatementOutput environment context tokens startTokenIndex
                                 invocation-statement (array-size invocation-statement)
                                 output)))))

;; When encountering references of (alias), output C function invocation underlyingFuncName()
;; output-aliased-c-function-invocation actually does the work
(defgenerator def-c-function-alias (alias (ref symbol) underlying-func-name (ref symbol))
  ;; TODO Hack: Invoke this to create a dependency on it, so by the time we make the
  ;; alias, we can set the generators table to it
  (output-aliased-c-function-invocation)

  (get-or-create-comptime-var environment c-function-aliases
                              (template (in std unordered_map) (in std string) (in std string)))
  (set (at (field alias contents) (deref c-function-aliases)) (field underlying-func-name contents))

  ;; (Logf "aliasing %s to %s\n" (call-on c_str (field underlying-func-name contents))
  ;;       (call-on c_str (field alias contents)))

  ;; Upen encountering an invocation of our alias, run the aliased function output
  ;; In case the function already has references, resolve them now. Future invocations will be
  ;; handled immediately (because it'll be in the generators list)
  (var evaluated-success bool
    (registerEvaluateGenerator environment (call-on c_str (field alias contents))
                               (at "output-aliased-c-function-invocation" (field environment generators))
                               (addr alias)))

  (return evaluated-success))

;;
;; Uncategorized
;;

;; Make it easier to specify fields by name:
;;(set-fields my-struct
;;            member-a 43
;;            member-b (* 3 4)
;;            (member-c field-a) 3)
(defmacro set-fields (output-struct any &rest set-fields (index any))
  (var end-invocation-index int (FindCloseParenTokenIndex tokens startTokenIndex))
  (var field-name-token (addr (const Token)) null)
  (each-token-argument-in tokens set-fields end-invocation-index current-index
    (unless field-name-token
      (set field-name-token (addr (at current-index tokens)))
      (continue))
    (var set-value-token (addr (const Token)) (addr (at current-index tokens)))
    (if (= (path field-name-token > type) TokenType_OpenParen)
        (scope ;; Support nested fields via parens
         (tokenize-push output
           (set (field (token-splice output-struct)
                       (token-splice-rest (+ field-name-token 1) tokens))
                (token-splice set-value-token))))
        (scope
         (tokenize-push output
           (set (field (token-splice output-struct) (token-splice field-name-token))
                (token-splice set-value-token)))))
    (set field-name-token null))
  (when field-name-token
    (ErrorAtToken (deref field-name-token) "Expected value for this field")
    (return false))
  (return true))

;; TODO: Support more compilers and platforms
;; TODO: Only use if in C, because C++11 added alignof()
(defmacro alignment-of (type-or-field any)
  ;; GCC: https://gcc.gnu.org/onlinedocs/gcc/Alignment.html#Alignment
  (tokenize-push output
    (__alignof__ (token-splice type-or-field)))
  (return true))

;;
;; Helpers for meta stuff, e.g. the file and line number of the token itself
;;

;; The current file, as a string
(defmacro this-file ()
  (var filename-token Token (at startTokenIndex tokens))
  (set (field filename-token type) TokenType_String)
  (token-contents-snprintf filename-token "%s" (field filename-token source))
  (tokenize-push output
    (token-splice-addr filename-token))
  (return true))

;; The current line, as a symbol
(defmacro this-line ()
  (var line-token Token (at startTokenIndex tokens))
  (set (field line-token type) TokenType_Symbol)
  (token-contents-snprintf line-token "%d" (field line-token lineNumber))
  (tokenize-push output
    (token-splice-addr line-token))
  (return true))

;; This tacks the file and line on as the last two arguments to the function.
;; define-forward-file-line-macro is used to know the association between this macro's invocation
;; and the actual desired function to call.
(defmacro call-function-with-file-line (&optional &rest arguments any)
  (when (and arguments
             (= 0 (call-on compare (path arguments > contents) "for-dependencies")))
    (return true))
  (get-or-create-comptime-var environment c-forward-macro-functions
                              (template (in std unordered_map) (in std string) (in std string)))
  ;; We spoofed the macro call, now we need to rename the function
  (var function-to-call Token (at (+ 1 startTokenIndex) tokens))
  (set (field function-to-call contents)
       (at (field function-to-call contents) (deref c-forward-macro-functions)))
  (var filename-token Token (at startTokenIndex tokens))
  (set (field filename-token type) TokenType_String)
  (token-contents-snprintf filename-token "%s" (field filename-token source))
  (var line-token Token (at startTokenIndex tokens))
  (set (field line-token type) TokenType_Symbol)
  (token-contents-snprintf line-token "%d" (field line-token lineNumber))
  (tokenize-push output
    (call (token-splice-addr function-to-call)
          (token-splice-rest arguments tokens)
          (token-splice-addr filename-token) (token-splice-addr line-token)))
  (return true))

;; Upon finding macro-name, call function-to-call in its place with the same arguments, but with
;; the file and line of the invocation tacked on the end of the arguments list.
(defmacro define-forward-file-line-macro (macro-name symbol function-to-call symbol)
  ;; Make sure the function exists before we can get built so we can reference it
  (call-function-with-file-line "for-dependencies")
  (declare-extern-function
   call-function-with-file-line
   (environment (ref EvaluatorEnvironment) context (ref (const EvaluatorContext))
    tokens (ref (const (template (in std vector) Token))) startTokenIndex int
    output (ref (template (in std vector) Token))
    &return bool))
  (get-or-create-comptime-var environment c-forward-macro-functions
                              (template (in std unordered_map) (in std string) (in std string)))
  (set (at (path macro-name > contents) (deref c-forward-macro-functions))
       (path function-to-call > contents))
  (registerEvaluateMacro environment (call-on c_str (path macro-name > contents))
                         call-function-with-file-line macro-name)
  (return true))
