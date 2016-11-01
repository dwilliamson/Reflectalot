
#include "Rfl.h"
#include "Win32.h"
#include "tinyxml.h"
#include <cstdio>

using namespace rfl;


void* Type::CreateObject() const
{
	char* data = new char[size];
	if (constructor)
		constructor->Call(data);
	return data;
}


void Function::Call() const
{
	u64 base_address = Win32::GetProgramBaseAddress();
	u32 faddress = u32(base_address + call_address);
	__asm call faddress
}


void Function::Call(void* object) const
{
	// thiscall assumed
	u64 base_address = Win32::GetProgramBaseAddress();
	u32 faddress = u32(base_address + call_address);
	__asm
	{
		mov ecx, object
		call faddress
	}
}


// Reflect all native C++ types
RFL_REFLECT_TYPE(void);
RFL_REFLECT_TYPE(bool);
RFL_REFLECT_TYPE(char);
RFL_REFLECT_TYPE(short);
RFL_REFLECT_TYPE(int);
RFL_REFLECT_TYPE(long);
RFL_REFLECT_TYPE(__int64);
RFL_REFLECT_TYPE(unsigned char);
RFL_REFLECT_TYPE(unsigned short);
RFL_REFLECT_TYPE(unsigned int);
RFL_REFLECT_TYPE(unsigned long);
RFL_REFLECT_TYPE(unsigned __int64);
RFL_REFLECT_TYPE(float);
RFL_REFLECT_TYPE(double);

RFL_REFLECT_TYPE(Name);

// Reflect all reflection types
RFL_REFLECT_TYPE(rfl::Scope);
RFL_REFLECT_TYPE(rfl::Namespace);
RFL_REFLECT_TYPE(rfl::Type);
RFL_REFLECT_TYPE(rfl::BaseType);
RFL_REFLECT_TYPE(rfl::Class);
RFL_REFLECT_TYPE(rfl::Template);
RFL_REFLECT_TYPE(rfl::TemplateInstance);
RFL_REFLECT_TYPE(rfl::Enum);
RFL_REFLECT_TYPE(rfl::Parameter);
RFL_REFLECT_TYPE(rfl::Field);
RFL_REFLECT_TYPE(rfl::Function);
RFL_REFLECT_TYPE(rfl::Module);
