using Microsoft.Data.SqlClient;

namespace Pharmacy.Infrastructure.Purchasing;

internal static class PurchasingSqlFailure
{
    public static bool IsUnique(Exception exception) => Find(exception)?.Number is 2601 or 2627;
    public static bool IsUnavailable(Exception exception)
    {
        var sql = Find(exception);
        return exception is InvalidOperationException || sql?.Number is -2 or 2 or 20 or 40 or 53 or 64 or 233 or 4060 or 10054 or 18456;
    }
    private static SqlException? Find(Exception? exception)
    {
        while (exception is not null) { if (exception is SqlException sql) return sql; exception = exception.InnerException; }
        return null;
    }
}
