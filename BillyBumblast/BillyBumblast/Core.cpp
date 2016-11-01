
#include "Core.h"
#include "MurmurHash2.h"


Name::Name(const char* name) : string(name)
{
	hash_id = MurmurHash2(name, (int)strlen(name), 0xFEEDB00D);
}