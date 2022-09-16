(defun main (&return int)
  (return 0))

(defun-comptime print-metadata (manager (ref ModuleManager) module (addr Module) &return bool)
  (return true))

(add-compile-time-hook-module pre-build print-metadata)
