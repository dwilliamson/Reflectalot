using System;
using System.Collections.Generic;
using System.Text;

namespace Rfl
{
    public class Function
    {
        public Name Name;

        public uint CallAddress;

        public Parameter ReturnParameter = null;

        public List<Parameter> Parameters = new List<Parameter>();

        public List<Attribute> Attributes = new List<Attribute>();

        public Function(Name name, uint call_address)
        {
            Name = name;
            CallAddress = call_address;
        }
    }
}
