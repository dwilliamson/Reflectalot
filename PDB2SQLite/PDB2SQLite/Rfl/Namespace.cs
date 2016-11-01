using System;
using System.Collections.Generic;
using System.Text;

namespace Rfl
{
    public class Namespace : Scope
    {
        public Namespace(Scope parent_scope, string name)
            : base(parent_scope, name)
        {
        }

        public Namespace FindOrCreateNamespace(ref string type_name)
        {
            Namespace cur_ns = this;

            int template_nest = 0;
            int prev_ns_pos = 0;

            for (int i = 0; i < type_name.Length; i++)
            {
                char val = type_name[i];

                // Ignore parsing of namespaces within templates
                if (val == '<')
                    template_nest++;
                else if (val == '>')
                    template_nest--;

                if (val == ':' && template_nest == 0)
                {
                    string ns_name = type_name.Substring(prev_ns_pos, i - prev_ns_pos);

                    // Create the namespace only if it doesn't exist
                    Namespace nested_ns = cur_ns.Namespaces.Find(delegate(Namespace ns) { return ns.Name.String == ns_name; });
                    if (nested_ns == null)
                    {
                        Namespace new_ns = new Namespace(cur_ns, ns_name);
                        cur_ns.Namespaces.Add(new_ns);
                        cur_ns = new_ns;
                    }
                    else
                    {
                        cur_ns = nested_ns;
                    }

                    // Skip over symbol separator
                    prev_ns_pos = i + 2;
                    i++;
                }
            }

            // The last bit is the namespace-local type name
            type_name = type_name.Substring(prev_ns_pos);

            return cur_ns;
        }
    }
}
