using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace Rfl
{
    public class DBWriter
    {
        public void WriteModule(Rfl.Module module, string filename)
        {
            m_Connection = new RflDB.Connection(filename);
            m_Connection.AddSQLType(new RflDB.SQLType(typeof(Rfl.Name), "integer", NameValueToString));

            CreateEnumTable("Internal.Rfl.ParameterStorage", typeof(Rfl.TypeModifier));

            m_Connection.CreateTable("Module.Names", typeof(Rfl.Name));
            m_Connection.CreateTable("Module.BaseTypes", typeof(Rfl.Type));
            m_Connection.CreateTable("Module.Classes", typeof(Rfl.Class));
            m_Connection.CreateTable("Module.Enums", typeof(Rfl.Enum));

            //m_Connection.InsertRows("Module.Names", module.Names);
            //m_Connection.InsertRows("Module.BaseTypes", module.GlobalNamespace.BaseTypes);
            //m_Connection.InsertRows("Module.Classes", module.GlobalNamespace.Classes);
            //m_Connection.InsertRows("Module.Enums", module.GlobalNamespace.Enums);
        }

        private void CreateEnumTable(string name, System.Type enum_type)
        {
            m_Connection.CreateTable(name, typeof(EnumDescription));

            FieldInfo[] fields = enum_type.GetFields();
            List<EnumDescription> enum_rows = new List<EnumDescription>();

            // Build a row for each enumeration
            foreach (FieldInfo field in fields)
            {
                if (field.IsLiteral)
                {
                    EnumDescription desc = new EnumDescription();
                    desc.Value = (int)field.GetRawConstantValue();
                    desc.Name = field.Name;
                    enum_rows.Add(desc);
                }
            }

            m_Connection.InsertRows(name, enum_rows);
        }

        private static string NameValueToString(object value)
        {
            Rfl.Name name = value as Rfl.Name;
            return name.Id.ToString();
        }

        private class EnumDescription
        {
            [RflDB.PrimaryKey]
            public int Value;

            public string Name;
        };

        RflDB.Connection m_Connection;
    }
}
