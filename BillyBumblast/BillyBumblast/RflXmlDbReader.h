
#pragma once


namespace rfl
{
	struct Module;


	struct XmlDbReader
	{
		static Module* LoadModule(const char* xml_file);
	};
}