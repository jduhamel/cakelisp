#pragma once

typedef enum WriterFormatBraceStyle
{
	// See https://en.wikipedia.org/wiki/Indentation_style
	WriterFormatBraceStyle_Allman,
	WriterFormatBraceStyle_KandR_1TBS
} WriterFormatBraceStyle;

typedef enum WriterFormatIndentType
{
	WriterFormatIndentType_Tabs,
	WriterFormatIndentType_Spaces
} WriterFormatIndentType;
