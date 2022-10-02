#pragma once


typedef enum ModuleDependencyType
{
	ModuleDependency_Cakelisp,
	ModuleDependency_Library,
	ModuleDependency_CFile
} ModuleDependencyType;

typedef enum CakelispImportOutput
{
	CakelispImportOutput_Header = 1 << 0,
	CakelispImportOutput_Source = 1 << 1,
	CakelispImportOutput_Both = CakelispImportOutput_Header | CakelispImportOutput_Source,
} CakelispImportOutput;
