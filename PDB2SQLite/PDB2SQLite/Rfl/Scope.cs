using System;
using System.Collections.Generic;
using System.Text;

namespace Rfl
{
    public class Scope
    {
        [XmlSerialise.Transient]
        public Scope ParentScope;

        public Name Name;

        public Name FullName;

        public List<Rfl.Namespace> Namespaces = new List<Rfl.Namespace>();

        public List<Rfl.BaseType> BaseTypes = new List<Rfl.BaseType>();

        public List<Rfl.Class> Classes = new List<Rfl.Class>();

        public List<Rfl.Template> Templates = new List<Rfl.Template>();

        public List<Rfl.TemplateInstance> TemplateInstances = new List<TemplateInstance>();

        public List<Rfl.Enum> Enums = new List<Rfl.Enum>();

        public List<Rfl.Function> Functions = new List<Rfl.Function>();

        public Scope(Scope parent_scope, string name)
        {
            ParentScope = parent_scope;
            Name = new Name(name);
            FullName = new Name(name);

            if (name != "")
            {
                // Determine full name by ignoring the root global scope
                if (ParentScope != null && ParentScope.ParentScope != null)
                    FullName = new Name(ParentScope.ConstructFullName(name));

                Module.AddName(Name);

                if (Name.String != FullName.String)
                    Module.AddName(FullName);
            }
        }


        public string ConstructFullName(string name)
        {
            return FullName.String + "::" + name;
        }
    }
}
