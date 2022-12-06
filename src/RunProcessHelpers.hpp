#include <string>
#include <vector>

#include "RunProcessEnums.hpp"
#include "Exporting.hpp"


//
// Helpers for programmatically constructing arguments
//

struct ProcessCommandArgument
{
	ProcessCommandArgumentType type;
	std::string contents;
};

struct ProcessCommand
{
	std::string fileToExecute;
	std::vector<ProcessCommandArgument> arguments;
};

struct ProcessCommandInput
{
	ProcessCommandArgumentType type;
	std::vector<const char*> value;
};

// The array will need to be deleted, but the array members will not
// All strings need to exist and not be moved until after you call runProcess
CAKELISP_API const char** MakeProcessArgumentsFromCommand(
    const char* fileToExecute, std::vector<ProcessCommandArgument>& arguments,
    const ProcessCommandInput* inputs, int numInputs);
