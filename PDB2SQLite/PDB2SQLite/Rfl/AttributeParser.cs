using System;
using System.Collections.Generic;
using System.Text;
using Spart.Parsers;
using Spart.Parsers.NonTerminal;
using Spart.Actions;
using System.Diagnostics;


namespace Rfl
{
    public class AttributeParser
    {
        // Rules for acting upon the text being parsed
        private Rule m_NameRule;
        private Rule m_SymbolRule;
        private Rule m_IntegerRule;
        private Rule m_ScalarRule;
        private Rule m_TextRule;
        private Rule m_AttributeRule;

        // Root parser that embeds all the rules
        private Parser m_AttributesParser;

        // Current attribute being parsed
        private Attribute m_Attribute;

        // Current set of attributes being applied
        private Dictionary<string, Attribute> m_Attributes = new Dictionary<string,Attribute>();

        private Stack<List<string>> m_AttributeStack = new Stack<List<string>>();


        public AttributeParser()
        {
            // Basic symbolic parameters
            Parser name = Ops.Seq(Prims.Letter | '_', Ops.Star(Prims.LetterOrDigit | '_'));
            m_NameRule = new Rule(name);
            m_SymbolRule = new Rule(name);

            // Integer and floating point parameters
            Parser natural = Ops.Plus(Prims.Digit);
            Parser integer = Ops.Seq(Ops.Optional('-'), natural);
            Parser scalar = Ops.Seq(Ops.Optional('-'), Ops.Star(Prims.Digit), '.', natural, Ops.Optional(Ops.Seq('e', integer)));
            m_IntegerRule = new Rule(integer);
            m_ScalarRule = new Rule(scalar);

            // String parameters
            Parser quotes_esc = Ops.Seq('\\', '\"');
            NegatableParser quotes = Prims.Ch('\"');
            Parser text = Ops.Seq('\"', Ops.Star(quotes_esc | ~quotes), '\"');
            m_TextRule = new Rule(text);

            // Definition of an attribute
            Parser whitespace = Ops.Star(Prims.WhiteSpace);
            Parser assign = Ops.Seq(whitespace, '=', whitespace, m_SymbolRule | m_ScalarRule | m_IntegerRule | m_TextRule);
            m_AttributeRule = new Rule(Ops.Seq(m_NameRule, Ops.Optional(assign)));

            // The final definition of a list of attributes
            m_AttributesParser = Ops.Seq(whitespace, m_AttributeRule, Ops.Star(Ops.Seq(whitespace, ',', whitespace, m_AttributeRule)));

            // Setup event handlers for reading the parse results
            m_NameRule.Act += OnName;
            m_SymbolRule.Act += OnSymbol;
            m_IntegerRule.Act += OnInteger;
            m_ScalarRule.Act += OnScalar;
            m_TextRule.Act += OnText;
        }


        public void PushAttributes(string input)
        {
            // Collect a list of attributes parsed in this call
            List<string> attributes = new List<string>();
            ActionHandler handler = delegate(object sender, ActionEventArgs args)
            {
                // TODO: What happens if an attribute is pushed twice??? Simplest is to error as the stack could make things complex.
                attributes.Add(m_Attribute.Name.String);
                m_Attributes.Add(m_Attribute.Name.String, m_Attribute);
            };

            // Parse the attributes
            m_AttributeRule.Act += handler;
            Spart.Scanners.StringScanner scan = new Spart.Scanners.StringScanner(input);
            ParserMatch match = m_AttributesParser.Parse(scan);
            m_AttributeRule.Act -= handler;

            // Record for the next pop
            m_AttributeStack.Push(attributes);
        }


        public void PopAttributes()
        {
            // Remove all attributes added in the previous push
            List<string> attributes = m_AttributeStack.Peek();
            foreach (string attribute in attributes)
            {
                m_Attributes.Remove(attribute);
            }
        }


        public void PopAllAttributes()
        {
            m_AttributeStack.Clear();
            m_Attributes.Clear();
        }


        public List<Attribute> GetAttributes()
        {
            return new List<Attribute>(m_Attributes.Values);
        }


        private void OnName(object sender, ActionEventArgs args)
        {
            // Start a new attribute, ready for another pass
            m_Attribute = new Attribute();
            m_Attribute.Name = new Name(args.Value);
            Module.AddName(m_Attribute.Name);
        }


        private void OnSymbol(object sender, ActionEventArgs args)
        {
            m_Attribute.ValueType = Attribute.Type.Symbol;
            m_Attribute.Value = args.Value;
        }


        private void OnInteger(object sender, ActionEventArgs args)
        {
            m_Attribute.ValueType = Attribute.Type.Integer;
            m_Attribute.Value = args.Value;
        }


        private void OnScalar(object sender, ActionEventArgs args)
        {
            m_Attribute.ValueType = Attribute.Type.Float;
            m_Attribute.Value = args.Value;
        }


        private void OnText(object sender, ActionEventArgs args)
        {
            // Strip the quote marks from the string
            m_Attribute.ValueType = Attribute.Type.String;
            m_Attribute.Value = args.Value.Substring(1, args.Value.Length - 2);
        }
    }
}
