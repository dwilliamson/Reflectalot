using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Data.SQLite;
using System.Data.OleDb;
using System.Data.Common;
using System.Reflection;

namespace RflDB
{
    class SQLType
    {
        public SQLType(Type native_type, string sql_type_name)
        {
            NativeType = native_type;
            SQLTypeName = sql_type_name;
        }

        public SQLType(Type native_type, string sql_type_name, ValueToStringDelegate value_to_string)
        {
            NativeType = native_type;
            SQLTypeName = sql_type_name;
            ValueToString = value_to_string;
        }

        public Type NativeType;

        public string SQLTypeName;

        public delegate string ValueToStringDelegate(object value);
        public ValueToStringDelegate ValueToString = DefaultValueToString;

        private static string DefaultValueToString(object value)
        {
            return '"' + value.ToString() + '"';
        }
    }

    class Connection
    {
        public Connection(string filename)
        {
            // Create SQL type name map
            AddSQLType(new SQLType(typeof(string), "text"));
            AddSQLType(new SQLType(typeof(Int32), "integer"));
            AddSQLType(new SQLType(typeof(UInt32), "integer"));
            AddSQLType(new SQLType(typeof(bool), "boolean"));

            // Delete this for now so that I don't have to worry about updating the DB
            System.IO.File.Delete(filename);

            // Open a local SQLite connection
            DbProviderFactory factory = DbProviderFactories.GetFactory("System.Data.SQLite");
            m_Connection = factory.CreateConnection() as SQLiteConnection;
            m_Connection.ConnectionString = "Data Source=" + filename;
            m_Connection.Open();
        }

        public void AddSQLType(SQLType sql_type)
        {
            m_SQLTypes[sql_type.NativeType] = sql_type;
        }

        public SQLiteCommand CreateCommand(string text)
        {
            SQLiteCommand command = m_Connection.CreateCommand();
            command.Connection = m_Connection;
            command.CommandText = text;

            return command;
        }

        public void CreateTable(string name, Type data_type)
        {
            CreateTable(name, data_type, null);
        }

        public void CreateTable(string name, Type data_type, StringCollection included_columns)
        {
            SQLiteCommand command = CreateCommand("CREATE TABLE IF NOT EXISTS '" + name + "' (");

            List<string> columns = new List<string>();
            List<string> primary_keys = new List<string>();
            List<FieldInfo> fields = GetSpecifiedFields(data_type, included_columns);

            foreach (FieldInfo field in fields)
            {
                // If the field is to be serialised as a table, ignore it at this stage
                if (GetSerialiseTableAttr(field) != null)
                    continue;

                // Store integer values for enumerations
                Type field_type = field.FieldType;
                if (field.FieldType.IsEnum)
                {
                    field_type = typeof(int);
                }

                // Add column description
                string column_name = "'" + ConstructColumnName(field.Name) + "'";
                string column_desc = column_name + " " + m_SQLTypes[field_type].SQLTypeName;
                columns.Add(column_desc);

                // Keep track of all the primary keys
                object[] attributes = field.GetCustomAttributes(typeof(RflDB.PrimaryKey), true);
                if (attributes.Length != 0)
                {
                    primary_keys.Add(column_name);
                }
            }

            command.CommandText += ConstructCommaSeparatedList(columns);

            if (primary_keys.Count != 0)
            {
                // Construct primary key list
                command.CommandText += ", PRIMARY KEY(";
                command.CommandText += ConstructCommaSeparatedList(primary_keys);
                command.CommandText += ")";
            }

            // Finish the command and create the table
            command.CommandText += ")";
            command.ExecuteNonQuery();
        }

        public void InsertRows<TYPE>(string table_name, List<TYPE> rows)
        {
            InsertRows(table_name, rows, null);
        }

        public void InsertRows<TYPE>(string table_name, List<TYPE> rows, StringCollection included_columns)
        {
            // Pre-create most of the insert command
            SQLiteCommand command = CreateCommand("INSERT INTO '" + table_name + "'");

            List<string> columns = new List<string>();
            List<FieldInfo> fields = GetSpecifiedFields(typeof(TYPE), null);

            // Specify all columns
            foreach (FieldInfo field in fields)
            {
                if (GetSerialiseTableAttr(field) == null)
                {
                    string column_name = ConstructColumnName(field.Name);
                    columns.Add(column_name);
                }
            }

            command.CommandText += " (" + ConstructCommaSeparatedList(columns) + ")";
            command.CommandText += " VALUES(";

            SQLiteTransaction transaction = m_Connection.BeginTransaction();

            List<string> values = new List<string>();

            foreach (TYPE row in rows)
            {
                // Complete a copy of the pre-created command
                SQLiteCommand row_command = command.Clone() as SQLiteCommand;

                foreach (FieldInfo field in fields)
                {
                    RflDB.SerialiseTable serialise_table_attr = GetSerialiseTableAttr(field);
                    if (serialise_table_attr != null)
                    {
                        string sub_table_name = serialise_table_attr.ExecuteGetTableName(row);
                        CreateTable(sub_table_name, field.FieldType);
                    }
                    else
                    {
                        // Use integers for enumerations
                        object value = field.GetValue(row);
                        if (field.FieldType.IsEnum)
                        {
                            value = (int)value;
                        }

                        // Specify the value as a string
                        string str_value = m_SQLTypes[field.FieldType].ValueToString(value);
                        values.Add(str_value);
                    }
                }

                row_command.CommandText += ConstructCommaSeparatedList(values) + ")";
                values.Clear();

                // Defer query exection until the transaction commit
                row_command.Transaction = transaction;
                row_command.ExecuteNonQuery();
            }

            Console.WriteLine("Comitting.");
            transaction.Commit();
        }

        private List<FieldInfo> GetSpecifiedFields(Type data_type, StringCollection included_columns)
        {
            FieldInfo[] src_fields = data_type.GetFields();
            List<FieldInfo> dst_fields = new List<FieldInfo>();

            foreach (FieldInfo field in src_fields)
            {
                // Ignore transient fields
                object[] transient_attrs = field.GetCustomAttributes(typeof(RflDB.Transient), true);
                if (transient_attrs.Length != 0)
                    continue;

                // Ignore fields not specified    
                if (included_columns != null && !included_columns.Contains(field.Name))
                    continue;

                dst_fields.Add(field);
            }

            return dst_fields;
        }

        private RflDB.SerialiseTable GetSerialiseTableAttr(FieldInfo field)
        {
            // Check for the serialise table attribute
            object[] serialise_table_attrs = field.GetCustomAttributes(typeof(RflDB.SerialiseTable), true);
            if (serialise_table_attrs.Length == 0)
                return null;

            // Only interested in the first one
            return serialise_table_attrs[0] as RflDB.SerialiseTable;
        }

        private string ConstructColumnName(string field_name)
        {
            // Just strip out the member prefix
            if (field_name.StartsWith("m_"))
            {
                return field_name.Substring(2);
            }

            return field_name;
        }

        private string ConstructCommaSeparatedList(List<string> list)
        {
            return string.Join(", ", list.ToArray());
        }

        private SQLiteConnection m_Connection;

        private Dictionary<Type, SQLType> m_SQLTypes = new Dictionary<Type, SQLType>();
    }
}
