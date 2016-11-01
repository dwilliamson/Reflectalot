using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Rfl
{
    //
    // What's all this nonsense about using hash IDs for generating a unique ID for all the types??? Given
    // that we know all the types in the system at any one time, isn't the guaranteed-unique serial ID
    // the best option??? It can also guarantee a range that can be compressed to 16-bits (maybe optionally,
    // like the old Lightwave IDs).
    // NO!!!
    // If you add or remove a type in the DB it will adjust the IDs of all types, thus making it impossible
    // to reference types between DB versions.
    // 

    public class XmlNameSerialiser : XmlSerialise.CustomSerialiser
    {
        public override XmlNode Write(XmlDocument document, string field_name, object value)
        {
            Name name = (Name)value;

            //if (!name.IsEmpty())
            {
                XmlElement element = document.CreateElement(field_name);
                element.InnerText = name.Id.ToString();

                // This is only for debugging
                XmlAttribute attr = document.CreateAttribute("str");
                attr.Value = name.String;
                element.Attributes.Append(attr);

                return element;
            }

            return null;
        }
    }

    [XmlNameSerialiser]
    public class Name
    {
        public string String = "";

        public uint Id = 0;


        public Name(string name)
        {
            String = name;
            if (String != null)
                Id = MurmurHash2(String, 0xFEEDB00D);
        }


        public bool IsEmpty()
        {
            return String == null || String == "";
        }


        private uint MurmurHash2 (string key, uint seed)
        {
	        // 'm' and 'r' are mixing constants generated offline.
	        // They're not really 'magic', they just happen to work well.

	        const uint m = 0x5bd1e995;
	        const int r = 24;

	        // Initialize the hash to a 'random' value

            uint len = (uint)key.Length;
            int pos = 0;
	        uint h = seed ^ len;

	        // Mix 4 bytes at a time into the hash

	        while(len >= 4)
	        {
                uint k = 0;
                k |= (uint)key[pos + 0];
                k |= (uint)key[pos + 1] << 8;
                k |= (uint)key[pos + 2] << 16;
                k |= (uint)key[pos + 3] << 24;

		        k *= m; 
		        k ^= k >> r; 
		        k *= m; 
        		
		        h *= m; 
		        h ^= k;

		        pos += 4;
		        len -= 4;
	        }
        	
	        // Handle the last few bytes of the input array

	        switch(len)
	        {
                case 3: h ^= (uint)key[pos + 2] << 16; goto case 2;
                case 2: h ^= (uint)key[pos + 1] << 8; goto case 1;
                case 1: h ^= (uint)key[pos + 0];
                        h *= m; break;
	        };

	        // Do a few final mixes of the hash to ensure the last few
	        // bytes are well-incorporated.

	        h ^= h >> 13;
	        h *= m;
	        h ^= h >> 15;

	        return h;
        } 
    }
}
