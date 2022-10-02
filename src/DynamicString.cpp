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

size_t dynamicStringSize(const DynamicString a)
{
	return a.size();
}

bool dynamicStringIsEmpty(const DynamicString a)
{
	return a.empty();
}

void dynamicStringAppend(DynamicString* a, const char* b)
{
	a->append(b);
}

void dynamicStringAppendString(DynamicString* a, const DynamicString b)
{
	a->append(b);
}

const char* dynamicStringToCStr(const DynamicString& a)
{
	return a.c_str();
}
