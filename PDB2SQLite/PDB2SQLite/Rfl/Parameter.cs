using System;
using System.Collections.Generic;
using System.Text;

namespace Rfl
{
    public enum TypeModifier
    {
        Value,
        Pointer,
        Reference
    }


    public class Parameter
    {
        public Name Name;

        [XmlTypeNameSerialiser]
        public Rfl.Type Type;

        public bool IsConst;

        public TypeModifier Modifier;

        [XmlSerialise.NoSerialiseDefault(0)]
        public int ArrayRank = 0;

        [XmlSerialise.NoSerialiseDefault(1)]
        public int ArrayLength0 = 1;

        [XmlSerialise.NoSerialiseDefault(1)]
        public int ArrayLength1 = 1;

        public Parameter(Name name, Rfl.Type type, bool is_const, TypeModifier modifier, int array_rank, int array_length_0, int array_length_1)
        {
            Name = name;
            Type = type;
            IsConst = is_const;
            Modifier = modifier;
            ArrayRank = array_rank;
            ArrayLength0 = array_length_0;
            ArrayLength1 = array_length_1;
        }
    }
}
