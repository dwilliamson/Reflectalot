
#pragma once


#include <vector>
#include <iosfwd>


namespace rfl
{
	struct Type;
}


struct STLVector : public std::vector<char>
{
	int GetCapacity(const rfl::Type* type) const;

	int GetSize(const rfl::Type* type) const;

	static void Serialise(const rfl::Type* type, const void* object, std::ostream& ostream);

	void Delete(const rfl::Type* object_type);

	void New(const rfl::Type* object_type, int size);

	static void Deserialise(const rfl::Type* type, void* object, std::istream& istream);
};