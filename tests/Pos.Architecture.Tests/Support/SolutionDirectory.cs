using System;
using System.IO;
using System.Linq;

namespace Pos.Architecture.Tests.Support;

public static class SolutionDirectory
{
    private static readonly Lazy<string> SolutionRootPath = new(() =>
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && directory.GetFiles("Pos.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new InvalidOperationException("No se encontró la raíz de la solución que contiene Pos.slnx");
        }

        return directory.FullName;
    });

    public static string Root => SolutionRootPath.Value;
    public static string PosApplication => Path.Combine(Root, "src", "Pos.Application");
    public static string PosApi => Path.Combine(Root, "src", "Pos.Api");
}
