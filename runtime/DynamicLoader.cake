;; Dynamically load code from shared objects (.so) or dynamic libraries (.dll)
;; This code was based on cakelisp/src/DynamicLoader.cpp, which is GPL, but I wanted runtime to be
;; MIT, so this copy is justified (this comment is by Macoy Madson, so I can change the license)
(c-import "<stdio.h>" "<string>" "<unordered_map>")

(import "FileUtilities.cake")

(comptime-cond
 ('Unix
  (c-import "<dlfcn.h>"))
 ('Windows
  (c-preprocessor-define WIN32_LEAN_AND_MEAN)
  (c-import "<windows.h>"))
 (true
  ;; If you're hitting this, you may need to port this over to whatever new platform you are on
  (comptime-error
   "This module requires platform-specific code. Please define your platform before importing" \
   " this module, e.g.: (comptime-define-symbol 'Unix). Supported platforms: 'Unix, 'Windows")))

(def-type-alias-global DynamicLibHandle (addr void))

(c-preprocessor-define MAX_PATH_LENGTH 256)

(defstruct DynamicLibrary
  handle DynamicLibHandle)

(def-type-alias DynamicLibraryMap (template (in std unordered_map) (in std string) DynamicLibrary))
(var dynamicLibraries DynamicLibraryMap)

;; allow-global-linking = Allow subsequently loaded libraries to resolve from this library You do
;;  NOT want this if you intend to reload the library, because it may resolve to the old version
(defun dynamic-library-load (libraryPath (addr (const char))
                             allow-global-linking bool
                             &return DynamicLibHandle)
  (var libHandle (addr void) null)

  (comptime-cond
   ('Unix
    ;; Clear error
    (dlerror)

    ;; RTLD_LAZY: Don't look up symbols the shared library needs until it encounters them
    ;; RTLD_GLOBAL: Allow subsequently loaded libraries to resolve from this library (mainly for
    ;; compile-time function execution)
    ;; Note that this requires linking with -Wl,-rpath,. in order to turn up relative path .so files
    (if allow-global-linking
        (set libHandle (dlopen libraryPath (bit-or RTLD_LAZY RTLD_GLOBAL)))
        (set libHandle (dlopen libraryPath (bit-or RTLD_LAZY RTLD_LOCAL))))

    (var error (addr (const char)) (dlerror))
    (when (or (not libHandle) error)
      (fprintf stderr "DynamicLoader Error:\n%s\n" error)
      (return null)))
   ('Windows
    ;; TODO Clean this up! Only the cakelispBin is necessary I think (need to double check that)
    ;; TODO Clear added dirs after? (RemoveDllDirectory())
    (var absoluteLibPath (addr char)
      (make-absolute-path-allocated null libraryPath))
    (var dllDirectory (array MAX_PATH_LENGTH char) (array 0))
    (get-directory-from-path absoluteLibPath dllDirectory (sizeof dllDirectory))
    (path-convert-to-backward-slashes absoluteLibPath)
    (var dll-directory-length size_t (strlen dllDirectory))
    ;; Append the trailing slash for Windows
    (set (at dll-directory-length dllDirectory) '/')
    (set (at (+ 1 dll-directory-length) dllDirectory) 0)
    (path-convert-to-backward-slashes dllDirectory)
    (scope ;; DLL directory
     (var wchars_num int (MultiByteToWideChar CP_UTF8 0 dllDirectory -1 null 0))
     (var wstrDllDirectory (addr wchar_t) (new-array wchars_num wchar_t))
     (MultiByteToWideChar CP_UTF8 0 dllDirectory -1 wstrDllDirectory wchars_num)
     (AddDllDirectory wstrDllDirectory)
     (delete-array wstrDllDirectory))

    ;; When loading cakelisp.lib, it will actually need to find cakelisp.exe for the symbols
    ;; This is only necessary for Cakelisp itself; left here for reference
    ;; (scope ;; Cakelisp directory
    ;;  (var cakelispBinDirectory (addr (const char))
    ;;    (makeAbsolutePath_Allocated null "bin"))
    ;;  (var wchars_num int (MultiByteToWideChar CP_UTF8 0 cakelispBinDirectory -1 null 0))
    ;;  (var wstrDllDirectory (addr wchar_t) (new-array wchars_num wchar_t))
    ;;  (MultiByteToWideChar CP_UTF8 0 cakelispBinDirectory -1 wstrDllDirectory wchars_num)
    ;;  (AddDllDirectory wstrDllDirectory)
    ;;  (free (type-cast cakelispBinDirectory (addr void)))
    ;;  (delete-array wstrDllDirectory))

    (set libHandle (LoadLibraryEx absoluteLibPath null
                                  (bit-or LOAD_LIBRARY_SEARCH_USER_DIRS
                                          LOAD_LIBRARY_SEARCH_DEFAULT_DIRS)))
    (unless libHandle
      (fprintf stderr "DynamicLoader Error: Failed to load %s with code %d\n" absoluteLibPath
               (GetLastError))
      (free (type-cast absoluteLibPath (addr void)))
      (return null))
    (free (type-cast absoluteLibPath (addr void)))))

  (set (at libraryPath dynamicLibraries) (array libHandle))
  (return libHandle))

