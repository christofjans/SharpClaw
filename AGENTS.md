# AGENTS.md

## Project Overview

This is a class library that encompasses the main functionality of a modern AI agent. It provides support for:

* AGENTS.md
* SKILL.md
* support for different models
* MEMORY.md

## Tech Stack

This project uses C# and .NET 10.0.

## Project Structure

- `.\src\SharpClawLib`: Main library code
- `.\src\SharpClawCli`: Command line interface for testing and interacting with the library

## Code Style

- Follow C# conventions as per Microsoft documentation.
- When new-ing an object, use the target-typed new expression where possible.
```cs
MyClass obj = new(); // use this
MyClass obj = new MyClass(); // avoid this
var obj = new MyClass(); // avoid this
```
- When assigning a variable with the return value of a method, use `var` where possible.
```cs
var obj = GetMyClass(); // use this
MyClass obj = GetMyClass(); // avoid this
```
- Use expression-bodied members where possible.
```cs
public int MyProperty => 42; // use this
public int MyProperty { get { return 42; } } // avoid this
```
- Use collection expressions where possible.
```cs
List<int> list = [1,2,3]; // use this
var list = new List<int> { 1, 2, 3 }; // avoid this
```

## Making changes

- Never modify any files in the `.\prompts` folder.

## After making changes

- Always run `dotnet build` after making any changes
- Never commit anything to git
- Do not try to run the application