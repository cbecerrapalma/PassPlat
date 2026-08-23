namespace PassPlat.Aplicacion.Test.Tests.Logging;

/// <summary>
/// Violación contractual de gobernanza de EventName: un valor literal igual a una
/// constante del catálogo CBP.Logging fue usado directamente en el código en lugar
/// de la constante `LoggingEvents.*`. El reporte es accionable: archivo, línea,
/// literal que lo causó y la constante sugerida como reemplazo.
/// </summary>
public sealed record EventNameLiteralViolation(
    string FilePath,
    int Line,
    string LiteralValue,
    string SuggestedConstant)
{
    public override string ToString() =>
        "Logging Event Catalog violation\n" +
        $"File: {FilePath}\n" +
        $"Line: {Line}\n" +
        $"Literal: \"{LiteralValue}\"\n" +
        $"Expected: LoggingEvents.{SuggestedConstant}";
}