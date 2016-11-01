using System;
using System.Collections.Generic;
using System.Text;

namespace Rfl
{
    public class Attribute
    {
        public enum Type
        {
            Boolean,
            Integer,
            Float,
            Symbol,
            String
        }

        public Type ValueType = Type.Boolean;

        public Name Name;

        public string Value;
    }
}
