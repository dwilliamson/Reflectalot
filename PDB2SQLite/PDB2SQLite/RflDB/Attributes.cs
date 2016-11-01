using System;
using System.Collections.Generic;
using System.Text;

namespace RflDB
{
    // For marking class fields as database primary keys
    class PrimaryKey : Attribute
    {
    }

    // Don't serialise
    class Transient : Attribute
    {
    };

    // Serialise a collection into table and get the table name from specified method
    class SerialiseTable : Attribute
    {
        public SerialiseTable(string get_table_name)
        {
            GetTableName = get_table_name;
        }

        public string ExecuteGetTableName(object containing_object)
        {
            // First check to see if the cached method exists
            if (MethodInfo == null)
            {
                // Extract the type name and function name
                int sep_index = GetTableName.LastIndexOf('.');
                string type_name = GetTableName.Substring(0, sep_index);
                string func_name = GetTableName.Substring(sep_index + 1);

                // Lookup the required method
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                System.Type type = assembly.GetType(type_name);
                MethodInfo = type.GetMethod(func_name);
            }

            // Call the users function to generate the table name
            string table_name = (string)MethodInfo.Invoke(containing_object, null);
            return table_name;
        }

        private string GetTableName;

        private System.Reflection.MethodInfo MethodInfo;
    };
}
