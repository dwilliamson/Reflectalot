using System;
using System.Collections.Generic;
using System.Text;
using Dia2Lib;
using System.Text.RegularExpressions;
using System.IO;

namespace Rfl
{
    //
    // From the DIA SDK
    // http://msdn.microsoft.com/en-us/library/4szdtzc3(VS.80).aspx
    //
    enum BasicType
    {
        btNoType    = 0,
        btVoid      = 1,
        btChar      = 2,
        btWChar     = 3,
        btInt       = 6,
        btUInt      = 7,
        btFloat     = 8,
        btBCD       = 9,
        btBool      = 10,
        btLong      = 13,
        btULong     = 14,
        btCurrency  = 25,
        btDate      = 26,
        btVariant   = 27,
        btComplex   = 28,
        btBit       = 29,
        btBSTR      = 30,
        btHresult   = 31
    }

    //
    // From the DIA SDK
    // http://msdn.microsoft.com/en-us/library/b2x2t313.aspx
    //
    enum DataKind
    {
        DataIsUnknown,                      // Data symbol cannot be determined
        DataIsLocal,                        // Data item is a local variable
        DataIsStaticLocal,                  // Data item is a static local variable
        DataIsParam,                        // Data item is a formal parameter
        DataIsObjectPtr,                    // Data item is an object pointer (this)
        DataIsFileStatic,                   // Data item is a file-scoped variable
        DataIsGlobal,                       // Data item is a global variable
        DataIsMember,                       // Data item is an object member variable
        DataIsStaticMember,                 // Data item is a class static variable
        DataIsConstant                      // Data item is a constant value
    }

    public class PDBLoader
    {
        public Module Load(string filename)
        {
            m_Logger = new Logger(Path.ChangeExtension(filename, ".rfl_log"));

            // Create a DIA session from the loaded PDB file
            m_DataSource = new DiaSourceClass();
            m_DataSource.loadDataFromPdb(filename);
            m_DataSource.openSession(out m_Session);

            Module module = new Module();
            LoadReflectedTypes(module);
            ReflectClasses(module.GlobalNamespace);

            m_Logger.Flush();

            return module;
        }

        private void ReflectClasses(Scope scope)
        {
            foreach (Class cls in scope.Classes)
            {
                m_Logger.WriteSection("Reflecting Class - {" + cls.FullName.String + "}");

                // Enumerate all class members
                IDiaSymbol dia_class_symbol = m_DiaSymbolMap[cls];
                IDiaEnumSymbols enum_class_members;
                dia_class_symbol.findChildren(SymTagEnum.SymTagNull, null, 0, out enum_class_members);

                // Need to enumerate them in the order they are defined so that attributes get attached
                // to the members next to them
                foreach (IDiaSymbol member_symbol in enum_class_members)
                {
                    switch ((SymTagEnum)member_symbol.symTag)
                    {
                        case SymTagEnum.SymTagEnum:
                            ReflectClassEnum(cls, member_symbol);
                            break;

                        case SymTagEnum.SymTagUDT:
                            ReflectClassClass(cls, member_symbol);
                            break;

                        case SymTagEnum.SymTagData:
                            ReflectClassField(cls, member_symbol);
                            break;

                        case SymTagEnum.SymTagFunction:
                            ReflectClassMethod(cls, member_symbol);
                            break;
                    }
                }

                m_AttributeParser.PopAllAttributes();

                m_Logger.EndSection();
            }

            // Reflect classes within nested namespaces
            Namespace ns = scope as Namespace;
            if (ns != null)
            {
                foreach (Namespace nested_ns in ns.Namespaces)
                    ReflectClasses(nested_ns);
            }
        }

        private void ReflectClassEnum(Class cls, IDiaSymbol member_symbol)
        {
            if (cls.MinimalReflection)
                return;

            string full_type_name = cls.FullName.String + "::" + member_symbol.name;

            // Merge already existing unnamed enumerations
            Rfl.Type enum_type = null;
            if (member_symbol.name != "<unnamed-tag>" || m_TypeMap.TryGetValue(full_type_name, out enum_type) == false)
            {
                enum_type = new Rfl.Enum(
                    cls,
                    member_symbol.name,
                    (uint)member_symbol.length,
                    TypeOfVirtualAddress("enum " + full_type_name));

                m_TypeMap.Add(full_type_name, enum_type);
                m_DiaSymbolMap.Add(enum_type, member_symbol);
                cls.Enums.Add(enum_type as Rfl.Enum);
            }

            LoadEnumEntries(enum_type as Rfl.Enum, member_symbol);
        }

