
#pragma once


#include <iosfwd>


namespace rfl
{
	struct Type;
	struct Class;
};


namespace serialise
{
	void BinarySerialise(const char* object, const rfl::Class* class_type, std::ostream& ostream);
	void BinarySerialiseObject(const char* object, const rfl::Type* type, std::ostream& ostream);
	void BinaryDeserialise(char* object, const rfl::Class* class_type, std::istream& istream);
	void BinaryDeserialiseObject(char* object, const rfl::Type* type, std::istream& istream);


	template <typename TYPE> void BinarySerialise(const TYPE& object, std::ostream& ostream)
	{
		rfl::Type* type = rfl::TypeOf<TYPE>();
		rfl::Class* class_type = rfl::ExactCast<rfl::Class>(type);
		BinarySerialise((const char*)&object, class_type, ostream);
	}


	template <typename TYPE> void BinaryDeserialise(TYPE& object, std::istream& istream)
	{
		rfl::Type* type = rfl::TypeOf<TYPE>();
		rfl::Class* class_type = rfl::ExactCast<rfl::Class>(type);
		BinaryDeserialise((char*)&object, class_type, istream);
	}

	// TEMP: Should these be here?

	template <typename TYPE> TYPE Read(std::istream& istream)
	{
		TYPE temp;
		istream.read((char*)&temp, sizeof(temp));
		return temp;
	}


	template <typename TYPE> void Write(std::ostream& ostream, const TYPE& value)
	{
		ostream.write((char*)&value, sizeof(value));
	}

}