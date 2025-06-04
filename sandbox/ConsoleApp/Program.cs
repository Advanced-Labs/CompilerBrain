using CompilerBrain;
using System.Text.Json;

var memory = new SessionMemory();

var id = CSharpMcpServer.Initialize(memory);


var diagnostics = await CSharpMcpServer.OpenCsharpProject(memory, id, @"C:\ZLinq\src\ZLinq\ZLinq.csproj");


//var list = new List<CodeStructure>();
//var page = 0;
//CodeStructure codeStructure = default!;
//do
//{
//    page++;
//    Console.WriteLine("Read Page:" + page);
//    var structure = CSharpMcpServer.GetCodeStructure(memory, id, page);
//    list.Add(structure);
//    codeStructure = structure;
//} while (codeStructure.TotalPage != page);


//Console.WriteLine("foo");


var result = CSharpMcpServer.AddOrReplaceCode(memory, id, new[] {
    new Codes
    {
        FilePath =  @"C:\ZLinq\src\ZLinq\Test.cs",
        Code = """
namespace ZLinq;

public class Test
{
}
"""
    }
});
