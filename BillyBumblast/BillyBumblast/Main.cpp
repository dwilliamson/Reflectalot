 
#include "Rfl.h"
#include "RflXmlDbReader.h"
#include "Win32.h"
#include "BinarySerialiser.h"
#include "STLVector.h"
#include "STLString.h"

#include <sstream>


// TODO:
// * Attributes need to be proven
// * Serialisation of pointers to objects
// * Iterators
// * Inheritance hierarchy
// * Endian-ness swap when the generating machine differs from the loading machine
// * Smart pointers
// * Overloaded methods (e.g. constructors)
// * Custom field serialisation - can you bake serialisation decisions into this?
// * Look at the inheritance tree - does everything really need to inherit from Scope?
// * Casting types to their most derived needs to be a simple, clear operation
// * Function calling API with varying calling conventions (http://msdn.microsoft.com/en-us/library/k2b2ssfy(VS.71).aspx)
// * Handle multiple DLLs (e.g. for the case of mult-threaded debug dll crt libs)
//
// DONE:
// * Native C++ arrays
// * Create objects by type name
// * Template-based collections
// * Support for copy constructors
// * Store references to constructor/copy/destructor in XML file
// * Ensure PODs don't generate constructors (YAY!)
// * Template instances don't have unique names so they can't be stored in any type map!
// * Need a lightweight reflect for types where the internals don't matter (e.g. std::string)
// * Storage for strings
// * Store constructor/destructor pointers in type, with Construct/Destruct methods
// * Copy constructor and assignment operator are getting optimised out
// * Need template instance types that don't expose the full API, just constructors, etc.
// * If you reflect type-only then constructors/etc are missing! This is not an option!
//
// int a[3];		array 3
// int* a;			pointer
// int* a[3];		pointer to array 3

// Legal:
// typedef int IntArray[3];
// int a[3];
// int* a[3];
// IntArray a;
//
// Not legal:
// IntArray* a;
//
// The above limitation is so because the classification of array/pointer/reference is
// done with a Parameter type, rather than treating (for example) int[3] as a unique
// type. The Parameter type is far more light-weight than Type, which is one of the
// reasons for this design choice. Multi-dimensional arrays would also get quite
// heavy-weight with this kind of design. Not only will there be that limitation,
// but I'm limiting this to 2-dimensional arrays of 2^15 max length each.
//

typedef int ArrayType[3];

struct ArrayReflection
{
	int* a[3];
	int *(b[3]);
	ArrayType* c;
	int d[3][4];
};

RFL_REFLECT_TYPE(ArrayReflection);

struct Testy
{
	typedef int What;
};

template <typename TYPE> struct Blah
{
	typedef typename TYPE::What What;
};



struct Vec2Di
{
	int x, y;
};

//class_attr(std::string, Load = SerialiseSTLString, Save = DeserialiseSTLString);

//class_attr(Configuration, transient);

//class_attr(Configuration)
struct Configuration
{
	push_attr(group = "Base Group")

		std::string window_name;

		attr(transient, name = "Resolution", desc = "Specifies the resolution of the thingy", value = 3)
		Vec2Di resolution;

		Vec2Di offset;

		attr(min = 0.2, max = 0.4)
		float tolerance;

		std::vector<int> high_scores;
		std::vector<int> low_scores;
		std::vector< std::string > names;

	pop_attr();

	int fixed_array[5];
	std::string looking_place[2];
};

struct PODTest
{
	class NestedClassTest
	{
	};

	void* Func() const
	{
		printf("hello\n");
		return 0;
	}

	int x;
	const char* u;
	char b[3];
};


int Win32::Main(int argc, const char** argv)
{
	PODTest p;
	p.Func();

	rfl::Module* module = rfl::XmlDbReader::LoadModule("BillyBumblast.xml");
	rfl::Type* string_type = rfl::TypeOf<std::string>();
	rfl::TemplateInstance* vectype0 = static_cast<rfl::TemplateInstance*>(rfl::TypeOf< std::vector<int> >());
	rfl::Type* vectype1 = vectype0->instance_of;

	string_type->serialise_func = SerialiseSTLString;
	string_type->deserialise_func = DeserialiseSTLString;
	vectype1->serialise_func = STLVector::Serialise;
	vectype1->deserialise_func = STLVector::Deserialise;

	Configuration config;
	config.resolution.x = 640;
	config.resolution.y = 480;
	config.offset.x = 10;
	config.offset.y = 20;
	config.window_name = "Helooo";
	config.tolerance = 3.7f;
	config.high_scores.push_back(3);
	config.high_scores.push_back(4);
	config.names.push_back("don");
	config.names.push_back("tracey");
	config.names.push_back("debbie");
	config.fixed_array[0] = 3;
	config.fixed_array[1] = 8;
	config.fixed_array[2] = 17;
	config.fixed_array[3] = 25;
	config.fixed_array[4] = 42;
	config.looking_place[0] = "high";
	config.looking_place[1] = "low";

	std::stringstream s;
	serialise::BinarySerialise(config, s);

	Configuration configb;
	serialise::BinaryDeserialise(configb, s);

	return 0;
}


enum GlobalEnum
{
	BLAH = 3,
	FOREVER = 4,
	WHAT = -1
};

RFL_REFLECT_TYPE(PODTest);
RFL_REFLECT_TYPE(GlobalEnum);
RFL_REFLECT_TYPE(Configuration);
RFL_REFLECT_TYPE(Vec2Di);
RFL_REFLECT_TYPE_MINIMAL(std::string);
RFL_REFLECT_TEMPLATE(std::allocator<rfl::TemplateArg>);
RFL_REFLECT_TEMPLATE(std::vector<rfl::TemplateArg>);
