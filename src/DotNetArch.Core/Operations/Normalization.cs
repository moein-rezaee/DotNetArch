using DotNetArch.Core.Config;
using DotNetArch.Core.Doctor;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Brings generator output in line with the ABP standard by running the tool's own fixers in order: layout v3, layer projects, folder tree,
/// project references, typed client, Compose files. The generators stay layout-agnostic; the standard is applied by one mechanism (D-33).
/// </summary>
internal static class Normalization
{
    private static readonly string[] Rules = { "DA-A01", "DA-A02", "DA-A10", "DA-A11", "DA-A08", "DA-A09", "DA-A12" };

    public static bool IsAbp(string root)
    {
        var config = ConfigManager.Load(root);
        if (config?.IsV3 == true)
            return true;
        return NetArchStore.LoadState(root)?.Standards.Contains("abp", StringComparer.OrdinalIgnoreCase) == true;
    }

    public static void Run(string root, List<string> manual)
    {
        if (!Directory.Exists(root) || !IsAbp(root))
            return;

        foreach (var rule in Rules)
        {
            var profile = NetArchStore.ResolveProfile(root, null, out _);
            var ctx = new RepoContext(root, profile, new RuleSettings(), new[] { AbpChecks.Standard });
            var plan = FixOperation.Structural(rule, root, ctx, centralPackages: false, allowEmpty: false, manual);
            PlanApplier.Apply(root, plan);
        }

        if (ConfigManager.Load(root) is { IsV3: false, IsV2: true } config)
        {
            config.Layout = SolutionConfig.V3Layout;
            ConfigManager.Save(root, config);
        }

        var request = new OperationRequest(new Dictionary<string, string> { ["path"] = root, ["standards"] = AbpChecks.Standard }, Apply: true);
        AdoptOperation.Definition.Run(request);
    }
}
