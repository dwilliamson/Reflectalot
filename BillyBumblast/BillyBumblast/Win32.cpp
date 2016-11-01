
#include "Win32.h"
#include <cstdlib>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>


int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nShowCmd)
{
	return Win32::Main(__argc, (const char**)__argv);
}


int main(int argc, char* argv[])
{
	return Win32::Main(argc, (const char**)argv);
}


u64 Win32::GetProgramBaseAddress()
{
	return (u64)GetModuleHandle(0);
}