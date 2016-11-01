using System;
using System.Collections.Generic;
using System.Text;

namespace Rfl
{
    public class Field : Parameter
    {
        public int Offset;

        public List<Attribute> Attributes = new List<Attribute>();

        public Field(Parameter parameter, int offset)
            : base(parameter.Name, parameter.Type, parameter.IsConst, parameter.Modifier, parameter.ArrayRank, parameter.ArrayLength0, parameter.ArrayLength1)
        {
            Offset = offset;
        }
    }
}
