using System;
using System.Collections.Generic;
using System.Text;

namespace Rfl
{
    public class Module
    {
        public Namespace GlobalNamespace = new Namespace(null, "Global");

        // List of names in the module - this needs to be moved elsewhere, eventually
        public static Dictionary<string, Name> Names = new Dictionary<string, Name>();

        public static void AddName(Name name)
        {
            if (name.IsEmpty() || Names.ContainsKey(name.String))
                return;

            Names.Add(name.String, name);
        }

        public static uint GetUniqueTypeID()
        {
            return CurrentUniqueTypeID++;
        }

        private static uint CurrentUniqueTypeID = 0;
    }
}
