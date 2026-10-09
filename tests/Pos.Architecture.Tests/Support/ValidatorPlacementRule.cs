namespace Pos.Architecture.Tests.Support;

public static class ValidatorPlacementRule
{
    public static IReadOnlyList<string> ValidatePlacement(Type targetType)
    {
        var failures = new List<string>();

        var declaringFiles = SourceTreeIndex.GetDeclaringFilesForType(targetType.Name);
        if (declaringFiles.Count == 0)
        {
            failures.Add($"No se encontró el archivo de código fuente que declara el tipo '{targetType.Name}'.");
            return failures;
        }

        if (declaringFiles.Count > 1)
        {
            var fileList = string.Join(", ", declaringFiles.Select(f => Path.GetFileName(f.FilePath)));
            failures.Add($"El tipo '{targetType.Name}' está declarado en múltiples archivos: {fileList}.");
            return failures;
        }

        var declaringFile = declaringFiles.Single();
        var expectedValidatorName = $"{targetType.Name}Validator";

        var valDeclaringFiles = SourceTreeIndex.GetDeclaringFilesForType(expectedValidatorName);
        if (valDeclaringFiles.Count == 0)
        {
            failures.Add($"No se encontró la clase validadora '{expectedValidatorName}' en Pos.Application.");
            return failures;
        }

        if (valDeclaringFiles.Count > 1)
        {
            var fileList = string.Join(", ", valDeclaringFiles.Select(f => Path.GetFileName(f.FilePath)));
            failures.Add($"El validador '{expectedValidatorName}' está declarado en múltiples archivos: {fileList}.");
            return failures;
        }

        var valDeclaringFile = valDeclaringFiles.Single();
        var actualFileName = Path.GetFileName(valDeclaringFile.FilePath);
        var expectedFileName = $"{expectedValidatorName}.cs";

        if (!actualFileName.Equals(expectedFileName, StringComparison.Ordinal))
        {
            failures.Add($"El validador '{expectedValidatorName}' no está en su archivo propio '{expectedFileName}' (se encontró en '{actualFileName}').");
        }

        var targetDir = Path.GetDirectoryName(declaringFile.FilePath);
        var valDir = Path.GetDirectoryName(valDeclaringFile.FilePath);

        if (!string.Equals(targetDir, valDir, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"El validador '{expectedValidatorName}' debe estar en la misma carpeta que '{targetType.Name}'.");
        }

        if (valDeclaringFile.DeclaredTypes.Count != 1)
        {
            failures.Add($"El archivo de validador '{valDeclaringFile.FilePath}' declara {valDeclaringFile.DeclaredTypes.Count} tipos (se requiere exactamente 1).");
        }

        return failures;
    }
}
