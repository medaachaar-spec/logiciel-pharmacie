using System.Text.Json;
using Pharmacy.Infrastructure.Deployment;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 1 && args[0] == "version")
    {
        Console.WriteLine(PilotReleaseInfo.Version);
        return 0;
    }
    if (args.Length == 0)
    {
        PrintUsage();
        return 64;
    }

    try
    {
        var command = args[0].ToLowerInvariant();
        var config = PilotConfiguration.Load(GetOption(args, "--config"));
        var manager = new PilotDeploymentManager(config);
        switch (command)
        {
            case "preflight":
                var checks = await manager.PreflightAsync();
                foreach (var check in checks) Console.WriteLine($"{(check.Passed ? "PASS" : "FAIL")} | {check.Name} | {check.Detail}");
                return checks.All(x => x.Passed) ? 0 : 2;
            case "install-or-upgrade":
                var outcome = await manager.InstallOrUpgradeAsync();
                Console.WriteLine(JsonSerializer.Serialize(outcome, new JsonSerializerOptions { WriteIndented = true }));
                return outcome.Passed ? 0 : 3;
            case "health":
                var health = await manager.HealthAsync();
                Console.WriteLine(JsonSerializer.Serialize(health, new JsonSerializerOptions { WriteIndented = true }));
                return health.Passed ? 0 : 4;
            case "support-bundle":
                var output = GetRequiredOption(args, "--output");
                Console.WriteLine(await manager.CreateSupportBundleAsync(output));
                return 0;
            case "rollback":
                await manager.RollbackAsync(GetRequiredOption(args, "--backup"), GetRequiredOption(args, "--confirm-database"));
                Console.WriteLine("Rollback restauré et terminé.");
                return 0;
            default:
                PrintUsage();
                return 64;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"DEPLOYMENT_ERROR | {ex.GetType().Name} | {ex.Message}");
        return 1;
    }
}

static string? GetOption(string[] args, string name)
{
    var index = Array.FindIndex(args, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static string GetRequiredOption(string[] args, string name) =>
    GetOption(args, name) ?? throw new ArgumentException($"Option requise : {name}");

static void PrintUsage() => Console.WriteLine(
    "Pharmacy.Deployment <preflight|install-or-upgrade|health|support-bundle|rollback|version> [options]");
