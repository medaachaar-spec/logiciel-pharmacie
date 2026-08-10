namespace Pharmacy.Application.Reporting.Exports;

public static class CsvExportSecurity
{
    public static string NeutralizeFormula(string? value)
    {
        var safe = value ?? string.Empty;
        return safe.Length > 0 && safe[0] is '=' or '+' or '-' or '@' ? "'" + safe : safe;
    }
}
