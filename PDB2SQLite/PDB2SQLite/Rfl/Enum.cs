
//
// Anonymous enumerations need reflecting somehow... just keep adding them to a global enumeration type?
//

using System;
using System.Collections.Generic;
using System.Text;

namespace Rfl
{
    public class Enum : Rfl.Type
    {
        // A name/value enumeration pair
        public class Entry
        {
            public Name Name;

            public int Value;
        };

        // All possible values for this enumeration
        public List<Entry> Entries = new List<Entry>();

        public List<Attribute> Attributes = new List<Attribute>();

        public Enum(Scope parent_scope, string name, uint size, uint typeof_va)
            : base(parent_scope, name, size, typeof_va)
        {
        }

        public void AddEntry(string str_name, int value)
        {
            // Make sure the name is registered with the main module
            Name name = new Name(str_name);
            Module.AddName(name);

            // Add the entry
            Entry entry = new Entry();
            entry.Name = name;
            entry.Value = value;
            Entries.Add(entry);
        }
    }
}
