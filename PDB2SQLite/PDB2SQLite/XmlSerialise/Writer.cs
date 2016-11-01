
//
// Problems with MS XML serialiser:
//
//    1. Each type requires a default constructor.
//    2. You can't write a custom serialiser for one field only - it's all or nothing.
//    3. Use of IXmlSerializable is too verbose.
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Reflection;
using System.Runtime.Serialization;

namespace XmlSerialise
{
    public class Writer
    {
        public Writer(string root_name)
        {
            // Create the root container node
            RootNode = Document.CreateElement(root_name);
            Document.AppendChild(RootNode);
        }

        public void Serialise(object value, string name)
        {
            XmlNode object_node = SerialiseObject(value, name);
            RootNode.AppendChild(object_node);
        }

        public XmlNode SerialiseObject(object value, string name)
        {
            // First attempt to use a custom type serialiser
            Type type = value.GetType();
            XmlNode node = TryCustomSerialise(value, name, type.GetCustomAttributes(typeof(CustomSerialiser), true));
            if (node != null)
                return node;

            // Create node and serialise its fields
            XmlElement element = Document.CreateElement(name);
            SerialiseFields(element, type, value);

            return element;
        }

        private XmlNode TryCustomSerialise(object value, string name, object[] attributes)
        {
            if (attributes.Length == 0)
                return null;

            CustomSerialiser serialiser = (CustomSerialiser)attributes[0];
            return serialiser.Write(Document, name, value);
        }

        private bool FieldMatchesDefault(FieldInfo field, object value)
        {
            // Any defaults?
            object[] attributes = field.GetCustomAttributes(typeof(NoSerialiseDefault), true);
            if (attributes.Length == 0)
                return false;

            // Perform comparison with default
            object default_value = (attributes[0] as NoSerialiseDefault).m_DefaultValue;
            return default_value.Equals(value);
        }

        public void SerialiseCollection(XmlNode parent_node, System.Collections.IEnumerable enumerable, string name)
        {
            XmlElement element = Document.CreateElement(name);

            // Serialise each entry in the collection
            foreach (object sub_val in enumerable)
            {
                XmlNode child_node = SerialiseObject(sub_val, sub_val.GetType().Name);
                element.AppendChild(child_node);
            }

            // Only add if the collection has entries
            if (element.ChildNodes.Count != 0)
                parent_node.AppendChild(element);
        }

        private void SerialiseFields(XmlNode parent_node, Type type, object value)
        {
            // Serialise the base types first
            if (type.BaseType != typeof(object))
                SerialiseFields(parent_node, type.BaseType, value);

            // Visit only fields at this type level
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                // Ignore this field?
                if (field.GetCustomAttributes(typeof(Transient), true).Length != 0)
                    continue;

                object field_value = field.GetValue(value);
                Type field_type = field.FieldType;
                if (field_value == null)
                    continue;

                if (FieldMatchesDefault(field, field_value))
                    continue;

                // Try and convert to a collection
                System.Collections.IEnumerable enum_object = field_value as System.Collections.IEnumerable;

                // Try a custom field serialiser
                XmlNode custom_node = TryCustomSerialise(field_value, field.Name, field.GetCustomAttributes(typeof(CustomSerialiser), true));
                if (custom_node != null)
                {
                    parent_node.AppendChild(custom_node);
                }

                else if (enum_object != null && field_type != typeof(string))
                {
                    SerialiseCollection(parent_node, enum_object, field.Name);
                }

                else if (field_type.IsSerializable)
                {
                    // End recursion with serializable types, storing them as the text within an element.
                    // Doesn't handle the case of a correctly implemented ISerializable - just uses it as a hint.
                    XmlElement element = Document.CreateElement(field.Name);
                    element.InnerText = field_value.ToString();
                    parent_node.AppendChild(element);
                }

                else
                {
                    // Recurse into non-primitive types
                    XmlNode node = SerialiseObject(field_value, field.Name);
                    parent_node.AppendChild(node);
                }
            }
        }

        public XmlDocument Document = new XmlDocument();

        public XmlNode RootNode;
    }
}
