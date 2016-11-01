
#include "RflXmlDbReader.h"
#include "Rfl.h"
#include "Win32.h"
#include "tinyxml.h"

using namespace rfl;


namespace
{
	typedef std::map<u32, Type*> TypeMap;


	void ParseNamespace(TiXmlNode* node, Namespace& ns, Scope* parent_scope);
	void ParseBaseType(TiXmlNode* node, BaseType& type, Scope* parent_scope);
	void ParseClass(TiXmlNode* node, Class& cls, Scope* parent_scope);
	void ParseTemplate(TiXmlNode* node, Template& templ, Scope* parent_scope);
	void ParseTemplateInstance(TiXmlNode* node, TemplateInstance& instance, Scope* parent_scope);
	void ParseFunction(TiXmlNode* node, Function& function, Scope*);
	void ParseEnum(TiXmlNode* node, Enum& enm, Scope* parent_scope);
	void PopulateTypeMapScope(TypeMap& type_map, Scope& scope);
	void PatchTypePointersScope(TypeMap& type_map, Scope& scope);


	TiXmlElement* GetElement(TiXmlNode* node, const char* element_name)
	{
		// Get the first child as an element
		TiXmlNode* element_node = node->FirstChild(element_name);
		if (element_node)
			return element_node->ToElement();

		return 0;
	}


	void ParseName(TiXmlNode* node, const char* element_name, Name& name)
	{
		TiXmlNode* element_node = node->FirstChild(element_name);
		if (element_node)
		{
			TiXmlElement* element = element_node->ToElement();
			name.string = element->Attribute("str");
			name.hash_id = (u32)_atoi64(element->GetText());		// Hash id is 32-bits unsigned, which is out of the range of atoi
		}
	}


	const char* ParseText(TiXmlNode* node, const char* element_name)
	{
		if (TiXmlElement* element = GetElement(node, element_name))
			return element->GetText();

		return 0;
	}


	template <typename TYPE> void ParseInteger(TiXmlNode* node, const char* element_name, TYPE& value)
	{
		if (TiXmlElement* element = GetElement(node, element_name))
			value = TYPE(_atoi64(element->GetText()));
	}


	bool ParseBool(TiXmlNode* node, const char* element_name)
	{
		if (TiXmlElement* element = GetElement(node, element_name))
		{
			const char* text = element->GetText();
			return !_stricmp(text, "true");
		}

		return false;
	}


	template <typename TYPE> void ParseCollection(
		TiXmlNode* root_node,
		std::vector<TYPE>& collection,
		Scope* parent_scope,
		const char* collection_name,
		const char* entry_name,
		void (*parse_func)(TiXmlNode*, TYPE&, Scope*))
	{
		// Search for the collection
		if (TiXmlNode* node = root_node->FirstChild(collection_name))
		{
			// Iterate over every entry
			TiXmlNode* child_node = node->FirstChild(entry_name);
			while (child_node)
			{
				// Allocate a new object in the array and parse it
				collection.push_back(TYPE());
				TYPE& object = collection.back();
				parse_func(child_node, object, parent_scope);

				child_node = child_node->NextSibling(entry_name);
			}
		}
	}





	Type* MakePatchableTypePtr(const char* type_name)
	{
		u32 hash_id = Name(type_name).hash_id;
		return (Type*)(u64)hash_id;
	}


	Type* ParseType(TiXmlNode* node, const char* name)
	{
		// Temporarily store the hash ID in the type pointer, to be patched later
		Name type_name;
		ParseName(node, name, type_name);
		return (Type*)(u64)type_name.hash_id;
	}


	void ParseParameter(TiXmlNode* node, Parameter& param, Scope* parent_scope)
	{
		ParseName(node, "Name", param.name);
		param.type = ParseType(node, "Type");
		param.is_const = ParseBool(node, "IsConst");

		// Manual bit-field assignment for the array info
		if (TiXmlElement* element = GetElement(node, "ArrayRank"))
			param.array_rank = int(_atoi64(element->GetText()));
		if (TiXmlElement* element = GetElement(node, "ArrayLength0"))
			param.array_length_0 = int(_atoi64(element->GetText()));
		if (TiXmlElement* element = GetElement(node, "ArrayLength1"))
			param.array_length_1 = int(_atoi64(element->GetText()));

		// Map the modifier enumeration
		if (const char* modifier = ParseText(node ,"Modifier"))
		{
			if (!_stricmp(modifier, "value"))
				param.modifier = Parameter::VALUE;
			else if (!_stricmp(modifier, "pointer"))
				param.modifier = Parameter::POINTER;
			else if (!_stricmp(modifier, "reference"))
				param.modifier = Parameter::REFERENCE;
		}
	}


	void ParseField(TiXmlNode* node, Field& field, Scope* parent_scope)
	{
		ParseParameter(node, field, parent_scope);

		ParseInteger(node, "Offset", field.offset);
	}


