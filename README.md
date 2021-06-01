# About

**Note**: Other than BagoumLib, this project is extremely early in development and is not presently usable.

Suzunoya is a visual novel engine built in C#, designed to be portable between game engines.
It is free (as in free speech) software. The source code is on Github: https://github.com/Bagoum/suzunoya

The engine name may be abbreviated as **szy**.

Each supported game engine (such as Unity) has its own adapter subproject that maps the objects in the core engine to engine-specific game-objects. SuzunoyaUnity (https://github.com/Bagoum/suzunoya-unity) is the only one under current development. 

All code is written in C#9 and compiled against Framework 4.7.2 for compatibility with Unity. This means that some really useful features, like default interface implementations, cannot presently be used.

# Subprojects

- BagoumLib: common class library used by Suzunoya proper as well as some of my other codebases. You can compile it to a DLL and use it separately if you like. Note that the task code in BagoumLib uses the custom ICancellee interface, which is not thread-safe in the way CancellationToken is. (I use this custom interface to avoid .Dispose calls.)
- Suzunoya: Engine-agnostic visual novel engine, written as a class library. (not presently functional)
- SuzunoyaUnity: Adapter for the Unity game engine. (not presently functional)

# Licensing

The source code (all subprojects) is licensed under MIT. See the COPYING file for details.

