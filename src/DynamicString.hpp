#pragma once

#include <string> // TODO Remove

#ifdef WRAPPED_STRING
typedef struct DynamicString
{
	std::string str;
} DynamicString;
#else
typedef std::string DynamicString;
#endif

DynamicString CreateDynamicString(const char* initialValue);

void setDynamicString(DynamicString* a, const char* newValue);

char dynamicStringGetChar(const DynamicString a, unsigned int index);

// These are all pass-by-value because the string will not be copied once it's the C version

bool dynamicStringEquals(const DynamicString a, const DynamicString b);
bool dynamicStringEqualsCString(const DynamicString a, const char* b);

int dynamicStringCompare(const DynamicString a, const DynamicString b);

size_t dynamicStringSize(const DynamicString a);

bool dynamicStringIsEmpty(const DynamicString a);
void dynamicStringAppend(DynamicString* a, const char* b);
void dynamicStringAppendString(DynamicString* a, const DynamicString b);

// TODO: Reference will go away once we can safely copy these
const char* dynamicStringToCStr(const DynamicString& a);
