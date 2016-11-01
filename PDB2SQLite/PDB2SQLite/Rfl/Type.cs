using System;
using System.Collections.Generic;
using System.Text;
using Dia2Lib;
using System.Xml;

namespace Rfl
{
    public class XmlTypeNameSerialiser : XmlNameSerialiser
    {
        public override XmlNode Write(XmlDocument document, string field_name, object value)
        {
            Rfl.Type type = (Rfl.Type)value;
            return base.Write(document, field_name, type.FullName);
        }
    }

    //
    // The base type that all other types derive from.
    //
    public class Type : Scope
    {
        public uint UniqueID;

        public uint Size = 0;

        // Represents the location of a TypeOf function implementation within the executables virtual address
        // space. There is one of these for each reflected type and the loading code for a specific reflection
        // DB will patch all these addresses so that they point to the required type objects.
        [XmlSerialise.NoSerialiseDefault(0)]
        public uint TypeOfVA = 0;

        // Reflect the absolute minimum?
        [XmlSerialise.Transient]
        public bool MinimalReflection = false;

        // Important class methods
        // TODO: Are other operators needed?
        [XmlSerialise.NoSerialiseDefault(-1)]
        public int ConstructorIndex = -1;

        [XmlSerialise.NoSerialiseDefault(-1)]
        public int DestructorIndex = -1;

        [XmlSerialise.NoSerialiseDefault(-1)]
        public int CopyConstructorIndex = -1;

        [XmlSerialise.NoSerialiseDefault(-1)]
        public int AssignmentOperatorIndex = -1;

        public Type(Scope parent_scope, string name, uint size, uint typeof_va)
            : base(parent_scope, name)
        {
            UniqueID = Module.GetUniqueTypeID();
            Size = size;
            TypeOfVA = typeof_va;
        }
    }
}
