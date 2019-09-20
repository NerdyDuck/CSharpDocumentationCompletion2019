# CSharpDocumentationCompletion2019
#### Additional auto-complete items for C# XML documentation in Visual Studio 2019

This project creates an extension (VSIX) for Visual Studio 2019 that adds helpful entries to the auto-complete feature of Visual Studio Intellisense, when editing XML Documentation blocks in C# code files.
This extension adds the same entries as the extension provided by the [Sandcaste Help File Builder project (SHFB)](https://github.com/EWSoftware/SHFB), maintained by Eric Woodruff, which, at the time of writing, has no auto-complete support for Visual Studio 2019 (because Microsoft changed the extension API to make auto-completion *async*).

Examples for the auto-complete items added are
- Macros for various language words like `true` (which is represented as `<see langword="true"/>`), `false`, or `abstract`
- The classic thread safety warning (`<threadsafety static="true" instance="false"/>`)
- References like `<conceptualLink target=""/>`, `<inheritdoc/>` or `<code language="C#" title="" source="..\Path\SourceFile.cs" region="Region Name"/>`
- You can see all items in the source at [/src/CSharpDocumentationCompletionSource.cs](/src/CSharpDocumentationCompletionSource.cs)

#### How to install
You can either download and compile the project yourself (requires the "Visual Studio extension development" workload installed in your Visual Studio 2019), or you download the compiled extension file from the [releases](/releases) page.

#### License
The project is licensed under the [Apache License](LICENSE), Version 2.0.