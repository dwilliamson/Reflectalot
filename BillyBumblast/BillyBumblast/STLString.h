
#pragma once


#include <string>
#include <iosfwd>


namespace rfl
{
	struct Type;
}


void SerialiseSTLString(const rfl::Type* type, const void* object, std::ostream& ostream);
void DeserialiseSTLString(const rfl::Type* type, void* object, std::istream& istream);
