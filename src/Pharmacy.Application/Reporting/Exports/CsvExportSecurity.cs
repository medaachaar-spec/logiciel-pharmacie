namespace Pharmacy.Application.Reporting.Exports;

public static class CsvExportSecurity
{
    public static string NeutralizeFormula(string? value)
    {
        var safe = value ?? string.Empty;
        var firstContent = 0;
        while (firstContent < safe.Length && char.IsWhiteSpace(safe[firstContent])) firstContent++;
        return firstContent < safe.Length && safe[firstContent] is '=' or '+' or '-' or '@' ? "'" + safe : safe;
    }
}
