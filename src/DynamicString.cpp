#include "DynamicString.hpp"

#ifdef WRAPPED_STRING
DynamicString CreateDynamicString(const char* initialValue)
{
	DynamicString newString;
	newString.str = initialValue;
	return newString;
}

void setDynamicString(DynamicString* a, const char* newValue)
{
	a->str = newValue;
}

char dynamicStringGetChar(const DynamicString a, unsigned int index)
{
	return a.str[index];
}

bool dynamicStringEquals(const DynamicString a, const DynamicString b)
{
	return a.str.compare(b) == 0;
}

bool dynamicStringEqualsCString(const DynamicString a, const char* b)
{
	return a.str.compare(b) == 0;
}

int dynamicStringCompare(const DynamicString a, const DynamicString b)
{
	return a.str.compare(b);
}

size_t dynamicStringSize(const DynamicString a)
{
	return a.str.size();
}

bool dynamicStringIsEmpty(const DynamicString a)
{
	return a.str.empty();
}

void dynamicStringAppend(DynamicString* a, const char* b)
{
	a->str.append(b);
}

void dynamicStringAppendString(DynamicString* a, const DynamicString b)
{
	a->str.append(b);
}

const char* dynamicStringToCStr(const DynamicString& a)
{
	return a.str.c_str();
}

#else

DynamicString CreateDynamicString(const char* initialValue)
{
	DynamicString newString(initialValue);
	return newString;
}

void setDynamicString(DynamicString* a, const char* newValue)
{
	*a = newValue;
}

char dynamicStringGetChar(const DynamicString a, unsigned int index)
{
	return a[index];
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
#endif
