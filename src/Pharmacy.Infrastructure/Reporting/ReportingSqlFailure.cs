using System.ComponentModel;
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
            {
                if (sql.Number is -2 or 2 or 20 or 40 or 53 or 64 or 233 or 258 or 4060 or 10054 or 10060 or 18456)
                    return true;
            }
            if (current is Win32Exception system
                && system.NativeErrorCode is 258 or 10054 or 10060)
                return true;
            if (current is TimeoutException)
                return true;
            current = current.InnerException;
        }
        return exception is InvalidOperationException;
    }
}
