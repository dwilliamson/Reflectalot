
#pragma once

#include <string>


typedef unsigned int u32;
typedef unsigned __int64 u64;


struct Name
{
	Name() : hash_id(0)
	{
	}

	Name(const char* name);

	// TODO: Remove from runtime
	std::string string;

	u32 hash_id;
};

