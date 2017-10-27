# Contribution Guidelines

## Build failures due to locked files

When the solution is open in Visual Studio, the CodeGeneration.Roslyn.Tasks project
build may fail due to its output assembly being locked on disk.
This is because the test project in the solution consumes this DLL as part
of its design-build and VS doesn't unload the file. 

There are two workarounds for this:

1. Unload the CodeGeneration.Roslyn.Tasks project. Once it's built once, you don't
   tend to need it to build again anyway. Most code changes aren't to that assembly.
2. Unload the CodeGeneration.Roslyn.Tests project and restart Visual Studio.
   This will prevent the design-time builds in Visual Studio from loading and locking
   the output of the CodeGeneration.Roslyn.Tasks project, allowing you to make changes
   to that project and build them.

## Requirements
If you're running on Windows 10, you need to install the .net 3.5 optional feature in order to compile the project.
