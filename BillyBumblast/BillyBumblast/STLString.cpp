
#include "STLString.h"
#include "BinarySerialiser.h"


void SerialiseSTLString(const rfl::Type* type, const void* object, std::ostream& ostream)
{
	const std::string& str = *(std::string*)object;
	int length = (int)str.length();
	serialise::Write(ostream, length);
	ostream.write(str.c_str(), length);
}


void DeserialiseSTLString(const rfl::Type* type, void* object, std::istream& istream)
{
	std::string& str = *(std::string*)object;
	int length = serialise::Read<int>(istream);
	str.resize(length);
	// NOTE: Naughty const-cast
	istream.read((char*)str.data(), length);
}