        private void ReflectClassClass(Class cls, IDiaSymbol member_symbol)
        {
            if (cls.MinimalReflection)
                return;

            string full_type_name = cls.FullName.String + "::" + member_symbol.name;

            Rfl.Class class_type = new Rfl.Class(
                cls,
                member_symbol.name,
                (uint)member_symbol.length,
                TypeOfVirtualAddress(full_type_name),
                member_symbol.constructor == 0);

            m_TypeMap.Add(full_type_name, class_type);
            m_DiaSymbolMap.Add(class_type, member_symbol);
            cls.Classes.Add(class_type);

            ReflectClasses(cls);
        }

        private void ReflectClassField(Class cls, IDiaSymbol member_symbol)
        {
            if (cls.MinimalReflection || member_symbol.dataKind != (uint)DataKind.DataIsMember)
                return;

            m_Logger.WriteSection("Field - " + member_symbol.name);

            List<Attribute> attributes = GetAttributes();

            Parameter parameter = ReflectParameter(member_symbol);
            if (parameter != null)
            {
                // Construct a field from the parameter
                Field field = new Field(parameter, member_symbol.offset);
                field.Attributes = attributes;
                cls.Fields.Add(field);
            }
            else
            {
                m_Logger.WriteWarning("Parameter not reflected due to unknown type");
            }

            m_Logger.EndSection();
        }

        private void ReflectClassMethods(Type cls)
        {
            // Enumerate all member functions
            IDiaSymbol dia_type = m_DiaSymbolMap[cls];
            IDiaEnumSymbols enum_data;
            dia_type.findChildren(SymTagEnum.SymTagFunction, null, 0, out enum_data);

            foreach (IDiaSymbol dia_func in enum_data)
            {
                ReflectClassMethod(cls, dia_func);
            }
        }

        private string GetFirstAnnotationString(IDiaSymbol parent_symbol)
        {
            // Get the first annotation
            IDiaEnumSymbols enum_annotations;
            parent_symbol.findChildren(SymTagEnum.SymTagAnnotation, null, 0, out enum_annotations);
            if (enum_annotations.count == 0)
                return "";
            IDiaSymbol annotation = enum_annotations.Item(0);

            // Get the first string in this annotation
            IDiaEnumSymbols enum_strings;
            annotation.findChildren(SymTagEnum.SymTagData, null, 0, out enum_strings);
            if (enum_strings.count == 0)
                return "";
            IDiaSymbol str = enum_strings.Item(0);

            return str.value.ToString();
        }

        private void ReflectClassMethod(Type cls, IDiaSymbol member_symbol)
        {
            // Skip inlined/optimised-out functions
            if (member_symbol.virtualAddress == 0)
                return;

            // Strip scope information from the function name
            string func_name = member_symbol.name.Replace(cls.FullName.String + "::", "");

            if (!cls.MinimalReflection && func_name.StartsWith("PushRflAttributes__"))
            {
                m_Logger.WriteSection("Push Attributes");

                string input_string = GetFirstAnnotationString(member_symbol);
                m_Logger.Write(input_string);
                m_AttributeParser.PushAttributes(input_string);

                m_Logger.EndSection();
                return;
            }

            if (!cls.MinimalReflection && func_name.StartsWith("SetRflAttributes__"))
            {
                m_Logger.WriteSection("Set Attributes");

                if (m_AttributesBeingSet == false)
                {
                    string input_string = GetFirstAnnotationString(member_symbol);
                    m_Logger.Write(input_string);
                    m_AttributeParser.PushAttributes(input_string);
                    m_AttributesBeingSet = true;
                }
                else
                {
                    m_Logger.WriteWarning("Attributes can't be set more than once");
                }

                m_Logger.EndSection();
                return;
            }

            if (!cls.MinimalReflection && func_name.StartsWith("PopRflAttributes__"))
            {
                m_Logger.Write("Pop Attributes");
                m_AttributeParser.PopAttributes();
                return;
            }

            Function function = new Function(
                new Name(func_name),
                (uint)member_symbol.virtualAddress);

            // Enumerate all parameters
            IDiaEnumSymbols enum_params;
            member_symbol.findChildren(SymTagEnum.SymTagData, null, 0, out enum_params);
            bool param_reflect = true;
            foreach (IDiaSymbol dia_param in enum_params)
            {
                // Only want parameters and 'this'
                if (dia_param.dataKind != (uint)DataKind.DataIsParam && dia_param.dataKind != (uint)DataKind.DataIsObjectPtr)
                    continue;

                // Try to reflect and add to the list
                Parameter parameter = ReflectParameter(dia_param);
                if (parameter != null)
                {
                    function.Parameters.Add(parameter);
                }
                else
                {
                    param_reflect = false;
                    break;
                }
            }

            // Reflect the return type
            Parameter return_param = ReflectParameter(member_symbol.type);
            if (return_param != null && param_reflect)
            {
                function.ReturnParameter = return_param;

                bool is_class_function = AssignClassFunction(cls, function);
                if (!cls.MinimalReflection || is_class_function)
                {
                    m_Logger.Write("Method - " + function.Name.String);
                    cls.Functions.Add(function);
                }
            }

            else if (!cls.MinimalReflection)
            {
                m_Logger.WriteWarning("Method not reflected due to missing parameter type - " + function.Name.String);
            }
        }

