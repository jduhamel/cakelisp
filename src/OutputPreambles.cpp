#include "OutputPreambles.hpp"

// Must use extern "C" for dynamic symbols, because otherwise name mangling makes things hard
const char* macroSourceHeading =
    "#include \"Evaluator.hpp\""
    "\n#include \"EvaluatorEnums.hpp\""
    "\n#include \"Tokenizer.hpp\""
    "\nextern \"C\"\n{\n";
// Close extern "C" block
const char* macroSourceFooter = "}\n";

// Must use extern "C" for dynamic symbols, because otherwise name mangling makes things hard
const char* generatorSourceHeading =
    "#include \"Evaluator.hpp\""
    "\n#include \"EvaluatorEnums.hpp\""
	"\n#include \"Tokenizer.hpp\""
	"\n#include \"GeneratorHelpers.hpp\""
    "\nextern \"C\"\n{\n";
// Close extern "C" block
const char* generatorSourceFooter = "}\n";

const char* generatedSourceHeading = nullptr;
const char* generatedSourceFooter = nullptr;
const char* generatedHeaderHeading = "#pragma once\n";
const char* generatedHeaderFooter = nullptr;
