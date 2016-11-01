
//
// http://www.informit.com/articles/article.aspx?p=22685
// http://www.informit.com/articles/article.aspx?p=22429
// http://www.thomasandamy.com/projects/CPB
//
// Functions belong to compilands. Classes seem to belong 
//
// Problems:
//
//    * How are duplicate entries resolved?
//    * How can individual libraries have their own types stored (to reduce scope)?
//    * Need to use name CRCs in place of names to reduce file size.
//    * Need access to the constructors so that the reflection can create objects of a specified type.\|
//
// Type
//   -> Class
//   -> Enumeration
//   -> Template
//
// How are collections handled? In previous systems I hard-coded support for them. For each instance of a
// templated type you can change the layout drastically:
//
//    * Change the type of specific members.
//    * Add or remove individual members.
//    * Specify different inheritance.
//    * Change the type of parameters within methods.
//    * Add or remove nested types.
//
// For this reason it seems that each instance of a template requires its own type description. This can
// be achieved. For example, std::vector will have multiple implementations for each array type, with each
// data member pointing to memory cast to the specific type. Unfortunately, std::vector also has a huge
// amount of methods that we don't necessarily want to reflect.
//
// Each language implementation that you want to bind to will likely have its own collection representations.
// When you marshal from one language to another you'll need to send some context information. As in: I'm sending
// a std::vector as a sequence of bytes that I want you to reconstruct as a System.Array on the other side.
//
// When you load a std::vector from file you'll need to construct each and every object. Simply serialising
// the memory stream won't work: there might be some code in a constructor that requires execution (some
// auto-registration code, for example). Of course, you can specialise for PODs without pointers by just reading
// one big buffer and thus speeding serialisation.
//
// Can you write custom serialisers for specific types?
//
// A class that's a POD can not be serialised by fread/write. It contains fields that might be added/removed.
// Of course, when you don't care about versioning this can be done and serialisation will be much faster. Basic
// types such as int or char can always be serialised with fread/write, as long as endianness is taken into
// consideration.
//
// ---
//
// How are namespaces reflected? It would be good if they could be done automatically. Problem is, a class
// gets reflected as "Namespace::Class" in its name. We need to:
//
//    1. Inspect the parent to see if it's a namespace.
//    2. Create the namespace if it doesn't exist.
//    3. Add the class to the namespace.
//    4. Make sure the class name has the correct name (minus the namespace).
//
// Note that a structure embedded in a structure (e.g. A::B), where the outer class isn't reflected, leads to the
// outer class being auto-reflected as a namespace.
//

using System;
using System.Collections.Generic;
using System.Text;
using Dia2Lib;


namespace PDB2SQLite
{
    class Program
    {
        static long point = 0;

        static void Point(string text)
        {
            long end = DateTime.Now.Ticks;
            float span = end - point;
            float time = span / TimeSpan.TicksPerSecond;
            Console.WriteLine(text + ": " + time.ToString());

            point = DateTime.Now.Ticks;
        }

        static void Main(string[] args)
        {
            point = DateTime.Now.Ticks;

            //string pdb_file = @"c:\usr\code\projects\lame\Test\release\test.pdb";
            string pdb_file = @"e:\dev\Rfl\BillyBumblast\bin\debug\billybumblast.pdb";
            if (args.Length != 0)
                pdb_file = args[0].ToLower();

            Rfl.PDBLoader pdb_loader = new Rfl.PDBLoader();
            Rfl.Module module = pdb_loader.Load(pdb_file);

            Point("Loading PDB");

            XmlSerialise.Writer xml_writer = new XmlSerialise.Writer("RflDb");
            xml_writer.SerialiseCollection(xml_writer.RootNode, Rfl.Module.Names.Values, "Names");
            xml_writer.Serialise(module.GlobalNamespace, "Namespace");

            Point("Serialised XML");

            string xml_file = pdb_file.Replace(".pdb", ".xml");
            xml_writer.Document.Save(xml_file);

            Point("Saved XML");
        }
    }
}