        private List<Attribute> GetAttributes()
        {
            List<Attribute> attributes = m_AttributeParser.GetAttributes();

            // Ensure single-shot attributes are cleared out after use
            if (m_AttributesBeingSet)
            {
                m_AttributesBeingSet = false;
                m_AttributeParser.PopAttributes();
            }

            return attributes;
        }

        private bool AssignClassFunction(Type cls, Function function)
        {
            int index = cls.Functions.Count;
            string fname = function.Name.String;
            List<Parameter> param = function.Parameters;

            // Check for constructors
            if (function.Name.Id == cls.Name.Id)
            {
                if (param.Count == 1 && param[0].Type == cls)
                {
                    cls.ConstructorIndex = index;
                    return true;
                }
                else if (param.Count == 2 && param[0].Type == cls && param[1].Type == cls)
                {
                    cls.CopyConstructorIndex = index;
                    return true;
                }
            }

            // Check for destructor
            else if (fname[0] == '~' && fname.Substring(1) == cls.Name.String)
            {
                cls.DestructorIndex = index;
                return true;
            }

            // Check for assignment operator
            else if (fname.EndsWith("operator=") && param.Count == 2 && param[1].Type == cls)
            {
                cls.AssignmentOperatorIndex = index;
                return true;
            }

            return false;
        }

        Parameter ReflectParameter(IDiaSymbol dia_param)
        {
            // Decode array information
            IDiaSymbol type = dia_param.type;
            int array_length_0 = 1, array_length_1 = 1;
            int array_rank = GetArrayRank(ref type, ref array_length_0, ref array_length_1);

            // Check rank and size
            if (array_rank > 2 || array_length_0 > (1 << 15) || array_length_1 > (1 << 15))
            {
                // NOTE: No reason is given here for the failure
                return null;
            }

            // Decode the DIA type modifier and name
            TypeModifier modifier = GetTypeModifier(ref type);
            string type_name = GetTypeName(type);

            // Is this a template type?
            int template_index = type_name.IndexOf('<');
            if (template_index != -1)
            {
                // Has the template been reflected?
                string template_name = type_name.Remove(template_index);
                Rfl.Type template_type = null;
                if (m_TypeMap.TryGetValue(template_name, out template_type) && template_type is Rfl.Template)
                {
                    // Get the template instance parameters
                    string[] template_params = Rfl.Template.ParseInstance(type_name, template_index);
                    Rfl.Type type0;
                    m_TypeMap.TryGetValue(template_params[0], out type0);
                    Rfl.Type type1;
                    m_TypeMap.TryGetValue(template_params[1], out type1);

                    // If the first type can't be determined, this isn't a valid template instance
                    if (type0 == null)
                    {
                        return null;
                    }

                    // If the template instance doesn't exist, create it
                    Rfl.Type instance;
                    if (!m_TypeMap.TryGetValue(type_name, out instance))
                    {
                        // Strip scope from instance name
                        string parent_scope = template_type.ParentScope.FullName.String;
                        string instance_name = type_name.Substring(parent_scope.Length + 2);

                        //
                        // Assign the virtual address for the type-of function. Note that if the template parameter
                        // list is not fully matched (e.g. std::vector<int, std::allocator<int> > where the second parameter
                        // doesn't get matched), this will only record the address for the first instance that is parsed.
                        // This may or may not be a problem and will require a little more thought if a proper solution
                        // is required (e.g. a list of virtual addresses for all combinations).
                        //
                        instance = new Rfl.TemplateInstance(
                            template_type.ParentScope,
                            instance_name,
                            (uint)type.length,
                            TypeOfVirtualAddress(type_name),
                            template_type as Rfl.Template,
                            type0,
                            type1);

                        m_TypeMap.Add(type_name, instance);
                        m_DiaSymbolMap.Add(instance, type);
                        instance.ParentScope.TemplateInstances.Add(instance as Rfl.TemplateInstance);

                        m_Logger.WriteSection("Reflecting TemplateInstance - {" + instance.FullName.String + "}");

                        // Reflect the constructors, etc - minimal reflection is set to true for template instances
                        ReflectClassMethods(instance);

                        m_Logger.EndSection();
                    }
                }
            }

            // Has this type been reflected?
            Rfl.Type rfl_type = null;
            if (m_TypeMap.TryGetValue(type_name, out rfl_type))
            {
                // Some parameters won't have a name (e.g. return parameter)
                Name name = null;
                if (dia_param.name != null)
                    name = new Name(dia_param.name);

                return new Parameter(
                    name,
                    rfl_type,
                    type.constType != 0,
                    modifier,
                    array_rank,
                    array_length_0,
                    array_length_1);
            }

            return null;
        }

