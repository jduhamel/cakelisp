#pragma once

#include <stdbool.h>

#include "Exporting.hpp"

#ifdef __cplusplus
extern "C"
{
#endif

typedef struct RunProcessArguments
{
	const char* fileToExecute;
	// nullptr = no change (use parent process's working dir)
	const char* workingDirectory;
	const char** arguments;
} RunProcessArguments;

CAKELISP_API int runProcess(const RunProcessArguments* arguments, int* statusOut);

typedef void (*SubprocessOnOutputFunc)(const char* subprocessOutput);

CAKELISP_API void pollSubprocessOutput(SubprocessOnOutputFunc onOutput);

CAKELISP_API void waitForAllProcessesClosed(SubprocessOnOutputFunc onOutput);

void PrintProcessArguments(const char** processArguments);

extern bool g_shouldLogProcesses;

#ifdef __cplusplus
}
#endif
