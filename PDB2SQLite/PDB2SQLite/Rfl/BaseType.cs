using System;
using System.Collections.Generic;
using System.Text;

namespace Rfl
{
    public class BaseType : Rfl.Type
    {
        public BaseType(Scope parent_scope, string name, uint size, uint typeof_va)
            : base(parent_scope, name, size, typeof_va)
        {
        }
    }
}
