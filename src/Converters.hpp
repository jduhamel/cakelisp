#pragma once

#include "ConverterEnums.hpp"

#include "Exporting.hpp"

struct Token;

typedef struct NameStyleSettings
{
	// In general, you should write C/C++ types as they appear in C/C++, because then ETAGS etc. can
	// still find the C++ definition without running our conversion functions. If you really don't
	// want to, then typeNameMode should get you what you need, generally.
	//
	// WARNING: It is unwise to change this to pascal case because it will destroy C types which
	// start with lowercase characters (e.g. "int")
	NameStyleMode typeNameMode = NameStyleMode_PascalCaseIfLispy;
	// Using a different mode than your C/C++ conventions helps to distinguish Cakelisp functions
	// from C/C++ functions, if you want that
	// These will need to match if you use function pointers as variables, though
	// TODO: Remove the choice of doing different styles
	NameStyleMode functionNameMode = NameStyleMode_CamelCase;
	NameStyleMode argumentNameMode = NameStyleMode_CamelCase;
	NameStyleMode variableNameMode = NameStyleMode_CamelCase;
} NameStyleSettings;

// This only does anything to strings which have '-' as separators, or other symbols not allowed in
// C names. It's safe to e.g. pass in valid C names because they cannot have lisp-allowed symbols in
// them. This also means you can use whatever style you want in Cakelisp, and you'll get valid C/C++
// generated (so long as your non-'-' strings match the other C/C++ names)
CAKELISP_API void lispNameStyleToCNameStyle(NameStyleMode mode, const char* name, char* bufferOut,
                                            int bufferOutSize, const Token& token);
