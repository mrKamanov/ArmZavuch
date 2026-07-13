using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

var dll = args[0];
var typeName = args[1];
var output = args[2];
var decompiler = new CSharpDecompiler(dll, new DecompilerSettings(LanguageVersion.Latest));
var code = decompiler.DecompileTypeAsString(new FullTypeName(typeName));
await File.WriteAllTextAsync(output, code, Encoding.UTF8);
Console.WriteLine($"Wrote {output} ({code.Length} chars)");
