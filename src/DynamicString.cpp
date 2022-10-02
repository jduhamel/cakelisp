#include "DynamicString.hpp"

DynamicString CreateDynamicString(const char* optionalInitialValue)
{
	if (optionalInitialValue)
		return DynamicString(optionalInitialValue);
	return DynamicString();
}

bool dynamicStringEquals(const DynamicString a, const DynamicString b)
{
	return a.compare(b) == 0;
}

bool dynamicStringEqualsCString(const DynamicString a, const char* b)
{
	return a.compare(b) == 0;
}

int dynamicStringCompare(const DynamicString a, const DynamicString b)
{
	return a.compare(b);
}
