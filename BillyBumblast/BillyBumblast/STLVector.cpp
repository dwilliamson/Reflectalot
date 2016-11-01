
#include "STLVector.h"
#include "Rfl.h"
#include "BinarySerialiser.h"


int STLVector::GetCapacity(const rfl::Type* type) const
{
	return int(_Myend - _Myfirst) / type->size;
}


int STLVector::GetSize(const rfl::Type* type) const
{
	return int(_Mylast - _Myfirst) / type->size;
}


void STLVector::Serialise(const rfl::Type* type, const void* object, std::ostream& ostream)
{
	STLVector& vec = *(STLVector*)object;

	const rfl::TemplateInstance* instance_type = static_cast<const rfl::TemplateInstance*>(type);
	const rfl::Type* object_type = instance_type->type0;

	int size = vec.GetSize(object_type);
	serialise::Write(ostream, size);

	if (object_type->constructor == 0)
	{
		if (size)
			ostream.write(vec._Myfirst, object_type->size * size);
	}

	else
	{
		for (int i = 0; i < size; i++)
			serialise::BinarySerialiseObject(vec._Myfirst + i * object_type->size, object_type, ostream);
	}
}


void STLVector::Delete(const rfl::Type* object_type)
{
	if (_Myfirst)
	{
		// Destruct each object if needed
		if (object_type->destructor)
		{
			int size = GetSize(object_type);
			for (int i = 0; i < size; i++)
				object_type->destructor->Call(_Myfirst + i * object_type->size);
		}

		delete _Myfirst;
		_Myfirst = 0;
		_Mylast = 0;
		_Myend = 0;
	}
}


void STLVector::New(const rfl::Type* object_type, int size)
{
	int data_size = object_type->size * size;
	if (data_size)
	{
		_Myfirst = new char[data_size];
		_Mylast = _Myfirst + data_size;
		_Myend = _Myfirst + data_size;

		// Construct each new object if needed
		if (object_type->constructor)
		{
			for (int i = 0; i < size; i++)
				object_type->constructor->Call(_Myfirst + i * object_type->size);
		}
	}
}

void STLVector::Deserialise(const rfl::Type* type, void* object, std::istream& istream)
{
	STLVector& vec = *(STLVector*)object;

	const rfl::TemplateInstance* instance_type = static_cast<const rfl::TemplateInstance*>(type);
	const rfl::Type* object_type = instance_type->type0;

	// When deserialising to a vector, delete the old one before starting anew
	int size = serialise::Read<int>(istream);
	vec.Delete(object_type);
	vec.New(object_type, size);

	if (object_type->constructor == 0 && size)
	{
		istream.read(vec._Myfirst, object_type->size * size);
	}

	else
	{
		for (int i = 0; i < size; i++)
			serialise::BinaryDeserialiseObject(vec._Myfirst + i * object_type->size, object_type, istream);
	}
}
