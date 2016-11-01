using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace XmlSerialise
{
    public abstract class CustomSerialiser : Attribute
    {
        public abstract XmlNode Write(XmlDocument document, string field_name, object value);
    }
}
