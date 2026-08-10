using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Operations.Contracts;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Operations;

public sealed class SimulatedReceiptPrinter : IReceiptPrinter
{
    private readonly IBusinessAuditRecorder audit;
    private int attempts;
    public SimulatedReceiptPrinter(IBusinessAuditRecorder audit) => this.audit = audit;

    public async Task<Result<ReceiptPrintResult>> PrintAsync(ReceiptPrintRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SaleId <= 0 || string.IsNullOrWhiteSpace(request.SaleReference))
        {
            try { await audit.RecordFailureAsync("devices", "Sale", request.SaleId > 0 ? request.SaleId.ToString() : null, "receipt.print.simulated", request.OperationId, "printing.sale.not_persisted", cancellationToken); } catch { }
            return Result<ReceiptPrintResult>.Failure(new Error("printing.sale.not_persisted", "Une vente SQL persistée est requise avant impression."));
        }
        attempts++;
        await audit.RecordSuccessAsync("devices", "Sale", request.SaleId.ToString(), request.IsReprint ? "receipt.reprint.simulated" : "receipt.print.simulated", request.OperationId, cancellationToken);
        return Result<ReceiptPrintResult>.Success(new ReceiptPrintResult(true, "Simulated80mm", null, attempts));
    }
}

public sealed class ManualPaymentTerminal : IManualPaymentTerminal
{
    public Task<Result<ManualTerminalResult>> ConfirmAsync(ManualTerminalOutcome outcome, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<ManualTerminalResult>.Success(new ManualTerminalResult(outcome, $"Manual{outcome}", DateTime.UtcNow)));
}

public sealed class LocalDeviceDiagnostics : IDeviceDiagnostics
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IPermissionAuthorizer authorizer;
    private readonly IBusinessAuditRecorder audit;
    public LocalDeviceDiagnostics(IDbContextFactory<PharmacyDbContext> contextFactory, IPermissionAuthorizer authorizer, IBusinessAuditRecorder audit) { this.contextFactory = contextFactory; this.authorizer = authorizer; this.audit = audit; }

    public async Task<Result<DeviceDiagnosticView>> CheckAsync(CancellationToken cancellationToken = default)
    {
        var denied = authorizer.Authorize(SecurityPermissions.DevicesConfigure, true);
        if (denied.IsFailure)
        {
            try { await audit.RecordFailureAsync("devices", "Workstation", null, "devices.diagnostics", null, denied.Error.Code, cancellationToken); } catch { }
            return Result<DeviceDiagnosticView>.Failure(denied.Error);
        }
        var items = new List<DeviceDiagnosticItem>();
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var reachable = await context.Database.CanConnectAsync(cancellationToken);
            items.Add(new("SQL Server", reachable ? "Disponible" : "Indisponible", context.Database.GetDbConnection().Database, reachable));
        }
        catch { items.Add(new("SQL Server", "Indisponible", "Connexion contrôlée en échec", false)); }
        var localBackup = Environment.GetEnvironmentVariable("PHARMACY_BACKUP_LOCAL_DIRECTORY");
        var external = Environment.GetEnvironmentVariable("PHARMACY_BACKUP_EXTERNAL_DIRECTORY");
        items.Add(new("Sauvegarde locale", string.IsNullOrWhiteSpace(localBackup) ? "Chemin SQL par défaut" : Directory.Exists(localBackup) ? "Disponible" : "À créer", localBackup ?? "InstanceDefaultBackupPath", true));
        items.Add(new("Support externe", !string.IsNullOrWhiteSpace(external) && Directory.Exists(external) ? "Disponible" : "Non configuré/absent", external ?? "Aucun chemin", !string.IsNullOrWhiteSpace(external) && Directory.Exists(external)));
        items.Add(new("Scanner USB HID", "Mode clavier", "Suffixe Enter attendu ; test sans création métier", true));
        items.Add(new("Imprimante ticket", "Simulateur", "80 mm abstrait ; certification physique en attente", true));
        items.Add(new("TPE", "Externe manuel", "Aucune donnée bancaire capturée", true));
        items.Add(new("UPS", "Contrôle manuel", "Aucun SDK de marque", false));
        items.Add(new("Application", "Disponible", typeof(LocalDeviceDiagnostics).Assembly.GetName().Version?.ToString() ?? "non versionnée", true));
        items.Add(new("Runtime", ".NET", RuntimeInformation.FrameworkDescription, true));
        try { await audit.RecordSuccessAsync("devices", "Workstation", null, "devices.diagnostics", null, cancellationToken); } catch { }
        return Result<DeviceDiagnosticView>.Success(new DeviceDiagnosticView(DateTime.UtcNow, items.AsReadOnly()));
    }
}
