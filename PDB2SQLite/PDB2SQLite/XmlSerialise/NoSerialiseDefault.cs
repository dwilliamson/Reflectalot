using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace XmlSerialise
{
    public class NoSerialiseDefault : Attribute
    {
        public NoSerialiseDefault(object value)
        {
            m_DefaultValue = value;
        }

        public object m_DefaultValue;
    }
}
