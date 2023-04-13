(c-import "<stdio.h>" "<string.h>" "<assert.h>" "<malloc.h>"
          ;; FILE*, bool TODO: How can I remove this from header?
          &with-decls "<stdio.h>" "<stdbool.h>")

(c-preprocessor-define MAX_PATH_LENGTH 1024)

(comptime-cond
 ('Unix
  (c-preprocessor-define __USE_XOPEN_EXTENDED) ;; for realpath to return (addr char), strdup
  (c-import "<stdlib.h>" "<libgen.h>" "<string.h>"))
 ('Windows
  (c-preprocessor-define WIN32_LEAN_AND_MEAN)
  (c-import "<windows.h>"))
 (true
  ;; If you're hitting this, you may need to port this over to whatever new platform you are on
  (comptime-error
   "This module requires platform-specific code. Please define your platform before importing" \
   " this module, e.g.: (comptime-define-symbol 'Unix). Supported platforms: 'Unix, 'Windows")))

(defun path-convert-to-forward-slashes (path-str (addr char))
  (each-char-in-string path-str current-char
	(when (= (deref current-char) '\\')
	  (set (deref current-char) '/'))))

(defun path-convert-to-backward-slashes (path-str (addr char))
  (each-char-in-string path-str current-char
	(when (= (deref current-char) '/')
	  (set (deref current-char) '\\'))))

;; Converts to forward slashes!
(defun make-absolute-path-allocated (fromDirectory (addr (const char)) filePath (addr (const char))
                                     &return (addr char))
  (comptime-cond
   ('Unix
	;; Second condition allows for absolute paths
	(if (and fromDirectory (!= (at 0 filePath) '/'))
        (scope
		 (var relativePath (array MAX_PATH_LENGTH char) (array 0))
		 (safe-string-print relativePath (sizeof relativePath) "%s/%s" fromDirectory filePath)
		 (return (realpath relativePath null)))
        (scope
		 ;; The path will be relative to the binary's working directory
		 (return (realpath filePath null)))))
   ('Windows
	(var absolutePath (addr char) (type-cast (malloc MAX_PATH_LENGTH) (addr char)))
	(var isValid bool false)
	(if fromDirectory
        (scope
		 (var relativePath (array MAX_PATH_LENGTH char) (array 0))
		 (safe-string-print relativePath (sizeof relativePath) "%s/%s" fromDirectory filePath)
		 (set isValid (_fullpath absolutePath relativePath MAX_PATH_LENGTH)))
		(set isValid (_fullpath absolutePath filePath MAX_PATH_LENGTH)))

	(unless isValid
	  (free absolutePath)
	  (return null))

    ;; Save the user from a whole lot of complexity by keeping slashes consistent with Unix style
    ;; Note that this means you may need to convert back to backslashes in some cases
    (path-convert-to-forward-slashes absolutePath)
	(return absolutePath))
   (true
    (comptime-error "Need to be able to normalize path on this platform")
	(return null))))

;; Expects: forward slash path. Returns: forward slash path
(defun get-directory-from-path (path (addr (const char)) bufferOut (addr char) bufferSize int)
  (comptime-cond
   ('Unix
    (declare-extern-function strdup (str (addr (const char)) &return (addr char)))
	(var pathCopy (addr char) (string-duplicate path))
	(var dirName (addr (const char)) (dirname pathCopy))
	(safe-string-print bufferOut bufferSize "%s" dirName)
	(free pathCopy))
   ('Windows
    (var converted-path (array MAX_PATH_LENGTH char) (array 0))
    (var num-printed size_t
	  (snprintf converted-path (sizeof converted-path) "%s" path))
	(when (= (at (- num-printed 1) converted-path) '/')
	  (set (at (- num-printed 1) converted-path) 0))
    (path-convert-to-backward-slashes converted-path)

	(var drive (array MAX_PATH_LENGTH char))
	(var dir (array MAX_PATH_LENGTH char))
	;; char fname[_MAX_FNAME];
	;; char ext[_MAX_EXT];
	(_splitpath_s converted-path drive (sizeof drive) dir (sizeof dir)
	              null 0 ;; fname
	              null 0) ;; extension
	(_makepath_s bufferOut bufferSize drive dir
                 null ;; fname
	             null) ;; extension

    (path-convert-to-forward-slashes bufferOut)

    ;; Remove trailing slash to match dirname
    (var bufferOut-length size_t (strlen bufferOut))
    (when (and (> bufferOut-length 1)
               (= (at (- bufferOut-length 1) bufferOut) '/'))
	  (set (at (- bufferOut-length 1) bufferOut) 0)))
   (true
    (comptime-error "Need to be able to strip path on this platform"))))

