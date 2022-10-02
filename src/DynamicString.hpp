#pragma once
// TODO: Purge std::string
#include <string>
typedef std::string DynamicString;

DynamicString CreateDynamicString(const char* optionalInitialValue);

bool dynamicStringEquals(const DynamicString a, const DynamicString b);
bool dynamicStringEqualsCString(const DynamicString a, const char* b);

int dynamicStringCompare(const DynamicString a, const DynamicString b);

// TODO: Fix apparent unnecessary rebuilds
