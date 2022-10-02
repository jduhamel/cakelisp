#pragma once

#include <unordered_map>
#include <vector>

#include "DynamicArray.hpp"
#include "DynamicString.hpp"
#include "Exporting.hpp"
#include "FileTypes.hpp"

extern const char* compilerObjectExtension;
extern const char* compilerDebugSymbolsExtension;
extern const char* compilerImportLibraryExtension;
extern const char* linkerDynamicLibraryPrefix;
extern const char* linkerDynamicLibraryExtension;
extern const char* defaultExecutableName;
extern const char* precompiledHeaderExtension;

struct BuildArgumentConverter
{
	DynamicStringArray* stringsIn;

	// Use C++ to manage our string memory, pointed to by argumentsOut
	DynamicStringArray argumentsOutMemory;
	CStringArray* argumentsOut;
	void (*argumentConversionFunc)(char* buffer, int bufferSize, const char* stringIn,
	                               const char* executableName);
};

void convertBuildArguments(BuildArgumentConverter* argumentsToConvert, int numArgumentsToConvert,
                           const char* buildExecutable);

void makeIncludeArgument(char* buffer, int bufferSize, const char* searchDir);

// On Windows, extra formatting is required to output objects
void makeObjectOutputArgument(char* buffer, int bufferSize, const char* objectName);
void makeDebugSymbolsOutputArgument(char* buffer, int bufferSize, const char* debugSymbolsName);
void makeImportLibraryPathArgument(char* buffer, int bufferSize, const char* path,
                                   const char* buildExecutable);
void makeDynamicLibraryOutputArgument(char* buffer, int bufferSize, const char* libraryName,
                                      const char* buildExecutable);
CAKELISP_API void makeExecutableOutputArgument(char* buffer, int bufferSize,
                                               const char* executableName,
                                               const char* linkExecutable);
void makeLinkLibraryArgument(char* buffer, int bufferSize, const char* libraryName,
                             const char* linkExecutable);
void makeLinkLibrarySearchDirArgument(char* buffer, int bufferSize, const char* searchDir,
                                      const char* linkExecutable);
void makeLinkLibraryRuntimeSearchDirArgument(char* buffer, int bufferSize, const char* searchDir,
                                             const char* linkExecutable);
void makeLinkerArgument(char* buffer, int bufferSize, const char* argument,
                        const char* linkExecutable);
void makePrecompiledHeaderOutputArgument(char* buffer, int bufferSize, const char* outputName,
                                         const char* precompilerExecutable);
void makePrecompiledHeaderIncludeArgument(char* buffer, int bufferSize,
                                          const char* precompiledHeaderName,
                                          const char* buildExecutable);

// On Windows, extra work is done to find the compiler and linker executables. This function handles
// looking up those environment variables to determine which executable to use
CAKELISP_API bool resolveExecutablePath(const char* fileToExecute, char* resolvedPathOut,
                                        int resolvedPathOutSize);

// /p:WindowsTargetPlatformVersion=%d.%d.%d.%d for MSBuild
CAKELISP_API void makeTargetPlatformVersionArgument(char* resolvedArgumentOut,
                                                    int resolvedArgumentOutSize);

typedef std::unordered_map<DynamicString, FileModifyTime> HeaderModificationTimeTable;

struct CrcWithFlags
{
	uint32_t crc;
	// Track whether we actually changed the state of whatever the CRC is pointing to, as opposed to
	// just loaded it and saved it back out
	bool wasModified;
};

// If an existing cached build was run, check the current build's commands against the previous
// commands via CRC comparison. This ensures changing commands will cause rebuilds
typedef std::unordered_map<DynamicString, CrcWithFlags> ArtifactCrcTable;
typedef std::pair<const DynamicString, CrcWithFlags> ArtifactCrcTablePair;

// Uses a hash of the artifact name, then the source name as key
typedef std::unordered_map<uint32_t, CrcWithFlags> HashedSourceArtifactCrcTable;
typedef std::pair<uint32_t, CrcWithFlags> HashedSourceArtifactCrcTablePair;

// Why read, merge, write? Because it's possible we ran another instance of cakelisp in the same
// directory during our build phase. The caches are shared state, so we don't want to blow away
// their data.
void buildReadMergeWriteCacheFile(const char* buildOutputDir, ArtifactCrcTable& cachedCommandCrcs,
                                  ArtifactCrcTable& newCommandCrcs,
                                  HashedSourceArtifactCrcTable& sourceArtifactFileCrcs,
                                  ArtifactCrcTable& changedHeaderCrcCache);

// Returns false if there were errors; the file not existing is not an error
bool buildReadCacheFile(const char* buildOutputDir, ArtifactCrcTable& cachedCommandCrcs,
                        HashedSourceArtifactCrcTable& sourceArtifactFileCrcs,
                        ArtifactCrcTable& headerCrcCache);

// commandArguments should have terminating null sentinel
bool commandEqualsCachedCommand(ArtifactCrcTable& cachedCommandCrcs, const char* artifactKey,
                                const char** commandArguments, CrcWithFlags* crcOut);

struct EvaluatorEnvironment;

// Check command, headers, and cache for whether the artifact is still valid
bool cppFileNeedsBuild(EvaluatorEnvironment& environment, const char* sourceFilename,
                       const char* artifactFilename, const char** commandArguments,
                       ArtifactCrcTable& cachedCommandCrcs, ArtifactCrcTable& newCommandCrcs,
                       HeaderModificationTimeTable& headerModifiedCache,
                       DynamicStringArray& headerSearchDirectories);

CAKELISP_API bool setPlatformEnvironmentVariable(const char* name, const char* value);
