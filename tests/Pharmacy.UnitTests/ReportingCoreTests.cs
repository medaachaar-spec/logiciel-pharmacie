using Pharmacy.Application.Reporting.Errors;
using Pharmacy.Application.Reporting.Exports;
using Pharmacy.Application.Reporting.Models;
using Pharmacy.Application.Security;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class ReportingCoreTests
{
    [Fact]
    public void PeriodRequiresUtcAndAnExclusiveUpperBoundAfterTheStart()
    {
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(new ReportingPeriod(from, from.AddDays(1)).IsValid);
        Assert.False(new ReportingPeriod(from, from).IsValid);
        Assert.False(new ReportingPeriod(DateTime.SpecifyKind(from, DateTimeKind.Local), from.AddDays(1)).IsValid);
        Assert.Equal("reporting.period.invalid", ReportingErrors.InvalidPeriod.Code);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-42", "'-42")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("normal", "normal")]
    [InlineData("", "")]
    public void CsvExportNeutralizesSpreadsheetFormulaPrefixes(string input, string expected)
    {
        Assert.Equal(expected, CsvExportSecurity.NeutralizeFormula(input));
    }

    [Fact]
    public void ReportingPermissionsAreExplicitAndSettingsAreSensitive()
    {
        Assert.Contains(SecurityPermissions.ReportsView, SecurityPermissions.All);
        Assert.Contains(SecurityPermissions.ReportsExport, SecurityPermissions.All);
        Assert.Contains(SecurityPermissions.ReportsManageSettings, SecurityPermissions.All);
        Assert.Contains(SecurityPermissions.ReportsManageSettings, SecurityPermissions.Sensitive);
        Assert.DoesNotContain(SecurityPermissions.ReportsExport, SecurityPermissions.Sensitive);
    }
}
