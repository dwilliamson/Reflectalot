
#pragma once

#include <vector>
#include <map>
#include "Core.h"


namespace rfl
{
	struct Namespace;
	struct Type;
	struct BaseType;
	struct Class;
	struct Template;
	struct TemplateInstance;
	struct Enum;
	struct Function;
	struct Field;


	//
	// Base type for objects that allow access to their type and referencing by name
	//
	struct Object
	{
		Object() : type(0)
		{
		}

		Name name;

		// TODO: How does this type variable get initialised? Overloaded new? Wouldn't work with stack construction...
		const Type* type;
	};


	//
	// A collection of symbols that can be nested
	//
	struct Scope : public Object
	{
		Scope() : parent_scope(0)
		{
		}

		Scope* parent_scope;

		// Need to store these in the Name CRC database also as they're a unique key when looking
		// up type names
		Name full_name;

		std::vector<Namespace> namespaces;

		std::vector<BaseType> base_types;

		std::vector<Class> classes;

		std::vector<Template> templates;

		std::vector<TemplateInstance> template_instances;

		std::vector<Enum> enums;

		std::vector<Function> functions;
	};


	struct Namespace : public Scope
	{
	};


	//
	// A type can introduce scope
	//
	struct Type : public Scope
	{
		Type() : unique_id(0), size(0), typeof_va(0), serialise_func(0), deserialise_func(0)
		{
			// Again, more horrid code: This is because of the pointer patching stuff which needs to be fixed
			// Setting to -1 forces the patching to set these to null post-load
			constructor = (Function*)-1;
			destructor = (Function*)-1;
			copy_constructor = (Function*)-1;
			assignment_operator = (Function*)-1;
		}

		u32 unique_id;

		u32 size;

		u32 typeof_va;

		const Function* constructor;
		const Function* destructor;
		const Function* copy_constructor;
		const Function* assignment_operator;

		void (*serialise_func)(const Type* type, const void* object, std::ostream& ostream);
		void (*deserialise_func)(const Type* type, void* object, std::istream& istream);

		void* CreateObject() const;

		template <typename TYPE> TYPE* CreateObject() const
		{
			return (TYPE*)CreateObject();
		}
	};


	//
	// A native C++ type
	//
	struct BaseType : public Type
	{
	};


	//
	// A class/struct object type
	//
	struct Class : public Type
	{
		Class() : is_pod(false)
		{
		}

		bool is_pod;

		std::vector<Field> fields;
	};


	struct Template : public Type
	{
	};


	struct TemplateInstance : public Type
	{
		Template* instance_of;

		const Type* type0;
		const Type* type1;
	};


	//
	// An enumeration and all its possible values
	//
	struct Enum : public Type
	{
		// A name/value pair for each enum entry
		struct Entry
		{
			Name name;
			int value;
		};

		std::vector<Entry> entries;
	};


	//
	// A generalised parameter for functions, return types or data members
	//
	struct Parameter
	{
		Parameter() : type(0), is_const(false), modifier(VALUE), array_rank(0), array_length_0(1), array_length_1(1)
		{
		}

		enum Modifier
		{
			VALUE,
			POINTER,
			REFERENCE
		};

		Name name;

		const Type* type;

		bool is_const;

		// 32-bits defining the array type
		u32 array_rank : 2;
		u32 array_length_0 : 15;
		u32 array_length_1 : 15;

		Modifier modifier;
	};


	struct Field : public Parameter
	{
		Field() : offset(0)
		{
		}

		u32 offset;
	};


	struct Function
	{
		Function() : call_address(0)
		{
		}

		Name name;

		u32 call_address;

		Parameter return_parameter;

		std::vector<Parameter> parameters;

		void Call() const;
		void Call(void* object) const;
	};


	struct Module
	{
		Namespace global_namespace;
	};


	struct TemplateArg
	{
	};


	//
	// Retrieve the type object for a compile-time-specified type
	//
	template <typename TYPE> __declspec(noinline) Type* TypeOf()
	{
		// Gets automatically patched up by the Reflection Database loading code
		static Type* type = 0;
		return type;
	}


	template <typename TYPE_TO, typename TYPE_FROM> TYPE_TO* ExactCast(TYPE_FROM* object_ptr)
	{
		// First ensure the incoming type derives from Object
		Object* base_ptr = static_cast<Object*>(object_ptr);

		// Check the types match at runtime
		if (base_ptr->type != TypeOf<TYPE_TO>())
			return 0;

		return static_cast<TYPE_TO*>(object_ptr);
	}
}


//
// Force generation of copy constructor and assignment operator
//
#pragma optimize("", off)
template <typename TYPE> inline void GenerateCopyAssignmentFuncs()
{
	TYPE a(*(TYPE*)0);
	TYPE b;
	b = a;
};
#pragma optimize("", on)

//
// Can't create objects of type 'void' so specialise for this
//
template <> inline void GenerateCopyAssignmentFuncs<void>()
{
}


//
// Reflect a type - must be specified in the global namespace
//
#define RFL_REFLECT_TYPE(refl_type)											\
	__declspec(dllexport) void RflReflectedTypesTable(refl_type* arg)		\
	{																		\
		GenerateCopyAssignmentFuncs<refl_type>();							\
	}


#define RFL_REFLECT_TEMPLATE(refl_type)												\
	__declspec(dllexport) void RflReflectedTypesTable(refl_type* arg, bool is_template)		\
	{																						\
	}

#define RFL_REFLECT_TYPE_MINIMAL(refl_type)												\
	__declspec(dllexport) void RflReflectedTypesTable(refl_type* arg, bool minimal)	\
	{																					\
	}

#define MAKE_UNIQUE2(x, y) x##y
#define MAKE_UNIQUE1(x, y) MAKE_UNIQUE2(x, y)
#define MAKE_UNIQUE(x) MAKE_UNIQUE1(x, __COUNTER__)

#define typeof(type) rfl::TypeOf<type>()

// I tried using an overload of AddRflAttributes with different templated parameters, e.g. Parameter<__COUNTER__>. While this
// generated the appropriate entries, they were all stored next to each other in the PDB file's description of the class,
// making it impossible to tell which fields to apply the attributes to.
#define attr(...) static __declspec(dllexport) void MAKE_UNIQUE(SetRflAttributes__)() { __annotation(L#__VA_ARGS__); }
#define push_attr(...) static __declspec(dllexport) void MAKE_UNIQUE(PushRflAttributes__)() { __annotation(L#__VA_ARGS__); }
#define pop_attr() static __declspec(dllexport) void MAKE_UNIQUE(PopRflAttributes__)() { }
