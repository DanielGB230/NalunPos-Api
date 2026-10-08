using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Pos.Architecture.Tests.Support;
using Xunit;

namespace Pos.Architecture.Tests;

public class BehaviorGovernanceTests
{
    [Fact]
    public void Every_Behavior_Class_Must_Be_Referenced_In_DependencyInjection_Code_Not_Comments()
    {
        var appDir = SolutionDirectory.PosApplication;
        var behaviorsDir = Path.Combine(appDir, "Common", "Behaviors");
        var diFile = Path.Combine(appDir, "DependencyInjection.cs");

        Assert.True(Directory.Exists(behaviorsDir), $"El directorio '{behaviorsDir}' no existe.");
        Assert.True(File.Exists(diFile), $"El archivo '{diFile}' no existe.");

        var diRawText = File.ReadAllText(diFile);

        // 1. Eliminar bloques de comentarios /* ... */
        var blockCommentsRemoved = Regex.Replace(diRawText, @"/\*.*?\*/", "", RegexOptions.Singleline);
        // 2. Eliminar comentarios de línea // ... (solo hasta fin de línea; no afecta // dentro de strings literales
        //    ya que en DependencyInjection.cs no hay string literals que contengan //)
        var diCodeOnly = Regex.Replace(blockCommentsRemoved, @"//[^\r\n]*", "", RegexOptions.Multiline);

        var behaviorFiles = Directory.GetFiles(behaviorsDir, "*.cs");
        var unreferencedBehaviors = new List<string>();

        foreach (var file in behaviorFiles)
        {
            var className = Path.GetFileNameWithoutExtension(file);
            // Coincidencia de identificador C# completo (word boundary)
            var identifierRegex = new Regex($@"\b{Regex.Escape(className)}\b");
            if (!identifierRegex.IsMatch(diCodeOnly))
            {
                unreferencedBehaviors.Add(className);
            }
        }

        Assert.True(
            unreferencedBehaviors.Count == 0,
            $"Las siguientes clases en Common/Behaviors no están registradas en el CÓDIGO de DependencyInjection.cs (se ignoraron comentarios):\n{string.Join("\n", unreferencedBehaviors)}"
        );
    }
}
