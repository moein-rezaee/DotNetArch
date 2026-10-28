using DotNetArch.Core.Operations;

namespace DotNetArch.Core.Doctor;

/// <summary>
/// What the generators need from a service (DA-G01) and the multi-tenant standard (DA-TN01, opt-in with <c>adopt --standards=tenancy</c>): every entity
/// the EF context persists belongs to a tenant. The tool names no kit; the fix takes the tenancy profile from the caller.
/// </summary>
internal static class ToolChecks
{
    public static void Run(RepoContext ctx)
    {
        const string cat = "tool";
        if (!ctx.Has("dotnet-arch.yml") && ConfigDeriver.Derive(ctx.Root) is not null)
            ctx.Check("DA-G01", cat, false, DoctorSeverity.Warning, "dotnet-arch.yml is missing: the generators (new_entity, apply_blueprint, apply_profile, add_tenancy) read the shape of the service from it.", hint: "dotnet-arch fix --rules=DA-G01 --apply writes it from the solution, the Api project, the packages and the Dockerfile.");

        if (!ctx.Standards.Contains("tenancy"))
            return;

        var model = TenancyModel.Find(ctx.Root, out var problem);
        if (model is null)
        {
            ctx.Check("DA-TN01", cat, false, DoctorSeverity.Warning, "The tenancy standard is on but the entities cannot be read: " + problem, hint: "A service with one DbContext is checked; with more, make each tenant-scoped by hand.");
            return;
        }

        var open = model.Entities.Where(e => !e.Scoped).Select(e => e.Name).ToList();
        ctx.Check("DA-TN01", cat, open.Count == 0, DoctorSeverity.Warning, "Entities that do not belong to a tenant (no ITenantOwned).", hint: "dotnet-arch fix --rules=DA-TN01 --tenancy_profile=<profile with the tenancy kit> --apply (runs add_tenancy); list shared ones in rules.yml as an exception.", details: open);
    }
}
