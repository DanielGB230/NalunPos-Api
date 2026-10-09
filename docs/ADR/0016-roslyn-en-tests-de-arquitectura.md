# ADR 0016: Análisis Sintáctico con Roslyn en Tests de Arquitectura

## Contexto

Los tests de gobierno de arquitectura (validadores de comandos/queries en archivo propio y `PaginationRules` como única autoridad) dependían anteriormente del análisis de código fuente en texto plano mediante expresiones regulares (Regex). 

Esta aproximación basada en Regex presentaba fragilidades estructurales:
- Sensible a falsos positivos/negativos por comentarios de código, literales de cadena, cadenas verbatim/raw strings o formato de líneas.
- Incapacidad para analizar de forma robusta clases anidadas, delegados, tipos `file`, registros (`record`, `record struct`) o estructuras de llamadas complejas con expresiones anidadas (ej. `Math.Min(Math.Abs(request.PageSize), 100)`).

## Decisión

Hemos decidido incorporar la librería **`Microsoft.CodeAnalysis.CSharp` (Roslyn)** exclusivamente en el proyecto de pruebas de arquitectura (`tests/Pos.Architecture.Tests`):

1. **Uso Exclusivo en Tests de Arquitectura**: `Microsoft.CodeAnalysis.CSharp` se agrega únicamente a `Pos.Architecture.Tests`. No se introduce en proyectos de código de producción (`src/`) ni en otros proyectos de test.
2. **Gestión Centralizada de Versión**: Declarado en `Directory.Packages.props` en el grupo `Testing — Architecture`.
3. **Versión Elegida**: `4.14.0`, seleccionada como la última versión estable publicada con compatibilidad plena para el SDK de .NET 10 / C# 13/14.
4. **Licencia Verificada**: Licencia MIT oficial de Microsoft registrada en el manifiesto `.nuspec` del paquete.

## Alternativas Descartadas

1. **Expresiones Regulares (Regex)**: Descartadas por su fragilidad sintáctica e inhabilidad para comprender la estructura jerárquica del lenguaje C#.
2. **ArchUnitNET (`TngTech.ArchUnitNET`)**: ArchUnitNET opera mediante reflexión sobre ensamblados compilados en IL, por lo que no permite inspeccionar árboles sintácticos de archivos de código fuente C# ni tokens individuales de identificadores.

## Consecuencias y Límites

### Positivas
- **Análisis Sintáctico Robusto**: Los comentarios, cadenas de texto y variaciones de formato son ignorados automáticamente por el parser de Roslyn (`CSharpSyntaxTree`).
- **Soporte Completo de Construcciones C#**: Identifica con precisión cualquier declaración de tipo (`class`, `struct`, `interface`, `enum`, `record`, `record struct`, `delegate`, declaradores anidados y modificadores como `file`).
- **Pruebas Sintéticas Deterministas**: Permite escribir tests de unidad sintéticos sobre el propio escáner usando cadenas de C#.

### Negativas y Límites Aceptados
- **Análisis Sintáctico (Sin Modelo Semántico)**: La solución analiza únicamente árboles de sintaxis (`SyntaxTree`), sin compilar un `SemanticModel`. Por tanto, no resuelve símbolos o tipos complejos cruzados de forma semántica.
- **Límite en Operadores Condicionales**: El escáner detecta invocaciones a `Math.Min`, `Math.Max`, `Math.Clamp` y el identificador `MaxPageSize`, pero no detecta acotamientos manuales de paginación implementados mediante operadores ternarios (`request.PageSize > 100 ? 100 : request.PageSize`) o condicionales `if`. Este límite se acepta por diseño para mantener la simplicidad y alta velocidad del escáner sintáctico.