	void ParseFunction(TiXmlNode* node, Function& function, Scope*)
	{
		ParseName(node, "Name", function.name);
		ParseInteger(node, "CallAddress", function.call_address);

		// Functions could inherit from Scope, making the Scope parameter mean something here
		// Note that functions can also introduce new types, enums, etc.

		if (TiXmlNode* param_node = node->FirstChild("ReturnParameter"))
			ParseParameter(param_node, function.return_parameter, 0);

		ParseCollection<Parameter>(node, function.parameters, 0, "Parameters", "Parameter", ParseParameter);
	}


	void ParseScope(TiXmlNode* node, Scope& scope, Scope* parent_scope)
	{
		scope.parent_scope = parent_scope;
		ParseName(node, "Name", scope.name);
		ParseName(node, "FullName", scope.full_name);

		ParseCollection<Namespace>(node, scope.namespaces, &scope, "Namespaces", "Namespace", ParseNamespace);
		ParseCollection<BaseType>(node, scope.base_types, &scope, "BaseTypes", "BaseType", ParseBaseType);
		ParseCollection<Class>(node, scope.classes, &scope, "Classes", "Class", ParseClass);
		ParseCollection<Template>(node, scope.templates, &scope, "Templates", "Template", ParseTemplate);
		ParseCollection<TemplateInstance>(node, scope.template_instances, &scope, "TemplateInstances", "TemplateInstance", ParseTemplateInstance);
		ParseCollection<Enum>(node, scope.enums, &scope, "Enums", "Enum", ParseEnum);
		ParseCollection<Function>(node, scope.functions, &scope, "Functions", "Function", ParseFunction);
	}


	void ParseNamespace(TiXmlNode* node, Namespace& ns, Scope* parent_scope)
	{
		ns.type = MakePatchableTypePtr("rfl::Namespace");
		ParseScope(node, ns, parent_scope);
	}


	void ParseType(TiXmlNode* node, Type& type, Scope* parent_scope)
	{
		ParseInteger(node, "UniqueID", type.unique_id);
		ParseInteger(node, "Size", type.size);
		ParseInteger(node, "TypeOfVA", type.typeof_va);

		ParseScope(node, type, parent_scope);

		// After the scope has collected the functions
		ParseInteger(node, "ConstructorIndex", type.constructor);
		ParseInteger(node, "DestructorIndex", type.destructor);
		ParseInteger(node, "CopyConstructorIndex", type.copy_constructor);
		ParseInteger(node, "AssignmentOperatorIndex", type.assignment_operator);
	}


	void ParseBaseType(TiXmlNode* node, BaseType& type, Scope* parent_scope)
	{
		type.type = MakePatchableTypePtr("rfl::BaseType");
		ParseType(node, type, parent_scope);
	}


	void ParseClass(TiXmlNode* node, Class& cls, Scope* parent_scope)
	{
		cls.type = MakePatchableTypePtr("rfl::Class");
		cls.is_pod = ParseBool(node, "IsPOD");

		ParseCollection<Field>(node, cls.fields, &cls, "Fields", "Field", ParseField);

		ParseType(node, cls, parent_scope);
	}


	void ParseTemplate(TiXmlNode* node, Template& templ, Scope* parent_scope)
	{
		templ.type = MakePatchableTypePtr("rfl::Template");
		ParseType(node, templ, parent_scope);
	}


	void ParseTemplateInstance(TiXmlNode* node, TemplateInstance& instance, Scope* parent_scope)
	{
		instance.type = MakePatchableTypePtr("rfl::TemplateInstance");
		instance.instance_of = (Template*)ParseType(node, "InstanceOf");
		instance.type0 = ParseType(node, "Type0");
		instance.type1 = ParseType(node, "Type1");

		ParseType(node, instance, parent_scope);
	}


	void ParseEnumEntry(TiXmlNode* node, Enum::Entry& entry, Scope* parent_scope)
	{
		ParseName(node, "Name", entry.name);
		ParseInteger(node, "Value", entry.value);
	}


	void ParseEnum(TiXmlNode* node, Enum& enm, Scope* parent_scope)
	{
		enm.type = MakePatchableTypePtr("rfl::Enum");
		ParseCollection(node, enm.entries, &enm, "Entries", "Entry", ParseEnumEntry);
		ParseType(node, enm, parent_scope);
	}


	// These functions search through the loaded database looking for type definitions and adding
	// them to the type map, using the type name hash ID as the key. This is done as a post-process
	// as resizing a vector changes the address of the type objects.
	// TODO: This is not ideal - need to come up with a better ownership strategy that's not too
	// memory inefficient.


	void PopulateTypeMapType(TypeMap& type_map, Type& type)
	{
		type_map[type.full_name.hash_id] = &type;

		PopulateTypeMapScope(type_map, type);
	}


	template <typename COLLECTION, typename FUNCTION> void PopulateTypeMapCollection(TypeMap& type_map, COLLECTION& collection, FUNCTION function)
	{
		for (size_t i = 0; i < collection.size(); i++)
			function(type_map, collection[i]);
	}