(defmacro safe-string-print (buffer any size any
                             format string &rest args any)
  (tokenize-push output
   (scope
    (var num-printed int (snprintf (token-splice buffer size format)
                                   (token-splice-rest args tokens)))
    (set (at num-printed (token-splice buffer)) '\0')))
 (return true))

(defmacro string-duplicate (string-to-dup any)
  (comptime-cond
   ('Unix
    (tokenize-push output
                   (strdup (token-splice string-to-dup))))
   ('Windows
    (tokenize-push output
                   (_strdup (token-splice string-to-dup)))))
  (return true))

(comptime-cond
 ('Unix
  (c-import "<unistd.h>")))

(defun file-exists (filename (addr (const char)) &return bool)
  (comptime-cond
   ('Unix
    (return (!= -1 (access filename F_OK))))
   ('Windows
	(return (!= (GetFileAttributes filename) INVALID_FILE_ATTRIBUTES))))
  (return false))

(defun read-file-into-memory-ex (in-file (addr FILE)
                                 ;; If file is larger than this, quit early
                                 ;; This allows the program to decide to handle large files differently
                                 ;; Pass 0 for no max
                                 maximum-size size_t
                                 size-out (addr size_t)
                                 &return (addr char))
  (fseek in-file 0 SEEK_END)
  (set (deref size-out) (ftell in-file))
  (rewind in-file)
  (when (and maximum-size (> (deref size-out) maximum-size))
    (return null))
  (var-cast-to out-buffer (addr char) (malloc (+ 1 (deref size-out))))
  (fread out-buffer (deref size-out) 1 in-file)
  (set (at (deref size-out) out-buffer) 0)
  (return out-buffer))

;; TODO: Windows CreateFile version of this
(defun read-file-into-memory (in-file (addr FILE) &return (addr char))
  (fseek in-file 0 SEEK_END)
  (var file-size size_t)
  (return (read-file-into-memory-ex in-file 0 (addr file-size))))

;; Useful when you know exactly how large the file should be
(defun open-read-file-into-buffer (filename (addr (const char))
                                   buffer (addr void)
                                   buffer-size size_t
                                   &return bool)
  (if-open-file-scoped filename "rb" in-file
    (scope
     (return (= 1 (fread buffer buffer-size
                         1 in-file))))
    (scope
     (return false))))

(defun write-string (out-file (addr FILE) out-string (addr (const char)))
  (var string-length size_t (strlen out-string))
  ;; (fprintf stderr "%s has length %d\n" out-string (type-cast string-length int))
  (fwrite (addr string-length) (sizeof string-length) 1 out-file)
  (fwrite out-string string-length 1 out-file))

(defun read-string (in-file (addr FILE) out-string-buffer (addr char) out-string-buffer-length size_t)
  (var string-length size_t 0)
  (fread (addr string-length) (sizeof string-length) 1 in-file)
  ;; (fprintf stderr "Next string has length %d\n" (type-cast string-length int))
  (assert (<= string-length out-string-buffer-length))
  (fread out-string-buffer string-length 1 in-file))

;; Plain old data with a known size
(defmacro write-pod (item any out-file any)
  (tokenize-push output
    (fwrite (addr (token-splice item)) (sizeof (token-splice item)) 1 (token-splice out-file)))
  (return true))

(defmacro read-pod (item any in-file any)
  (tokenize-push output
    (fread (addr (token-splice item)) (sizeof (token-splice item)) 1 (token-splice in-file)))
  (return true))

;; Automatically closes the file when exited
;; Note that the on-failure-block is intentionally not optional to encourage error handling
(defmacro if-open-file-scoped (filename any flags string file-pointer-name symbol
                               on-success-block array on-failure-block array)
  (tokenize-push output
    (scope
     (var (token-splice file-pointer-name) (addr FILE)
       (fopen (token-splice filename) (token-splice flags)))
     (if (token-splice file-pointer-name)
         (scope
          ;; We need defer in case the user returns out of this block etc.
          (defer (fclose (token-splice file-pointer-name)))
          (token-splice on-success-block))
         (scope
          (token-splice on-failure-block)))))
  (return true))

(ignore ;; For reference
 (defun-local test--load-save ()
   (scope
    (var test-file (addr FILE) (fopen "Test.bin" "wb"))
    (unless test-file
      (return 1))
    (write-string test-file "This is a test string")
    (fclose test-file)

    (var read-file (addr FILE) (fopen "Test.bin" "rb"))
    (unless read-file
      (return 1))
    (var read-buffer (array 256 char) (array 0))
    (read-string read-file read-buffer (sizeof read-buffer))
    (fprintf stderr "Got \"%s\"\n" read-buffer)
    (fclose read-file))))