        private int GetArrayRank(ref IDiaSymbol type, ref int length_0, ref int length_1)
        {
            // Not an array?
            if (type.symTag != (uint)SymTagEnum.SymTagArrayType)
                return 0;

            // 1D array
            length_0 = (int)type.count;
            type = type.type;
            if (type.symTag != (uint)SymTagEnum.SymTagArrayType)
                return 1;

            // 2D array
            length_1 = (int)type.count;
            type = type.type;
            if (type.symTag != (uint)SymTagEnum.SymTagArrayType)
                return 2;

            // Larger rank than currently allowed
            return 3;
        }

        private TypeModifier GetTypeModifier(ref IDiaSymbol type)
        {
            TypeModifier modifier = TypeModifier.Value;

            if (type.symTag == (uint)SymTagEnum.SymTagPointerType)
            {
                if (type.reference != 0)
                    modifier = TypeModifier.Reference;

                else
                    modifier = TypeModifier.Pointer;

                // Dereference pointer type
                type = type.type;
            }

            return modifier;
        }

        private void LoadReflectedTypes(Module module)
        {
            // Look for all overloaded implementations of the registration function
            IDiaEnumSymbols enum_symbols;
            m_Session.globalScope.findChildren(SymTagEnum.SymTagFunction, "RflReflectedTypesTable", 0, out enum_symbols);

            foreach (IDiaSymbol symbol in enum_symbols)
            {
                // Get the first argument
                IDiaEnumSymbols enum_args;
                symbol.findChildren(SymTagEnum.SymTagData, "arg", 0, out enum_args);
                IDiaSymbol arg = enum_args.Item(0);

                // Get base type information
                IDiaSymbol dia_type = arg.type.type;
                string type_name = GetTypeName(dia_type);
                string full_type_name = type_name;
                uint size = (uint)dia_type.length;

                Namespace cur_ns = module.GlobalNamespace.FindOrCreateNamespace(ref type_name);
                Rfl.Type type = null;

                // Is a template being reflected?
                symbol.findChildren(SymTagEnum.SymTagData, "is_template", 0, out enum_args);
                if (enum_args.count != 0)
                {
                    // Strip the template parameters
                    type_name = type_name.Remove(type_name.IndexOf('<'));
                    full_type_name = full_type_name.Remove(full_type_name.IndexOf('<'));

                    Rfl.Template template = new Rfl.Template(cur_ns, type_name);
                    cur_ns.Templates.Add(template);
                    type = template;

                    m_Logger.WriteSection("Reflect Request Template - {" + template.FullName.String + "}");
                }

                else
                {
                    switch ((SymTagEnum)dia_type.symTag)
                    {
                        case SymTagEnum.SymTagBaseType:
                            {
                                Rfl.BaseType base_type = new Rfl.BaseType(
                                    cur_ns,
                                    type_name,
                                    size,
                                    TypeOfVirtualAddress(full_type_name));

                                m_Logger.WriteSection("Reflect Request BaseType - {" + base_type.FullName.String + "}");

                                cur_ns.BaseTypes.Add(base_type);
                                type = base_type;

                                break;
                            }

                        case SymTagEnum.SymTagUDT:
                            {
                                Rfl.Class class_type = new Rfl.Class(
                                    cur_ns,
                                    type_name,
                                    size,
                                    TypeOfVirtualAddress(full_type_name),
                                    dia_type.constructor == 0);

                                m_Logger.WriteSection("Reflect Request Class - {" + class_type.FullName.String + "}");

                                cur_ns.Classes.Add(class_type);
                                type = class_type;

                                break;
                            }

                        case SymTagEnum.SymTagEnum:
                            {
                                Rfl.Enum enum_type = new Rfl.Enum(
                                    cur_ns,
                                    type_name,
                                    size,
                                    TypeOfVirtualAddress("enum " + full_type_name));

                                m_Logger.WriteSection("Reflect Request Enum - {" + enum_type.FullName.String + "}");

                                cur_ns.Enums.Add(enum_type);
                                LoadEnumEntries(enum_type, dia_type);
                                type = enum_type;

                                break;
                            }

                        default:
                            m_Logger.WriteError("Can not reflect unknown type - {" + type_name + "}");
                            continue;
                    }
                }

                // Check for light-weight type reflection
                symbol.findChildren(SymTagEnum.SymTagData, "minimal", 0, out enum_args);
                if (enum_args.count != 0)
                {
                    type.MinimalReflection = true;
                    m_Logger.Write("Minimal Reflection");
                }

                m_TypeMap.Add(full_type_name, type);
                m_DiaSymbolMap.Add(type, dia_type);

                m_Logger.EndSection();  
            }
        }

