using System;
using System.Collections.Generic;
using System.IO;
using FluentValidation;

namespace Pos.Architecture.Tests.Support;

public static class ValidatorPlacementRule
{
    public static List<string> ValidatePlacement(Type targetType, string kindName)
    {
        var failures = new List<string>();

        var validatorType = CqrsTypeCatalog.GetValidatorFor(targetType);
        if (validatorType == null)
        {
            failures.Add($"{kindName} '{targetType.Name}' debe tener exactamente 1 validador que implemente IValidator<{targetType.Name}>, pero se encontraron 0.");
            return failures;
        }

        var expectedValidatorFileName = $"{validatorType.Name}.cs";

        var targetFilePath = SourceCode.FindTypeFile(targetType.Name);
        var validatorFilePath = SourceCode.FindTypeFile(validatorType.Name);

        if (targetFilePath == null)
        {
            failures.Add($"No se encontró el archivo fuente para '{targetType.Name}'.");
            return failures;
        }

        if (validatorFilePath == null)
        {
            failures.Add($"El validador '{validatorType.Name}' de '{targetType.Name}' debe estar en un archivo dedicado '{expectedValidatorFileName}'.");
            return failures;
        }

        var targetDir = Path.GetDirectoryName(targetFilePath);
        var validatorDir = Path.GetDirectoryName(validatorFilePath);

        if (!string.Equals(targetDir, validatorDir, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"El validador '{validatorType.Name}' debe estar en la misma carpeta que '{targetType.Name}' ({targetDir}), pero se encontró en ({validatorDir}).");
        }

        var validatorFileContent = File.ReadAllText(validatorFilePath);
        int typeCount = SourceCode.CountDeclaredTypes(validatorFileContent, out var declaredTypes);

        if (typeCount > 1)
        {
            failures.Add($"El archivo '{expectedValidatorFileName}' declara múltiples tipos ({string.Join(", ", declaredTypes)}). Debe declarar únicamente el validador.");
        }

        return failures;
    }
}
