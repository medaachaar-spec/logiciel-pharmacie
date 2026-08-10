using Microsoft.Data.SqlClient;

namespace Pharmacy.Infrastructure.Reporting;

internal static class ReportingSqlFailure
{
    public static bool IsDatabaseUnavailable(Exception exception)
    {
        var current = exception;
        while (current is not null)
        {
            if (current is SqlException sql)
                return sql.Number is -2 or 2 or 20 or 40 or 53 or 64 or 233 or 4060 or 10054 or 18456;
            current = current.InnerException;
        }
        return exception is InvalidOperationException;
    }
}