        private string GetTypeName(IDiaSymbol dia_type)
        {
            // Use the name directly for enum and UDTs
            if (dia_type.symTag == (uint)SymTagEnum.SymTagEnum ||
                dia_type.symTag == (uint)SymTagEnum.SymTagUDT)
                return dia_type.name;

            // Base types in the DIA SDK don't actually map 1-to-1 with the C++ types. For example, there
            // is no "unsigned char" type. This is instead labeled "unsigned int". Using the length of the
            // type, however, we can identify the intention of the type specification.

            switch ((BasicType)dia_type.baseType)
            {
                case BasicType.btVoid:
                    return "void";

                case BasicType.btChar:
                    return "char";

                case BasicType.btInt:
                    if (dia_type.length == 2)
                        return "short";
                    if (dia_type.length == 8)
                        return "__int64";
                    return "int";

                case BasicType.btUInt:
                    if (dia_type.length == 1)
                        return "unsigned char";
                    if (dia_type.length == 2)
                        return "unsigned short";
                    if (dia_type.length == 8)
                        return "unsigned __int64";
                    return "unsigned int";

                case BasicType.btFloat:
                    if (dia_type.length == 8)
                        return "double";
                    return "float";

                case BasicType.btBool:
                    return "bool";

                case BasicType.btLong:
                    return "long";

                case BasicType.btULong:
                    return "unsigned long";
            }

            return "";
        }

        private uint TypeOfVirtualAddress(string type_name)
        {
            // Look for the matching TypeOf function
            string gettype_name = "rfl::TypeOf<" + type_name;
            if (gettype_name.EndsWith(">"))
                gettype_name += " >";
            else
                gettype_name += ">";
            IDiaEnumSymbols enum_gettype_funcs;
            m_Session.globalScope.findChildren(SymTagEnum.SymTagFunction, gettype_name, 0, out enum_gettype_funcs);

            if (enum_gettype_funcs.count != 0)
            {
                // Look for the local static type variable
                IDiaSymbol gettype_func = enum_gettype_funcs.Item(0);
                IDiaEnumSymbols enum_data;
                gettype_func.findChildren(SymTagEnum.SymTagData, "type", 0, out enum_data);

                if (enum_data.count != 0)
                {
                    // Add to the first to the patch table
                    IDiaSymbol data = enum_data.Item(0);
                    return (uint)data.virtualAddress;
                }

                else
                {
                    m_Logger.WriteWarning("Couldn't find TypeOf local static variable - " + type_name);
                }
            }

            return 0;
        }

        private void LoadEnumEntries(Rfl.Enum type, IDiaSymbol dia_type)
        {
            IDiaEnumSymbols enum_entries;
            dia_type.findChildren(SymTagEnum.SymTagData, null, 0, out enum_entries);

            m_Logger.WriteSection("Reflecting Enum - {" + type.FullName.String + "}");

            foreach (IDiaSymbol symbol in enum_entries)
            {
                // The values of an enumeration are stored as the smallest type possible (sbyte, short, int)
                // so need casting to int (casting to uint gives out of range exceptions).
                int value = System.Convert.ToInt32(symbol.value);
                type.AddEntry(symbol.name, value);

                m_Logger.Write(symbol.name + " = " + value.ToString());
            }

            m_Logger.EndSection();
        }

        private IDiaDataSource m_DataSource;

        private IDiaSession m_Session;

        private Dictionary<string, Rfl.Type> m_TypeMap = new Dictionary<string, Rfl.Type>();

        private Dictionary<Rfl.Type, IDiaSymbol> m_DiaSymbolMap = new Dictionary<Rfl.Type, IDiaSymbol>();

        private AttributeParser m_AttributeParser = new AttributeParser();
        private bool m_AttributesBeingSet = false;

        private Logger m_Logger;
    }
}
