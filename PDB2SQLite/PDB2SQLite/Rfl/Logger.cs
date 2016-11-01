using System;
using System.Collections.Generic;
using System.IO;

namespace Rfl
{
    class Logger
    {
        public Logger(string filename)
        {
            m_Writer = new StreamWriter(filename);
            m_Tab = "";
            m_TabString = "";

            for (int i = 0; i < m_TabSize; i++)
                m_TabString += " ";

            Write("Log Started");
        }

        public void Write(string text)
        {
            text = "[" + DateTime.Now.ToLongTimeString() + "] " + m_Tab + text + "\n";
            m_Writer.Write(text);
        }

        public void WriteWarning(string text)
        {
            Write("<WARNING> " + text);
        }

        public void WriteError(string text)
        {
            Write("<ERROR> " + text);
        }

        public void WriteSection(string text)
        {
            Write(text);
            BeginSection();
        }

        public void BeginSection()
        {
            m_Tab += m_TabString;
        }

        public void EndSection()
        {
            m_Tab = m_Tab.Substring(0, m_Tab.Length - m_TabSize);
        }

        public void Flush()
        {
            m_Writer.Flush();
        }

        private StreamWriter m_Writer;
        private string m_Tab;
        private int m_TabSize = 4;
        private string m_TabString;
    }
}
