
#include "BinarySerialiser.h"
#include "Rfl.h"


void serialise::BinarySerialiseObject(const char* object, const rfl::Type* type, std::ostream& ostream)
{
	if (type->serialise_func)
	{
		type->serialise_func(type, object, ostream);
	}

	else if (type->type == rfl::TypeOf<rfl::BaseType>())
	{
		ostream.write(object, type->size);
	}

	else if (type->type == rfl::TypeOf<rfl::Class>())
	{
		serialise::BinarySerialise(object, static_cast<const rfl::Class*>(type), ostream);
	}

	else if (type->type == rfl::TypeOf<rfl::TemplateInstance>())
	{
		rfl::Type* template_type = static_cast<const rfl::TemplateInstance*>(type)->instance_of;
		if (template_type->serialise_func)
		{
			template_type->serialise_func(type, object, ostream);
		}
	}
}


void serialise::BinarySerialise(const char* object, const rfl::Class* class_type, std::ostream& ostream)
{
	const std::vector<rfl::Field>& fields = class_type->fields;
	for (size_t i = 0; i < fields.size(); i++)
	{
		const rfl::Field& field = fields[i];

		if (field.array_rank)
		{
			u32 total_array_length = field.array_length_0 * field.array_length_1;
			u32 entry_size = field.type->size;

			if (field.type->constructor == 0)
			{
				ostream.write(object + field.offset, total_array_length * entry_size);
			}
			else
			{
				for (u32 j = 0; j < total_array_length; j++)
				{
					BinarySerialiseObject(object + field.offset + j * entry_size, field.type, ostream);
				}
			}
		}
		else
		{
			BinarySerialiseObject(object + field.offset, field.type, ostream);
		}
	}
}


void serialise::BinaryDeserialiseObject(char* object, const rfl::Type* type, std::istream& istream)
{
	if (type->deserialise_func)
	{
		type->deserialise_func(type, object, istream);
	}

	else if (type->type == rfl::TypeOf<rfl::BaseType>())
	{
		istream.read(object, type->size);
	}

	else if (type->type == rfl::TypeOf<rfl::Class>())
	{
		BinaryDeserialise(object, static_cast<const rfl::Class*>(type), istream);
	}

	else if (type->type == rfl::TypeOf<rfl::TemplateInstance>())
	{
		rfl::Type* template_type = static_cast<const rfl::TemplateInstance*>(type)->instance_of;
		if (template_type->deserialise_func)
		{
			template_type->deserialise_func(type, object, istream);
		}
	}
}


void serialise::BinaryDeserialise(char* object, const rfl::Class* class_type, std::istream& istream)
{
	const std::vector<rfl::Field>& fields = class_type->fields;
	for (size_t i = 0; i < fields.size(); i++)
	{
		const rfl::Field& field = fields[i];

		if (field.array_rank)
		{
			u32 total_array_length = field.array_length_0 * field.array_length_1;
			u32 entry_size = field.type->size;

			if (field.type->constructor == 0)
			{
				istream.read(object + field.offset, total_array_length * entry_size);
			}
			else
			{
				for (u32 j = 0; j < total_array_length; j++)
				{
					BinaryDeserialiseObject(object + field.offset + j * entry_size, field.type, istream);
				}
			}
		}
		else
		{
			BinaryDeserialiseObject(object + field.offset, field.type, istream);
		}
	}
}