	void PopulateTypeMapScope(TypeMap& type_map, Scope& scope)
	{
		PopulateTypeMapCollection(type_map, scope.base_types, PopulateTypeMapType);
		PopulateTypeMapCollection(type_map, scope.classes, PopulateTypeMapType);
		PopulateTypeMapCollection(type_map, scope.templates, PopulateTypeMapType);
		PopulateTypeMapCollection(type_map, scope.template_instances, PopulateTypeMapType);
		PopulateTypeMapCollection(type_map, scope.enums, PopulateTypeMapType);
		PopulateTypeMapCollection(type_map, scope.namespaces, PopulateTypeMapScope);
	}


	// These functions use a constructed type map to map from the type name hash ID to the final type pointer. Even if the types weren't
	// stored by value in vectors this would be necessary, as it resolves circular references between types.


	template <typename TYPE> void PatchPointer(TypeMap& type_map, TYPE*& type)
	{
		if (type)
			type = (TYPE*)type_map[(u32&)type];
	}


	void PatchTypePointersParameter(TypeMap& type_map, Parameter& parameter)
	{
		PatchPointer(type_map, parameter.type);
	}

	void PatchTypePointersBaseType(TypeMap& type_map, BaseType& base_type)
	{
		PatchPointer(type_map, base_type.type);
	}

	void PatchTypePointersTemplate(TypeMap& type_map, Template& templ)
	{
		PatchPointer(type_map, templ.type);
	}

	void PatchTypePointersTemplateInstance(TypeMap& type_map, TemplateInstance& instance)
	{
		PatchPointer(type_map, instance.type);
		PatchPointer(type_map, instance.instance_of);
		PatchPointer(type_map, instance.type0);
		PatchPointer(type_map, instance.type1);
	}

	void PatchTypePointersEnum(TypeMap& type_map, Enum& enum_type)
	{
		PatchPointer(type_map, enum_type.type);
	}

	template <typename COLLECTION, typename FUNCTION> void PatchTypePointersCollection(TypeMap& type_map, COLLECTION& collection, FUNCTION function)
	{
		for (size_t i = 0; i < collection.size(); i++)
			function(type_map, collection[i]);
	}


	void PatchTypePointersFunction(TypeMap& type_map, Function& function)
	{
		PatchTypePointersParameter(type_map, function.return_parameter);
		PatchTypePointersCollection(type_map, function.parameters, PatchTypePointersParameter);
	}


	void PatchTypePointersClass(TypeMap& type_map, Class& cls)
	{
		PatchTypePointersCollection(type_map, cls.fields, PatchTypePointersParameter);
		PatchTypePointersScope(type_map, cls);
	}


	void PatchTypePointersScope(TypeMap& type_map, Scope& scope)
	{
		scope.type = type_map[(u32&)scope.type];

		PatchTypePointersCollection(type_map, scope.base_types, PatchTypePointersBaseType);
		PatchTypePointersCollection(type_map, scope.classes, PatchTypePointersClass);
		PatchTypePointersCollection(type_map, scope.templates, PatchTypePointersTemplate);
		PatchTypePointersCollection(type_map, scope.template_instances, PatchTypePointersTemplateInstance);
		PatchTypePointersCollection(type_map, scope.enums, PatchTypePointersEnum);
		PatchTypePointersCollection(type_map, scope.functions, PatchTypePointersFunction);
		PatchTypePointersCollection(type_map, scope.namespaces, PatchTypePointersScope);
	}


	void UpdateFunctionPtr(const Function*& function, const Type* type)
	{
		// This is a nasty bit of code
		// Have to get function pointers last as everything is stored by-value in vectors
		// BAD IDEA to store pointers to objects in a vector and this entire file exhibits this issue

		int& index = (int&)function;
		if (index == -1)
			function = 0;
		else
			function = &type->functions[index];
	}


	void UpdateModulePointers(TypeMap& type_map)
	{
		u64 base_address = Win32::GetProgramBaseAddress();

		for (TypeMap::iterator i = type_map.begin(); i != type_map.end(); ++i)
		{
			Type* type = i->second;

			// Only patch types which have been requested in source code
			if (type->typeof_va)
			{
				// Figure out where the Type* pointer is in memory and update it
				u64 offset = type->typeof_va + base_address;
				Type** type_ptr = (Type**)offset;
				*type_ptr = type;
			}

			UpdateFunctionPtr(type->constructor, type);
			UpdateFunctionPtr(type->destructor, type);
			UpdateFunctionPtr(type->copy_constructor, type);
			UpdateFunctionPtr(type->assignment_operator, type);

			// for matching
		}
	}
}


Module* XmlDbReader::LoadModule(const char* xml_file)
{
	// Try to open the document
	TiXmlDocument xml_doc;
	if (!xml_doc.LoadFile(xml_file))
		return 0;

	// Search for the global namespace
	if (TiXmlNode* root_node = xml_doc.FirstChild("RflDb"))
	{
		if (TiXmlNode* global_ns_node = root_node->FirstChild("Namespace"))
		{
			// Parse the file
			Module* module = new Module;
			ParseNamespace(global_ns_node, module->global_namespace, 0);

			// Populate type map after all collections have been finalised
			TypeMap type_map;
			PopulateTypeMapScope(type_map, module->global_namespace);
			PatchTypePointersScope(type_map, module->global_namespace);
			UpdateModulePointers(type_map);

			return module;
		}
	}

	return 0;
}