(defun dynamic-library-get-symbol (library DynamicLibHandle
                                   symbolName (addr (const char))
                                   &return (addr void))
  (unless library
    (fprintf stderr "DynamicLoader Error: Received empty library handle\n")
    (return null))

  (comptime-cond
   ('Unix
    ;; Clear any existing error before running dlsym
    (var error (addr char) (dlerror))
    (when error
      (fprintf stderr "DynamicLoader Error:\n%s\n" error)
      (return null))

    (var symbol (addr void) (dlsym library symbolName))

    (set error (dlerror))
    (when error
      (fprintf stderr "DynamicLoader Error:\n%s\n" error)
      (return null))

    (return symbol))
   ('Windows
    (var procedure (addr void)
         (type-cast
          (GetProcAddress (type-cast library HINSTANCE) symbolName) (addr void)))
    (unless procedure
      (fprintf stderr "DynamicLoader Error:\n%d\n" (GetLastError))
      (return null))
    (return procedure))
   (true
    (return null))))

(defun dynamic-library-close-all ()
  (for-in libraryPair (ref (template (in std pair) (const (in std string)) DynamicLibrary)) dynamicLibraries
          (comptime-cond
           ('Unix
            (dlclose (field libraryPair second handle)))
           ('Windows
            (FreeLibrary (type-cast (field libraryPair second handle) HMODULE)))))
  (call-on clear dynamicLibraries))

(defun dynamic-library-close (handleToClose DynamicLibHandle)
  (var libHandle DynamicLibHandle null)
  (var libraryIt (in DynamicLibraryMap iterator) (call-on begin dynamicLibraries))
  (while (!= libraryIt (call-on end dynamicLibraries))
    (when (= handleToClose (path libraryIt > second . handle))
      (set libHandle (path libraryIt > second . handle))
      (call-on erase dynamicLibraries libraryIt)
      (break))
    (incr libraryIt))

  (unless libHandle
    (fprintf stderr "warning: closing library which wasn't in the list of loaded libraries\n")
    (set libHandle handleToClose))

  (comptime-cond
   ('Unix
    (dlclose libHandle))
   ('Windows
    (FreeLibrary (type-cast libHandle HMODULE)))))

;;
;; Building
;;

(comptime-cond
 ;; Did this weird thing because comptime-cond doesn't have (not)
 ('No-Hot-Reload-Options) ;; Make sure to not touch environment (they only want headers)
 (true
  (comptime-cond
   ('Unix
    ;; dl for dynamic loading
    (add-library-dependency "dl" "pthread")))))
