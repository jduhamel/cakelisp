#include "DynamicLoader.hpp"

#include <stdio.h>

#include <string>
#include <unordered_map>

#include "FileUtilities.hpp"
#include "Utilities.hpp"

#if defined(UNIX) || defined(MACOS)
#include <dlfcn.h>
#elif WINDOWS
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else
#error Platform support is needed for dynamic loading
#endif

typedef struct DynamicLibrary
{
	DynamicLibHandle handle;
} DynamicLibrary;

typedef std::unordered_map<DynamicString, DynamicLibrary> DynamicLibraryMap;
static DynamicLibraryMap dynamicLibraries;

DynamicLibHandle loadDynamicLibrary(const char* libraryPath)
{
	void* libHandle = nullptr;

#if defined(UNIX) || defined(MACOS)
	// Clear error
	dlerror();

	// RTLD_LAZY: Don't look up symbols the shared library needs until it encounters them
	// RTLD_GLOBAL: Allow subsequently loaded libraries to resolve from this library (mainly for
	// compile-time function execution)
	// Note that this requires linking with -Wl,-rpath,. in order to turn up relative path .so files
	libHandle = dlopen(libraryPath, RTLD_LAZY | RTLD_GLOBAL);

	const char* error = dlerror();
	if (!libHandle || error)
	{
		Logf("DynamicLoader Error:\n%s\n", error);
		return nullptr;
	}

#elif WINDOWS
	// TODO Clean this up! Only the cakelispBin is necessary I think (need to double check that)
	// TODO Clear added dirs after? (RemoveDllDirectory())
	const char* absoluteLibPath =
	    makeAbsolutePath_Allocated(/*fromDirectory=*/nullptr, libraryPath);
	char convertedPath[MAX_PATH_LENGTH] = {0};
	// TODO Remove, redundant with makeAbsolutePath_Allocated()
	makeBackslashFilename(convertedPath, sizeof(convertedPath), absoluteLibPath);
	char dllDirectory[MAX_PATH_LENGTH] = {0};
	getDirectoryFromPath(convertedPath, dllDirectory, sizeof(dllDirectory));
	{
		int wchars_num = MultiByteToWideChar(CP_UTF8, 0, dllDirectory, -1, nullptr, 0);
		wchar_t* wstrDllDirectory = new wchar_t[wchars_num];
		MultiByteToWideChar(CP_UTF8, 0, dllDirectory, -1, wstrDllDirectory, wchars_num);
		AddDllDirectory(wstrDllDirectory);
		delete[] wstrDllDirectory;
	}
	// When loading cakelisp.lib, it will actually need to find cakelisp.exe for the symbols
	{
		const char* cakelispBinDirectory =
		    makeAbsolutePath_Allocated(/*fromDirectory=*/nullptr, "bin");
		int wchars_num = MultiByteToWideChar(CP_UTF8, 0, cakelispBinDirectory, -1, nullptr, 0);
		wchar_t* wstrDllDirectory = new wchar_t[wchars_num];
		MultiByteToWideChar(CP_UTF8, 0, cakelispBinDirectory, -1, wstrDllDirectory, wchars_num);
		AddDllDirectory(wstrDllDirectory);
		free((void*)cakelispBinDirectory);
		delete[] wstrDllDirectory;
	}
	libHandle = LoadLibraryEx(convertedPath, nullptr,
	                          LOAD_LIBRARY_SEARCH_USER_DIRS | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
	if (!libHandle)
	{
		Logf("DynamicLoader Error: Failed to load %s with code %d\n", convertedPath,
		     GetLastError());
		free((void*)absoluteLibPath);
		return nullptr;
	}
	free((void*)absoluteLibPath);
#endif

	dynamicLibraries[libraryPath] = {libHandle};
	return libHandle;
}

void* getSymbolFromDynamicLibrary(DynamicLibHandle library, const char* symbolName)
{
	if (!library)
	{
		Log("DynamicLoader Error: Received empty library handle\n");
		return nullptr;
	}

#if defined(UNIX) || defined(MACOS)
	// Clear any existing error before running dlsym
	char* error = dlerror();
	if (error != nullptr)
	{
		Logf("DynamicLoader Error:\n%s\n", error);
		return nullptr;
	}

	void* symbol = dlsym(library, symbolName);

	error = dlerror();
	if (error != nullptr)
	{
		Logf("DynamicLoader Error:\n%s\n", error);
		return nullptr;
	}

	return symbol;
#elif WINDOWS
	void* procedure = (void*)GetProcAddress((HINSTANCE)library, symbolName);
	if (!procedure)
	{
		Logf("DynamicLoader Error:\n%d\n", GetLastError());
		return nullptr;
	}
	return procedure;
#else
	return nullptr;
#endif
}

void closeAllDynamicLibraries()
{
	for (std::pair<const DynamicString, DynamicLibrary>& libraryPair : dynamicLibraries)
	{
#if defined(UNIX) || defined(MACOS)
		dlclose(libraryPair.second.handle);
#elif WINDOWS
		FreeLibrary((HMODULE)libraryPair.second.handle);
#endif
	}

	dynamicLibraries.clear();
}

void closeDynamicLibrary(DynamicLibHandle handleToClose)
{
	DynamicLibHandle libHandle = nullptr;
	for (DynamicLibraryMap::iterator libraryIt = dynamicLibraries.begin();
	     libraryIt != dynamicLibraries.end(); ++libraryIt)
	{
		if (handleToClose == libraryIt->second.handle)
		{
			libHandle = libraryIt->second.handle;
			dynamicLibraries.erase(libraryIt);
			break;
		}
	}

	if (!libHandle)
	{
		Log("warning: closing library which wasn't in the list of loaded libraries\n");
		libHandle = handleToClose;
	}

#if defined(UNIX) || defined(MACOS)
	dlclose(libHandle);
#elif WINDOWS
	FreeLibrary((HMODULE)libHandle);
#endif
}
