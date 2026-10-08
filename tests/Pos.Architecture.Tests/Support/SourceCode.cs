using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pos.Architecture.Tests.Support;

public static class SourceCode
{
    public static string StripCommentsAndStrings(string code)
    {
        // Remove single line comments
        var noSingleComments = Regex.Replace(code, @"//.*", "");
        // Remove block comments
        var noComments = Regex.Replace(noSingleComments, @"/\*[\s\S]*?\*/", "");
        // Remove verbatim / double quoted strings
        var noStrings = Regex.Replace(noComments, @"""(?:[^""\\]|\\.)*""", "");
        // Remove character literals
        var noChars = Regex.Replace(noStrings, @"'(?:[^'\\]|\\.)*'", "");

        return noChars;
    }

    public static string? FindTypeFile(string typeName)
    {
        var searchFiles = Directory.GetFiles(SolutionDirectory.PosApplication, "*.cs", SearchOption.AllDirectories);
        return searchFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == typeName);
    }

    public static int CountDeclaredTypes(string rawCode, out List<string> declaredTypeNames)
    {
        var cleanCode = StripCommentsAndStrings(rawCode);

        var pattern = @"(?:\b(?:public|internal|private|protected|file|static|sealed|abstract|partial|readonly|ref)\s+)*(?:class|record\s+struct|record|struct|enum|interface|delegate)\s+([A-Za-z_]\w*)";

        var matches = Regex.Matches(cleanCode, pattern);
        declaredTypeNames = matches.Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Where(name => name != "void" && name != "var")
            .ToList();

        return declaredTypeNames.Count;
    }
}
