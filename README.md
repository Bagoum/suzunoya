# About

Suzunoya is a visual novel engine built in C#, designed to be portable between game engines.
It is free (as in free speech) software. The source code is on Github: https://github.com/Bagoum/suzunoya

The engine name may be abbreviated as **szy**.

Each supported game engine (such as Unity) has its own adapter that maps the objects in the core engine to engine-specific game-objects. SuzunoyaUnity (https://github.com/Bagoum/suzunoya-unity) is the only one under current development. 

All code is written in C#9 and compiled against Standard 2.1 for compatibility with Unity 2021.2. This version of Unity allows all C#8 features.

# Subprojects

### BagoumLib

BagoumLib is a small class library with some convenient functionality, including:

- Easing, lerping, and tweening
- Convenient data structures such as compacting arrays and coroutine iterators 
- Low-garbage event classes
- Helpers for expression trees, including functionality to print expressions to source code
  - I do not believe there are any other expression-to-source implementations for C#. There are many projects that can print expressions to stuff that looks like C# code, but they can't create code that you can actually compile.

You can compile it to a DLL and use it separately if you like. Note that the task code in BagoumLib uses the custom ICancellee interface, which is not thread-safe in the way CancellationToken is. 

### Mizuhashi

Mizuhashi is a lightweight combinatorial parser based on FParsec. I wrote this because Unity is really spotty with F# support and none of the C# FParsec clones I could find were up to my taste.

Consumes BagoumLib.

### Suzunoya

Suzunoya is an engine-agnostic visual novel library. It also contains code for state management in ADV games. 

Consumes BagoumLib and Mizuhashi.

### SuzunoyaUnity

[SuzunoyaUnity](https://github.com/Bagoum/suzunoya-unity) (separate repository) is the Suzunoya adapter for the Unity game engine.

Consumes BagoumLib, Mizuhashi, and Suzunoya.

# Licensing

The source code (all subprojects in this repository) is licensed under MIT. See the Suzunoya.LICENSE file for details.

SuzunoyaUnity has a separate license in its repository, but it is also MIT.

