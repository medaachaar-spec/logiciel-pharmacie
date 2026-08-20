using System.Text;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Reporting.Exports;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Models;
using Pharmacy.Application.Security.Services;
using Pharmacy.Infrastructure.Operations;
using Pharmacy.Infrastructure.Persistence;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class ImportGateCoreTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly CsvImportMapping StandardMapping = new(
        "Reference", "Designation", "PPV", "PPH", DefaultCategoryCode: "GEN");

    [Fact]
    public async Task IMP001_CsvUtf8ThreeRowsIsParsedWithoutInventedDataAndTemporaryFileIsDeleted()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Pharmacy_IMP_" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "miniature.csv");
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(path,
                "Reference,Designation,PPV,PPH\r\nQA-IMP-001,Produit QA 1,10.00,7.00\r\nQA-IMP-002,Produit QA 2,20.00,14.00\r\nQA-IMP-003,Produit QA 3,30.00,21.00\r\n",
                new UTF8Encoding(false), TestContext.Current.CancellationToken);
            var result = await PreviewAsync(Path.GetFileName(path), await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
            Assert.True(result.Value.CanConfirm);
            Assert.Equal(3, result.Value.TotalRows);
            Assert.Equal(["Reference", "Designation", "PPV", "PPH"], result.Value.Headers);
            Assert.Collection(result.Value.Rows,
                row => Assert.Equal(("QA-IMP-001", "Produit QA 1", 10.00m, 7.00m), (row.Reference, row.Designation, row.SalePrice, row.PurchasePrice)),
                row => Assert.Equal(("QA-IMP-002", "Produit QA 2", 20.00m, 14.00m), (row.Reference, row.Designation, row.SalePrice, row.PurchasePrice)),
                row => Assert.Equal(("QA-IMP-003", "Produit QA 3", 30.00m, 21.00m), (row.Reference, row.Designation, row.SalePrice, row.PurchasePrice)));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory, false);
        }

        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task IMP002_MappingSupportsOrderCaseWhitespaceAndUnknownColumnsButRejectsAmbiguity()
    {
        var content = Utf8("Ignore, pph , DESIGNATION , reference , ppv\r\nx,7.00,Produit QA,QA-MAP-001,10.00\r\n");
        var mapping = new CsvImportMapping("REFERENCE", "designation", "PPV", "PPH", DefaultCategoryCode: "GEN");
        var result = await PreviewAsync("mapping.csv", content, mapping);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.CanConfirm);
        Assert.Equal(("QA-MAP-001", "Produit QA", 10m, 7m),
            (result.Value.Rows[0].Reference, result.Value.Rows[0].Designation, result.Value.Rows[0].SalePrice, result.Value.Rows[0].PurchasePrice));

        var ambiguous = await PreviewAsync("mapping.csv", content,
            mapping with { SalePriceColumn = "REFERENCE" });
        Assert.Equal("import.mapping.invalid", ambiguous.Error.Code);
    }

    [Fact]
    public async Task IMP005_NonNumericSalePriceIsRejected()
    {
        await AssertBlockingIssueAsync("QA-IMP-BAD-01,PPV invalide,ABC,7.00", "import.amount.invalid");
        await AssertBlockingIssueAsync("QA-IMP-BAD-PPH,PPH invalide,10.00,ABC", "import.amount.invalid");
    }

    [Fact]
    public async Task IMP006_NegativeSalePriceIsRejected()
    {
        var result = await PreviewLineAsync("QA-IMP-BAD-02,Produit négatif,-1.00,0.50");
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.CanConfirm);
        Assert.Contains(result.Value.Issues, issue => issue.Code is "import.amount.negative" or "import.formula.detected");
    }

    [Fact]
    public async Task IMP007_EmptyReferenceIsRejected()
        => await AssertBlockingIssueAsync(",Produit sans référence,10.00,7.00", "import.reference.required");

    [Fact]
    public async Task IMP008_EmptyDesignationIsRejected()
        => await AssertBlockingIssueAsync("QA-IMP-BAD-03,,10.00,7.00", "import.designation.required");

    [Fact]
    public async Task IMP009_TextBeyondSqlLengthIsRejectedBeforePersistence()
    {
        var reference = new string('R', 129);
        var designation = new string('D', 257);
        var result = await PreviewLineAsync($"{reference},{designation},10.00,7.00");
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.CanConfirm);
        Assert.Contains(result.Value.Issues, issue => issue.Code == "import.text.too_long");
    }

    [Fact]
    public async Task IMP010_DecimalBeyondSqlPrecisionOrScaleIsRejectedBeforePersistence()
    {
        var excessiveScale = await PreviewLineAsync("QA-IMP-DEC-01,Scale excessive,10.123456789,7.00");
        var excessivePrecision = await PreviewLineAsync("QA-IMP-DEC-02,Précision excessive,1000000000000000.0000,7.00");

        Assert.False(excessiveScale.Value.CanConfirm);
        Assert.Contains(excessiveScale.Value.Issues, issue => issue.Code == "import.amount.precision");
        Assert.False(excessivePrecision.Value.CanConfirm);
        Assert.Contains(excessivePrecision.Value.Issues, issue => issue.Code == "import.amount.precision");
    }

    [Fact]
    public async Task IMP012_EmptyFileReturnsControlledFailure()
    {
        var result = await PreviewAsync("empty.csv", []);
        Assert.Equal("import.file.invalid", result.Error.Code);
    }

    [Fact]
    public async Task IMP013_HeaderOnlyReturnsControlledFailure()
    {
        var result = await PreviewAsync("header.csv", Utf8("Reference,Designation,PPV,PPH\r\n"));
        Assert.Equal("import.file.invalid", result.Error.Code);
    }

    [Fact]
    public async Task IMP014_MissingRequiredColumnIsRejected()
    {
        var result = await PreviewAsync("missing.csv", Utf8("Reference,PPV\r\nQA-001,10.00\r\n"));
        Assert.Equal("import.mapping.invalid", result.Error.Code);
    }

    [Fact]
    public async Task IMP015_DuplicateHeaderIsRejected()
    {
        var result = await PreviewAsync("duplicate-header.csv", Utf8("Reference,Reference,Designation,PPV\r\nQA-001,QA-002,Produit,10.00\r\n"));
        Assert.Equal("import.mapping.invalid", result.Error.Code);
    }

    [Fact]
    public async Task IMP016_DuplicateReferenceInOneFileIsDeterministicallyRejected()
    {
        var result = await PreviewAsync("duplicate-reference.csv", Utf8(
            "Reference,Designation,PPV,PPH\r\nQA-DUP-001,Premier,10.00,7.00\r\nQA-DUP-001,Second,11.00,8.00\r\n"));
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.CanConfirm);
        Assert.Contains(result.Value.Issues, issue => issue.Code == "import.reference.duplicate_file" && issue.RowNumber == 3);
    }

    [Fact]
    public async Task IMP020_Utf8AccentsAndArabicRemainUncorrupted()
    {
        var result = await PreviewAsync("unicode.csv", Utf8(
            "Reference,Designation,PPV,PPH\r\nU-1,Paracétamol,10,7\r\nU-2,Crème pédiatrique,11,8\r\nU-3,دواء للأطفال,12,9\r\n"));
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value.Rows, row => row.Designation.Contains('�'));
        Assert.Equal(["Paracétamol", "Crème pédiatrique", "دواء للأطفال"], result.Value.Rows.Select(row => row.Designation));
        Assert.Equal("دواء للأطفال", CsvExportSecurity.NeutralizeFormula("دواء للأطفال"));
    }

    [Fact]
    public async Task IMP021_QuotedCommasEscapedQuotesAndNewlinesAreParsedAsSingleCells()
    {
        var result = await PreviewAsync("quoted.csv", Utf8(
            "Reference,Designation,PPV,PPH\r\nQ-1,\"Produit, dosage 500 mg\",10,7\r\nQ-2,\"Crème \"\"Junior\"\"\",11,8\r\nQ-3,\"Sirop\r\npédiatrique\",12,9\r\n"));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(3, result.Value.TotalRows);
        Assert.Equal("Produit, dosage 500 mg", result.Value.Rows[0].Designation);
        Assert.Equal("Crème \"Junior\"", result.Value.Rows[1].Designation);
        Assert.Equal("Sirop\r\npédiatrique", result.Value.Rows[2].Designation);
    }

    [Fact]
    public void IMP022_ExportNeutralizesEqualsFormula() => Assert.Equal("'=1+1", CsvExportSecurity.NeutralizeFormula("=1+1"));

    [Fact]
    public void IMP023_ExportNeutralizesPlusFormula() => Assert.Equal("'+TEST", CsvExportSecurity.NeutralizeFormula("+TEST"));

    [Fact]
    public void IMP024_ExportNeutralizesMinusFormula() => Assert.Equal("'-TEST", CsvExportSecurity.NeutralizeFormula("-TEST"));

    [Fact]
    public void IMP025_ExportNeutralizesAtFormula() => Assert.Equal("'@TEST", CsvExportSecurity.NeutralizeFormula("@TEST"));

    [Fact]
    public void IMP026_ExportNeutralizesFormulaAfterSpacesOrTabWithoutChangingOrdinaryText()
    {
        Assert.Equal("'  =1+1", CsvExportSecurity.NeutralizeFormula("  =1+1"));
        Assert.Equal("'\t=1+1", CsvExportSecurity.NeutralizeFormula("\t=1+1"));
        Assert.Equal("'=REF", CsvExportSecurity.NeutralizeFormula("=REF"));
        Assert.Equal("'+DESIGNATION", CsvExportSecurity.NeutralizeFormula("+DESIGNATION"));
        Assert.Equal("'@FOURNISSEUR", CsvExportSecurity.NeutralizeFormula("@FOURNISSEUR"));
        Assert.Equal("  normal", CsvExportSecurity.NeutralizeFormula("  normal"));
        Assert.Equal("125.50", CsvExportSecurity.NeutralizeFormula("125.50"));
    }

    [Fact]
    public async Task IMP033_HostileLogicalFileNamesAreRejected()
    {
        var content = Utf8("Reference,Designation,PPV,PPH\r\nSAFE-1,Produit,1,1\r\n");
        foreach (var name in new[] { "../catalogue.csv", "..\\catalogue.csv", "bad<>.csv", "bad:name.csv" })
            Assert.Equal("import.file.invalid", (await PreviewAsync(name, content)).Error.Code);
    }

    [Fact]
    public async Task SupportedSeparatorsAreExplicitAndAnUnsupportedDialectIsRejected()
    {
        foreach (var separator in new[] { ',', ';', '\t' })
        {
            var content = Utf8($"Reference{separator}Designation{separator}PPV{separator}PPH\r\nSEP-1{separator}Produit{separator}10{separator}7\r\n");
            Assert.True((await PreviewAsync("separator.csv", content)).IsSuccess);
        }

        var unsupported = await PreviewAsync("pipe.csv", Utf8("Reference|Designation|PPV|PPH\r\nSEP-1|Produit|10|7\r\n"));
        Assert.Equal("import.mapping.invalid", unsupported.Error.Code);
    }

    [Fact]
    public async Task ConfiguredRowLimitAcceptsLimitAndRejectsLimitPlusOne()
    {
        var csv = new StringBuilder("Reference;Designation;PPV;PPH\r\n");
        for (var index = 1; index <= 100_000; index++) csv.Append($"L{index:000000};P;1;1\r\n");
        var atLimit = await PreviewAsync("limit.csv", Utf8(csv.ToString()));
        Assert.True(atLimit.IsSuccess, atLimit.IsFailure ? atLimit.Error.Message : null);
        Assert.Equal(100_000, atLimit.Value.TotalRows);

        csv.Append("L100001;P;1;1\r\n");
        var overLimit = await PreviewAsync("over-limit.csv", Utf8(csv.ToString()));
        Assert.Equal("import.file.invalid", overLimit.Error.Code);
    }

    private static async Task AssertBlockingIssueAsync(string row, string issueCode)
    {
        var result = await PreviewLineAsync(row);
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.CanConfirm);
        Assert.Contains(result.Value.Issues, issue => issue.Code == issueCode);
    }

    private static Task<Pharmacy.Domain.Common.Result<ImportPreviewView>> PreviewLineAsync(string row)
        => PreviewAsync("invalid.csv", Utf8($"Reference,Designation,PPV,PPH\r\n{row}\r\n"));

    private static Task<Pharmacy.Domain.Common.Result<ImportPreviewView>> PreviewAsync(
        string fileName,
        byte[] content,
        CsvImportMapping? mapping = null)
        => CreateService().PreviewAsync(new PreviewImportRequest(
            fileName, content, ControlledImportType.Catalogue, mapping ?? StandardMapping), TestContext.Current.CancellationToken);

    private static SqlControlledImportService CreateService()
    {
        var clock = new FixedClock();
        var session = new CurrentSecuritySession();
        session.Establish(new UserSessionView(
            1800, "owner.imp", "Owner IMP", ["OWNER"],
            new HashSet<string>([SecurityPermissions.ImportView, SecurityPermissions.ImportExecute], StringComparer.Ordinal),
            Now, Now), Now);
        return new SqlControlledImportService(new ThrowingFactory(), new PermissionAuthorizer(session, clock), new NoOpAuditRecorder(), clock);
    }

    private static byte[] Utf8(string value) => new UTF8Encoding(false, true).GetBytes(value);

    private sealed class ThrowingFactory : IDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext() => throw new InvalidOperationException("Preview must not create a SQL context.");
        public Task<PharmacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Preview must not create a SQL context.");
    }

    private sealed class FixedClock : ISecurityClock { public DateTime UtcNow => Now; }
    private sealed class NoOpAuditRecorder : IBusinessAuditRecorder
    {
        public Task RecordSuccessAsync(string module, string entityType, string? entityId, string action, Guid? operationId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
