#pragma once
// TODO: Purge std::string
#include <string>
typedef std::string DynamicString;

DynamicString CreateDynamicString(const char* optionalInitialValue);

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
