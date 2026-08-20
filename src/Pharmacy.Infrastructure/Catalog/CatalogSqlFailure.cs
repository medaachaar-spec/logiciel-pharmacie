using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Pharmacy.Infrastructure.Catalog;

internal static class CatalogSqlFailure
{
    public static bool IsUniqueViolation(Exception exception) =>
        FindSqlException(exception)?.Number is 2601 or 2627;

    public static bool IsConstraintViolation(Exception exception) =>
        FindSqlException(exception)?.Number == 547;

    public static bool IsDatabaseUnavailable(Exception exception)
    {
        var sql = FindSqlException(exception);
        return ContainsUnavailableException(exception)
            || sql?.Number is -2 or 2 or 20 or 40 or 53 or 64 or 233 or 258 or 4060 or 10054 or 10060 or 18456;
    }

    private static bool ContainsUnavailableException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is InvalidOperationException or TimeoutException
                || exception is Win32Exception { NativeErrorCode: 258 or 10054 or 10060 })
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }

    private static SqlException? FindSqlException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is SqlException sqlException)
            {
                return sqlException;
            }

            exception = exception is DbUpdateException updateException
                ? updateException.InnerException
                : exception.InnerException;
        }

        return null;
    }
}
