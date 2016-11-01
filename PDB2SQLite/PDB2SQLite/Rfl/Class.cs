using System;
using System.Collections.Generic;
using System.Text;

namespace Rfl
{
    public class Class : Rfl.Type
    {
        public bool IsPOD;

        public List<Field> Fields = new List<Field>();

        public List<Attribute> Attributes = new List<Attribute>();

        public Class(Scope parent_scope, string name, uint size, uint typeof_va, bool is_pod)
            : base(parent_scope, name, size, typeof_va)
        {
            IsPOD = is_pod;
        }
    }
}
