#include "RunProcessHelpers.hpp"

#include "Logging.hpp"
#include "Utilities.hpp"

static const char* ProcessCommandArgumentTypeToString(ProcessCommandArgumentType type)
{
	switch (type)
	{
		case ProcessCommandArgumentType_None:
			return "None";
		case ProcessCommandArgumentType_String:
			return "String";
		case ProcessCommandArgumentType_SourceInput:
			return "SourceInput";
		case ProcessCommandArgumentType_ObjectOutput:
			return "ObjectOutput";
		case ProcessCommandArgumentType_DebugSymbolsOutput:
			return "DebugSymbolsOutput";
		case ProcessCommandArgumentType_ImportLibraryPaths:
			return "ImportLibraryPaths";
		case ProcessCommandArgumentType_ImportLibraries:
			return "ImportLibraries";
		case ProcessCommandArgumentType_CakelispHeadersInclude:
			return "CakelispHeadersInclude";
		case ProcessCommandArgumentType_IncludeSearchDirs:
			return "IncludeSearchDirs";
		case ProcessCommandArgumentType_AdditionalOptions:
			return "AdditionalOptions";
		case ProcessCommandArgumentType_PrecompiledHeaderOutput:
			return "PrecompiledHeaderOutput";
		case ProcessCommandArgumentType_PrecompiledHeaderInclude:
			return "PrecompiledHeaderInclude";
		case ProcessCommandArgumentType_ObjectInput:
			return "ObjectInput";
		case ProcessCommandArgumentType_DynamicLibraryOutput:
			return "DynamicLibraryOutput";
		case ProcessCommandArgumentType_LibrarySearchDirs:
			return "LibrarySearchDirs";
		case ProcessCommandArgumentType_Libraries:
			return "Libraries";
		case ProcessCommandArgumentType_LibraryRuntimeSearchDirs:
			return "LibraryRuntimeSearchDirs";
		case ProcessCommandArgumentType_LinkerArguments:
			return "LinkerArguments";
		case ProcessCommandArgumentType_ExecutableOutput:
			return "ExecutableOutput";
		default:
			return "Unknown";
	}
}

// The array will need to be deleted, but the array members will not
const char** MakeProcessArgumentsFromCommand(const char* fileToExecute,
                                             std::vector<ProcessCommandArgument>& arguments,
                                             const ProcessCommandInput* inputs, int numInputs)
{
	std::vector<const char*> argumentsAccumulate;

	for (unsigned int i = 0; i < arguments.size(); ++i)
	{
		ProcessCommandArgument& argument = arguments[i];

		if (argument.type == ProcessCommandArgumentType_String)
			argumentsAccumulate.push_back(argument.contents.c_str());
		else
		{
			bool found = false;
			for (int input = 0; input < numInputs; ++input)
			{
				if (inputs[input].type == argument.type)
				{
					for (const char* value : inputs[input].value)
					{
						if (!value || !value[0])
						{
							Logf(
							    "warning: attempted to pass null string to '%s' under argument "
							    "type %s. It will be ignored\n",
							    fileToExecute, ProcessCommandArgumentTypeToString(argument.type));
							continue;
						}

						argumentsAccumulate.push_back(value);
					}
					found = true;
					break;
				}
			}
			if (!found)
			{
				Logf("error: command to %s missing ProcessCommandInput of type %s\n", fileToExecute,
				     ProcessCommandArgumentTypeToString(argument.type));
				return nullptr;
			}
		}
	}

	int numUserArguments = argumentsAccumulate.size();
	// +1 for file to execute
	int numFinalArguments = numUserArguments + 1;
	// +1 again for the null terminator
	const char** newArguments = (const char**)calloc(sizeof(const char*), numFinalArguments + 1);

	newArguments[0] = fileToExecute;
	for (int i = 1; i < numFinalArguments; ++i)
		newArguments[i] = argumentsAccumulate[i - 1];
	newArguments[numFinalArguments] = nullptr;

	return newArguments;
}
