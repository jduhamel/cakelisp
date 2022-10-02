;; Hot-reloading library code modifier
;; - Changes all module and global variables to use the heap, thereby persisting state across reloads
;;   (in case it isn't obvious, you cannot persist state on the stack of code you're going to reload)
;; - Enables C-linkage and shared library toggles so functions can be discovered by the loader
;;
;; To use:
;; - Build the "loader" with HotReloading.cake. See HotLoader for an example loader
;; - The loader should link libraries that you need to use and shouldn't reload
;; - Now, modify the library which will be reloaded:
;; - Define the following function: (defun reloadable-entry-point (&return bool))
;;   (return true to reload, false to exit). This is the library's "main"
;; - Don't link any libraries which shouldn't be reloaded (they're in the loader)
;; - When building the library, put HotReloadingCodeModifier.cake as the *first* import
;;   (this ensures your functions will output c-linkage, and that a library will be built)
;;
;; It seems a bit complicated, but it's a really awesome feature, which makes it worth the price
;; Note that this will increase build times, because all affected functions and variables need to be
;; re-evaluated. There will be no macro expansions, however, which saves some time
;;
;; The goal is to be plug-and-play, but there are some modifications you'll need to do to your program:
;; - Variables with state which needs to be persisted across reloads need to be in module or global
;;   variables. This likely means moving function-local state variables into module scope
;; - Initialization needs to be gated by a module scope or global scope variable so the app doesn't
;;   re-initialize after each reload. It could also be a separate function which the loader calls once
;; - Change your main function to match the reload entry point signature
;; - Add some control within the program to instruct it to reload (return true from entry point)
;; - TODO: Array sizes are different when hot-reloading

;; Make all non-module-local functions dynamically loadable (we mainly care about our entry point
;; and initializer function, but the user could have a customized loader which hooks more)
;; This needs to be set before any functions which need C linkage are evaluated by Cakelisp
(set-cakelisp-option use-c-linkage true)

;; Because we could be building things without using C linkage, we need to separate all this code
;; out to prevent oscillating rebuilds
(add-build-config-label "HotLoadingModifiedCode")

;; This is a hack: We want to generate HotReloading.cake.hpp, but we don't want to inherit all the
;; loader-specific options. HotReloading.cake will see this define and not add the options
;; TODO: Make a context variable for preventing environment changes during &decls-only?
(comptime-define-symbol 'No-Hot-Reload-Options)

(import "ComptimeHelpers.cake")

;; This is redefined by make-code-hot-reloadable
(defun hot-reload-initialize-state ())

;; High-level explanation:
;; - Find all global and module-local variables
;; - Convert them to pointers so they can be stored on the heap, surviving reloads
;; - Create initializers for these pointers, which are called right after loading/reloading.
;; - Change all references to those variables to automatic pointer dereferencing. This is the expensive part
;; - Create a function for the loader to call to initialize all the pointers
;;
;; TODO: Arrays
;; TODO: Destructors
;; TODO: Initializers which reference other modified variables
;; TODO: Automatically convert main if found to entry point? A NoReload.cake could also be made (easy)
;; TODO: Potential bug if vars miss modified-vars stage. Need to store list of modified vars instead
;;       (would also need to keep track of modified references...yikes)
;; TODO: Need to take scope into account before changing a symbol (was it actually a module-private var)
(defun-comptime make-code-hot-reloadable (environment (ref EvaluatorEnvironment)
                                          &return bool)
  (var verbose bool false)

  (get-or-create-comptime-var modified-vars bool false)
  (when (deref modified-vars) ;; Modify variables only once
    (return true))
  (set (deref modified-vars) true)

  (fprintf stderr "HotReloading: Modifying code for hot-reloading.\n")
  (fprintf stderr "Subsequent modifications will not be hot-reloading safe\n")

  (get-or-create-comptime-var modules-with-import (template (in std unordered_map) (in std string) int))

  (defstruct modify-definition
    name (in std string)
    expanded-definition (template (in std vector) Token)
    module (addr Module))
  (var variables-to-modify (template (in std vector) modify-definition))

  ;; Collect variables. It must be done separately from modification because modification will
  ;; invalidate definition iterators
  (for-in definition-pair (ref ObjectDefinitionPair) (field environment definitions)
    (unless (= (field definition-pair second type) ObjectType_Variable)
      (continue))

    ;; Check if this variable is reloadable
    (var variable-start-invocation (addr (const Token))
      (field definition-pair second definitionInvocation))
    (var variable-type (addr (const Token)) (+ variable-start-invocation 3))
    (when (= TokenType_OpenParen (path variable-type > type))
      (var top-level-type (addr (const Token)) (+ variable-type 1))
      (cond
        ;; TODO: Add array support
        ((= 0 (call-on compare (path top-level-type > contents) "array"))
         (NoteAtToken (deref variable-start-invocation) "hot-reloadable arrays not supported yet")
         (continue))
        ;; Constants cannot be changed, so do not need to be reloaded
        ;; What happens when you reload the lib? Do the constants get updated?
        ((= 0 (call-on compare (path top-level-type > contents) "const"))
         (continue))))

    (when verbose (fprintf stderr ">>> Variable %s\n" (call-on c_str (field definition-pair first))))
    (var definition (ref ObjectDefinition) (field definition-pair second))
    (var var-to-modify modify-definition)
    (unless (CreateDefinitionCopyMacroExpanded definition
                                               (field var-to-modify expanded-definition))
      (return false))

    (set (field var-to-modify name) (field definition-pair first))
    (set (field var-to-modify module) (field definition context module))
    (call-on push_back variables-to-modify (call (in std move) var-to-modify)))

  ;; Collect references to variables we're going to need to auto-deref
  ;; TODO: variables can have initializers which reference modded variables, which the init functions
  ;; will need to take into account
  (var references-to-modify (template (in std vector) modify-definition))
  (for-in definition-pair (ref ObjectDefinitionPair) (field environment definitions)
    (unless (= (field definition-pair second type) ObjectType_Function)
      (continue))
    ;; This is pretty brutal: Expanding every single definition which might have a ref...
    (var definition (ref ObjectDefinition) (field definition-pair second))
    (var def-to-modify modify-definition)
    (unless (CreateDefinitionCopyMacroExpanded definition
                                               (field def-to-modify expanded-definition))
      (return false))

    (var reference-found bool false)
    (for-in token (ref (const Token)) (field def-to-modify expanded-definition)
      (unless (= (field token type) TokenType_Symbol)
        (continue))
      (for-in var-to-modify (ref (const modify-definition)) variables-to-modify
        (when (= 0 (call-on compare (field token contents)
                            (field (at 2 (field var-to-modify expanded-definition)) contents)))
          (set reference-found true)
          (break))))
    (unless reference-found
      (continue))

    (when verbose (fprintf stderr ">>> Reference(s) found in %s\n"
                          (call-on c_str (field definition-pair first))))

    (set (field def-to-modify module) (field definition context module))
    (set (field def-to-modify name) (field definition-pair first))
    (call-on push_back references-to-modify (call (in std move) def-to-modify)))

  (var initializer-names (template (in std vector) Token))

  ;; First = module filename. Second = initializer name token (for blaming)
  (var modules-to-import (template (in std unordered_map) (in std string) Token))

  ;; Pointerify variables and create initializer functions
  (for-in var-to-modify (ref modify-definition) variables-to-modify
    (var expanded-var-tokens (ref (template (in std vector) Token))
      (field var-to-modify expanded-definition))
    (var module (addr Module) (field var-to-modify module))

    ;; Before
    (when verbose (prettyPrintTokens expanded-var-tokens))

    (var start-token-index int 0)
    (var end-invocation-index int (- (call-on size expanded-var-tokens) 1))
    (var var-name-index int
      (getExpectedArgument "expected variable name"
                           expanded-var-tokens
                           start-token-index 1
	                       end-invocation-index))
	(when (= var-name-index -1)
	  (return false))

	(var type-index int
      (getExpectedArgument "expected variable type"
                           expanded-var-tokens
                           start-token-index 2
	                       end-invocation-index))
	(when (= type-index -1)
	  (return false))

    (var var-invocation (ref Token) (at 1 expanded-var-tokens))
    (var var-name (ref Token) (at var-name-index expanded-var-tokens))
    (var type-start (ref Token) (at type-index expanded-var-tokens))

    ;; Pointerify, remove intializer
    (var new-var-tokens (addr (template (in std vector) Token)) (new (template (in std vector) Token)))
    (call-on push_back (field environment comptimeTokens) new-var-tokens)
    (tokenize-push (deref new-var-tokens)
      ((token-splice-addr var-invocation)
       (token-splice-addr var-name)
       (addr (token-splice-addr type-start))
       null))

    ;; After
    (when verbose (prettyPrintTokens (deref new-var-tokens)))

    ;; Create intiailizer function
    (var init-function-name Token var-name)
    (var string-var-name Token var-name)
    (set (field string-var-name type) TokenType_String)
    (scope ;; Create initializer function name from variable name
     (var converted-name-buffer (array 64 char) (array 0))
     ;; TODO: Need to pass this in somehow
     (var name-style NameStyleSettings)
     (lispNameStyleToCNameStyle (field name-style variableNameMode) (call-on c_str (field var-name contents))
                                converted-name-buffer (sizeof converted-name-buffer) var-name)

     (var init-function-name-buffer (array 256 char) (array 0))
     (PrintfBuffer init-function-name-buffer "hotReloadInitVar_%s" converted-name-buffer)
     (set (field init-function-name contents) init-function-name-buffer))

    ;; Store it for making the global initializer, which will call all initializers
    (call-on push_back initializer-names init-function-name)
    (set (at (path module > filename) modules-to-import) init-function-name)

    (var assignment-tokens (template (in std vector) Token))
    (scope ;; Optional assignment
     (var assignment-index int
       (getArgument expanded-var-tokens start-token-index 3 endInvocationIndex))
     (when (!= assignment-index -1)
       (var assignment-token (addr Token) (addr (at assignment-index expanded-var-tokens)))
       (tokenize-push assignment-tokens
         (set (deref (token-splice-addr var-name)) (token-splice assignment-token)))))

    (var initializer-procedure-tokens (addr (template (in std vector) Token)) (new (template (in std vector) Token)))
    (call-on push_back (field environment comptimeTokens) initializer-procedure-tokens)
    ;; Note that we don't auto-deref this; this is the only place where that's the case
    (tokenize-push
        (deref initializer-procedure-tokens)
      (defun (token-splice-addr init-function-name) ()
        (var existing-value (addr void) nullptr)
        (if (hot-reload-find-variable (token-splice-addr string-var-name) (addr existing-value))
            (set (token-splice-addr var-name) (type-cast existing-value (addr (token-splice-addr type-start))))
            (scope ;; Create the variable
             ;; C can have an easier time with plain old malloc and cast
             (set (token-splice-addr var-name) (new (token-splice-addr type-start)))
             (token-splice-array assignment-tokens)
             ;; (set (deref (token-splice-addr var-name)) (token-splice-addr assignment))
             (hot-reload-register-variable (token-splice-addr string-var-name)
                                           (token-splice-addr var-name))))))
    (when verbose (prettyPrintTokens (deref initializer-procedure-tokens)))

    ;; Make the changes

    ;; Definition references invalid after this!

    (unless (ReplaceAndEvaluateDefinition environment (call-on c_str (field var-name contents))
                                          (deref new-var-tokens))
      (return false))

    (scope ;; Evaluate initializer
     (unless module
       (return false))
     (var initializer-context EvaluatorContext (array))
     (set (field initializer-context module) module)
     (set (field initializer-context scope) EvaluatorScope_Module)
     (set (field initializer-context definitionName)
          (addr (path environment . moduleManager > globalPseudoInvocationName)))
     (set (field initializer-context isRequired) true)

     ;; Make sure HotReloading header is included
     (var module-filename (addr (const char)) (path module > filename))
     (when (= (call-on find (deref modules-with-import) module-filename)
              (call-on-ptr end modules-with-import))
       (var import-hot-reloading-tokens (addr (template (in std vector) Token)) (new (template (in std vector) Token)))
       (call-on push_back (field environment comptimeTokens) import-hot-reloading-tokens)
       ;; Make sure we don't build our own version of this. The loader needs to manage it
       (tokenize-push (deref import-hot-reloading-tokens) (import &decls-only "HotReloading.cake"))
       (unless (= 0 (EvaluateGenerate_Recursive
                     environment initializer-context
                     (deref import-hot-reloading-tokens) 0
                     (deref (path module > generatedOutput))))
         (return false))
       ;; Meaningless number, only using hash table for fast lookup
       (set (at module-filename (deref modules-with-import)) 1))

     (unless (= 0 (EvaluateGenerate_Recursive
                   environment initializer-context
                   (deref initializer-procedure-tokens) 0
                   (deref (path module > generatedOutput))))
       (return false))))

  ;; Auto-dereference any references to the variables we've converted to pointers
  (for-in def-to-modify (ref modify-definition) references-to-modify
    (var expanded-def-tokens (ref (template (in std vector) Token))
      (field def-to-modify expanded-definition))
    (var module (addr Module) (field def-to-modify module))
    (var new-definition (addr (template (in std vector) Token)) (new (template (in std vector) Token)))
    (call-on push_back (field environment comptimeTokens) new-definition)
    (for-in token (ref (const Token)) expanded-def-tokens
      (unless (= (field token type) TokenType_Symbol)
        (call-on push_back (deref new-definition) token)
        (continue))

      ;; Check for reference
      (var reference-found bool false)
      (for-in var-to-modify (ref (const modify-definition)) variables-to-modify
        (when (= 0 (call-on compare (field token contents)
                            (field (at 2 (field var-to-modify expanded-definition)) contents)))
          (set reference-found true)
          (break)))
      ;; Just an uninteresting symbol
      (unless reference-found
        (call-on push_back (deref new-definition) token)
        (continue))

      ;; Insert the deref
      (var auto-deref-tokens (template (in std vector) Token))
      (tokenize-push auto-deref-tokens (deref (token-splice-addr token)))
      (PushBackAll (deref new-definition) auto-deref-tokens))

    ;; Replace it!
    (unless (ReplaceAndEvaluateDefinition environment (call-on c_str (field def-to-modify name))
                                          (deref new-definition))
      (return false)))

  ;; Create global initializer function to initialize all pointers on load/reload
  ;; Import all modules so that their initializers are exposed
  ;; Use this module to house the initializer. Putting it in some other module could cause unnecessary
  ;; rebuilds if different subsets of files are built. If it is housed here, only this file will
  ;; need to be recompiled
  (scope
   (var new-initializer-def (addr (template (in std vector) Token)) (new (template (in std vector) Token)))
   ;; Environment will handle freeing tokens for us
   (call-on push_back (field environment comptimeTokens) new-initializer-def)

   (var invocations (template (in std vector) Token))
   (for-in initializer-name (ref Token) initializer-names
     (tokenize-push invocations ((token-splice-addr initializer-name))))

   (var imports (template (in std vector) Token))
   (for-in module-to-import (ref (template std::pair (const (in std string)) Token)) modules-to-import
     (var module-name Token (field module-to-import second))
     (set (field module-name contents) (field module-to-import first))
     (set (field module-name type) TokenType_String)
     (tokenize-push imports (import (token-splice-addr module-name))))

   (when verbose (prettyPrintTokens imports))

   (tokenize-push (deref new-initializer-def)
     ;; TODO: This is a hack. Make sure imports work by adding working dir as search
     (add-c-search-directory-module ".")
     (token-splice-array imports)
     (defun hot-reload-initialize-state ()
       (token-splice-array invocations)))

   (unless (ReplaceAndEvaluateDefinition environment
                                         "hot-reload-initialize-state" (deref new-initializer-def))
     (return false)))

  (return true))

;;
;; Building
;;

(add-compile-time-hook post-references-resolved make-code-hot-reloadable)

(comptime-cond
 ('Unix
  (add-compiler-link-options "-shared")
  ;; TODO: Automatically make library if no main found?
  (set-cakelisp-option executable-output "libGeneratedCakelisp.so"))
 ('Windows
  (add-compiler-link-options "/DLL")
  (set-cakelisp-option executable-output "libGeneratedCakelisp.dll")))

;; This modifies code extensively, so it makes sense to have its own label
(add-build-config-label "HotReloadable")
