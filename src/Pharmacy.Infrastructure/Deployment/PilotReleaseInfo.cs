using System.Reflection;

namespace Pharmacy.Infrastructure.Deployment;

public static class PilotReleaseInfo
{
    public const string Channel = "Release Candidate pilote";

    public static string Version
    {
        get
        {
            var value = typeof(PilotReleaseInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            return string.IsNullOrWhiteSpace(value)
                ? "inconnue"
                : value.Split('+', 2)[0];
        }
    }
}
