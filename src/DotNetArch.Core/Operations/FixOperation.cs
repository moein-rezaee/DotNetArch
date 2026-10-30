using DotNetArch.Core.Doctor;
using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Restores the standard where the fix cannot change behaviour (D-21). By default only hygiene files are created from the tool's own templates or
/// missing ignore lines appended. Structural fixers (central packages, warnings as errors, Domain test project) run only when named in
/// <c>--rules</c>. Business code is never edited. Plan first; writes only with apply.
/// </summary>
internal static class FixOperation
{
    public static readonly OperationDefinition Definition = new(
        "fix",
        "Fix doctor findings mechanically and without touching source code. Default: missing global.json, .editorconfig, .gitignore lines, .dockerignore. Opt-in via rules: DA-B03 (central package versions, resolved versions unchanged), DA-B07 (warnings are errors in CI/Release), DA-S04 (Domain test project with an architecture test), DA-S06 (layout v2: src/ and tests/), DA-A01 (layout v3: tests/ to test/), DA-A10 (folder tree inside the layers), DA-A12 (folders a profile moves, such as deploy-time data to etc/), DA-A11 (project references between layers, solution and Dockerfile), DA-A02 (ABP layer projects: Domain.Shared, Application.Contracts and HttpApi are created only where files move into them - enum-only files, DTOs and MediatR requests, controllers - with namespaces unchanged and references, packages and Dockerfile restore lines wired; what cannot move is listed as manual; `empty` also creates empty layers). Plan first; writes only with apply. Other findings are listed as manual.",
        OperationKind.Mutating,
        new[]
        {
            new OperationParameter("path", "Repository root (default: current folder).", Positional: true),
            new OperationParameter("profile", "Profile file to apply instead of .net-arch/profile.yml."),
            new OperationParameter("tenancy_profile", "With DA-TN01: the shared profile that lists the tenancy kit; add_tenancy wires it and makes the entities tenant-scoped."),
            new OperationParameter("rules", "Comma-separated rule ids to fix (default: every default-fixable finding; name opt-in rules here)."),
            new OperationParameter("empty", "With DA-A02: also create ABP layer projects that have nothing to receive (default: a layer exists only where files move into it).", ParameterType.Flag),
        },
        Run);

    // idempotent structural rules that can be requested without a doctor finding (the standard may not be switched on yet)
    private static readonly HashSet<string> RequestedWithoutFinding = new(StringComparer.OrdinalIgnoreCase) { "DA-S06", "DA-A01", "DA-A02", "DA-A08", "DA-A09", "DA-A10", "DA-A13", "DA-A11", "DA-A12" };

    private static OperationResult Run(OperationRequest request)
    {
        var root = Path.GetFullPath(request.Get("path") ?? Directory.GetCurrentDirectory());
        var report = DoctorRunner.Run(root, new DoctorOptions(request.Get("profile")));
        var only = request.Get("rules")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var profile = NetArch.NetArchStore.ResolveProfile(root, request.Get("profile"), out _);
        var ctx = new RepoContext(root, profile, new NetArch.RuleSettings());
        var plan = new List<FixAction>();
        var manual = new List<string>();
        var tenancyChanges = new List<PlannedChange>();

        foreach (var finding in report.Findings.Where(f => f.Severity != DoctorSeverity.Info))
        {
            if (only != null && !only.Contains(finding.Id))
                continue;
            if (finding.Id is "DA-BP03" or "DA-BP05")
                continue;
            if (AdapterFixers.RuleIds.Contains(finding.Id))
            {
                if (only == null)
                {
                    manual.Add($"{finding.Id}  {finding.Message}  (opt-in: --rules={finding.Id})");
                    continue;
                }

                var adapterFixes = AdapterFixers.Plan(finding.Id, ctx, manual);
                if (adapterFixes.Count == 0 && !manual.Any(m => m.StartsWith(finding.Id, StringComparison.Ordinal)))
                    manual.Add($"{finding.Id}  {finding.Message}");
                plan.AddRange(adapterFixes);
                continue;
            }

            if (StructuralFixers.RuleIds.Contains(finding.Id))
            {
                if (only == null)
                {
                    manual.Add($"{finding.Id}  {finding.Message}  (opt-in: --rules={finding.Id})");
                    continue;
                }

                var structural = Structural(finding.Id, root, ctx, only?.Contains("DA-B03") == true, request.Flag("empty"), manual, EmptyOnly(request));
                if (structural.Count == 0)
                    manual.Add($"{finding.Id}  {finding.Message}");
                else
                    plan.AddRange(structural);
                continue;
            }

            if (finding.Id == "DA-TN01")
            {
                if (only == null || request.Get("tenancy_profile") is null)
                {
                    manual.Add($"{finding.Id}  {finding.Message}  (opt-in: --rules=DA-TN01 --tenancy_profile=<profile>)");
                    continue;
                }

                var retrofit = OperationRegistry.Execute(TenancyRetrofit.Definition, new Dictionary<string, string> { ["path"] = root, ["profile"] = request.Get("tenancy_profile")! }, request.Apply);
                if (retrofit.Ok)
                    tenancyChanges.AddRange((retrofit.Plan ?? Array.Empty<PlannedChange>()).Select(c => c with { RuleId = finding.Id }));
                else
                    manual.Add($"{finding.Id}  {retrofit.Error}");
                continue;
            }

            if (finding.Id is "DA-M10" or "DA-B10")
            {
                plan.AddRange(finding.Id == "DA-M10" ? LanguageLinkFix(root, finding) : VersionFix(root, finding));
                continue;
            }

            if (finding.Id == "DA-K04")
            {
                plan.AddRange(CorsFix(root, finding, manual));
                continue;
            }

            if (finding.Id == "DA-D03")
            {
                plan.AddRange(NonRootFix(root, finding, manual));
                continue;
            }

            var fix = Fixer(root, finding);
            if (fix == null)
                manual.Add($"{finding.Id}  {finding.Message}");
            else
                plan.Add(fix);
        }

        foreach (var requested in only ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase))
        {
            if (RequestedWithoutFinding.Contains(requested) && !plan.Any(p => p.Change.RuleId?.Equals(requested, StringComparison.OrdinalIgnoreCase) == true) && !report.Findings.Any(f => f.Id.Equals(requested, StringComparison.OrdinalIgnoreCase)))
                plan.AddRange(Structural(requested.ToUpperInvariant(), root, ctx, only!.Contains("DA-B03"), request.Flag("empty"), manual, EmptyOnly(request)));
        }

        if (request.Apply)
            PlanApplier.Apply(root, plan);

        var changes = plan.Select(p => p.Change).ToList();
        changes.AddRange(tenancyChanges);
        changes.AddRange(BlueprintFix(root, report, only, request, manual));
        var lines = changes.Select(c => $"  {c.Action,-7} {c.Path}  [{c.RuleId}] {c.Reason}").ToList();
        var text = $"fix: {(request.Apply ? "applied" : "plan (dry run, add --apply to write)")} - {changes.Count} change(s), {manual.Count} manual finding(s)\n"
                   + string.Join("\n", lines)
                   + (manual.Count == 0 ? string.Empty : "\nmanual (not mechanical, or opt-in):\n" + string.Join("\n", manual.Select(m => "  " + m)));
        return new OperationResult(true, text, new { manual }, changes, request.Apply);
    }

    /// <summary>
    /// DA-BP03 (described but not generated) and DA-BP05 (blueprint not applied at its version) are fixed by applying the blueprint with the same
    /// generators; the blueprint itself is never edited, and a change under an unchanged version is refused and listed as manual.
    /// </summary>
    private static IReadOnlyList<PlannedChange> BlueprintFix(string root, DoctorReport report, HashSet<string>? only, OperationRequest request, List<string> manual)
    {
        var wanted = report.Findings.Where(f => f.Id is "DA-BP01" or "DA-BP03" or "DA-BP05" && (only == null || only.Contains(f.Id))).ToList();
        if (wanted.Count == 0)
            return Array.Empty<PlannedChange>();

        var file = BlueprintFile.FindFile(root);
        if (file is null)
            return Array.Empty<PlannedChange>();

        var result = OperationRegistry.Execute(OperationRegistry.Find("apply_blueprint")!, new Dictionary<string, string> { ["path"] = root, ["blueprint"] = file }, request.Apply);
        if (!result.Ok)
        {
            manual.Add($"{wanted[0].Id}  {result.Error}");
            return Array.Empty<PlannedChange>();
        }

        return (result.Plan ?? Array.Empty<PlannedChange>()).Select(c => c with { RuleId = wanted[0].Id }).ToList();
    }

    /// <summary>The single layer an <c>add_layer</c> call may create empty (internal value, not a declared parameter).</summary>
    private static ProjectLayer? EmptyOnly(OperationRequest request) => request.Get("empty_only") switch
    {
        "Domain.Shared" => ProjectLayer.DomainShared,
        "Application.Contracts" => ProjectLayer.ApplicationContracts,
        "HttpApi" => ProjectLayer.HttpApi,
        "HttpApi.Client" => ProjectLayer.HttpApiClient,
        _ => null,
    };

    internal static IReadOnlyList<FixAction> Structural(string ruleId, string root, RepoContext ctx, bool centralPackages, bool allowEmpty, List<string> manual, ProjectLayer? emptyOnly = null)
    {
        if (ruleId.Equals("DA-A08", StringComparison.OrdinalIgnoreCase))
        {
            var client = TypedClient.Plan(root, ctx, out var skipped);
            manual.AddRange(skipped);
            return client;
        }

        if (ruleId.Equals("DA-A11", StringComparison.OrdinalIgnoreCase))
        {
            var references = LayerReferences.Plan(root, ctx, out var unresolved);
            manual.AddRange(unresolved);
            return references;
        }

        if (!ruleId.Equals("DA-A02", StringComparison.OrdinalIgnoreCase))
            return StructuralFixers.For(ruleId, root, ctx, centralPackages);
        var actions = LayerMigration.Plan(root, ctx, allowEmpty, out var left, emptyOnly);
        manual.AddRange(left.Select(m => $"DA-A02  {m}"));
        return actions;
    }

    /// <summary>DA-M10: the missing language link goes on the first line.</summary>
    private static IEnumerable<FixAction> LanguageLinkFix(string root, DoctorFinding finding)
    {
        foreach (var relative in finding.Details ?? Array.Empty<string>())
        {
            var file = Path.Combine(root, relative);
            var name = Path.GetFileName(relative);
            var counterpart = relative.EndsWith(".fa.md", StringComparison.OrdinalIgnoreCase) ? name[..^6] + ".md" : name[..^3] + ".fa.md";
            var link = relative.EndsWith(".fa.md", StringComparison.OrdinalIgnoreCase) ? $"[English](./{counterpart})" : $"[فارسی](./{counterpart})";
            var text = File.ReadAllText(file);
            yield return new FixAction(new PlannedChange(relative, "modify", "link to the other language", finding.Id), link + "\n\n" + text);
        }
    }

    /// <summary>DA-B10: the Api project gets <c>&lt;Version&gt;1.0.0&lt;/Version&gt;</c> in its first property group.</summary>
    private static IEnumerable<FixAction> VersionFix(string root, DoctorFinding finding)
    {
        foreach (var relative in finding.Details ?? Array.Empty<string>())
        {
            var text = File.ReadAllText(Path.Combine(root, relative));
            var group = System.Text.RegularExpressions.Regex.Match(text, @"<PropertyGroup>", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!group.Success)
                continue;

            var after = group.Index + group.Length;
            var multiLine = text.AsSpan(after).TrimStart(" \t").StartsWith("\n") || text.AsSpan(after).TrimStart(" \t").StartsWith("\r");
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            yield return new FixAction(new PlannedChange(relative, "modify", "the service version (a release raises it)", finding.Id), text.Insert(after, multiLine ? newline + "    <Version>1.0.0</Version>" : "<Version>1.0.0</Version>"));
        }
    }

    /// <summary>
    /// DA-K04: a host that allows any origin allows only the origins listed in <c>Cors:AllowedOrigins</c> (an empty list allows none, like a generated
    /// service); the key is added to the appsettings of the host. Only a top-level <c>Program.cs</c> that registers CORS on <c>builder.Services</c> is
    /// changed; any other shape stays a manual finding.
    /// </summary>
    private static IEnumerable<FixAction> CorsFix(string root, DoctorFinding finding, List<string> manual)
    {
        foreach (var relative in finding.Details ?? Array.Empty<string>())
        {
            var file = Path.IsPathRooted(relative) ? relative : Path.Combine(root, relative);
            var text = File.ReadAllText(file);
            if (!text.Contains("builder.Services.AddCors", StringComparison.Ordinal) || !System.Text.RegularExpressions.Regex.IsMatch(text, @"\.AllowAnyOrigin\(\)", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                manual.Add($"{finding.Id}  {relative}: CORS is not registered on builder.Services in this file, list the origins by hand");
                continue;
            }

            var fixedText = text.Replace(".AllowAnyOrigin()", ".WithOrigins(builder.Configuration.GetSection(\"Cors:AllowedOrigins\").Get<string[]>() ?? Array.Empty<string>())", StringComparison.Ordinal);
            yield return new FixAction(new PlannedChange(Path.GetRelativePath(root, file).Replace('\\', '/'), "modify", "CORS allows the origins of Cors:AllowedOrigins instead of any origin", finding.Id), fixedText);

            foreach (var settings in new[] { "appsettings.json", "appsettings.example.json" })
            {
                var path = Path.Combine(Path.GetDirectoryName(file)!, settings);
                if (!File.Exists(path))
                    continue;
                var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                if (json["Cors"] is System.Text.Json.Nodes.JsonObject cors && cors["AllowedOrigins"] is not null)
                    continue;
                if (json["Cors"] is not System.Text.Json.Nodes.JsonObject section)
                    json["Cors"] = section = new System.Text.Json.Nodes.JsonObject();
                section["AllowedOrigins"] = new System.Text.Json.Nodes.JsonArray();
                yield return new FixAction(new PlannedChange(Path.GetRelativePath(root, path).Replace('\\', '/'), "modify", "Cors:AllowedOrigins (empty: no cross-origin caller until origins are listed)", finding.Id), json.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
            }
        }
    }

    /// <summary>
    /// DA-D03: the final stage of a Dockerfile built on a .NET runtime image runs as the image's non-root <c>app</c> user (<c>$APP_UID</c>, set by the
    /// image since .NET 8). The line goes before the first ENTRYPOINT or CMD of the last stage; any other base image stays a manual finding.
    /// </summary>
    private static IEnumerable<FixAction> NonRootFix(string root, DoctorFinding finding, List<string> manual)
    {
        foreach (var relative in finding.Details ?? Array.Empty<string>())
        {
            var file = Path.IsPathRooted(relative) ? relative : Path.Combine(root, relative);
            var lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n').ToList();
            var from = lines.FindLastIndex(l => l.TrimStart().StartsWith("FROM ", StringComparison.OrdinalIgnoreCase));
            var start = lines.FindIndex(Math.Max(from, 0), l => l.TrimStart().StartsWith("ENTRYPOINT", StringComparison.OrdinalIgnoreCase) || l.TrimStart().StartsWith("CMD", StringComparison.OrdinalIgnoreCase));
            if (from < 0 || start < 0 || !lines[from].Contains("dotnet/aspnet", StringComparison.OrdinalIgnoreCase) && !lines[from].Contains("dotnet/runtime", StringComparison.OrdinalIgnoreCase))
            {
                manual.Add($"{finding.Id}  {relative}: the final stage is not a .NET runtime image, add a non-root USER by hand");
                continue;
            }

            lines.Insert(start, "USER $APP_UID");
            yield return new FixAction(new PlannedChange(Path.GetRelativePath(root, file).Replace('\\', '/'), "modify", "run as the non-root app user of the .NET runtime image", finding.Id), string.Join("\n", lines));
        }
    }

    /// <summary>The first controller of the service (in the Api or the HttpApi project): its assembly is what the snapshot host serves.</summary>
    private static (string Name, string Namespace)? FindController(string root, string app)
    {
        foreach (var project in new[] { $"{app}.Api", $"{app}.HttpApi" })
        {
            var folder = Path.Combine(root, "src", project);
            if (!Directory.Exists(folder))
                continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            {
                var text = File.ReadAllText(file);
                var type = System.Text.RegularExpressions.Regex.Match(text, @"public\s+(?:sealed\s+|partial\s+|abstract\s+)*class\s+(?<name>\w+Controller)\s*:\s*ControllerBase", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                var space = System.Text.RegularExpressions.Regex.Match(text, @"^namespace\s+(?<ns>[\w.]+)\s*[;{]", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                if (type.Success && space.Success)
                    return (type.Groups["name"].Value, space.Groups["ns"].Value);
            }
        }

        return null;
    }

    private static FixAction? Fixer(string root, DoctorFinding finding)
    {
        switch (finding.Id)
        {
            case "DA-B01":
                var sdk = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).Take(50).Any(f => File.ReadAllText(f).Contains("net9.0", StringComparison.Ordinal)) ? "9.0.100" : "8.0.100";
                return new FixAction(new PlannedChange("global.json", "create", "pin the SDK version", finding.Id),
                        TemplateRenderer.RenderTemplate("V2/solution/global.json.tpl", new Dictionary<string, string> { ["SdkVersion"] = sdk }));
            case "DA-B05":
                return new FixAction(new PlannedChange(".editorconfig", "create", "marker without rules (existing code: no style rules are imposed, so formatting checks keep passing)", finding.Id), TemplateRenderer.Load("V2/ops/editorconfig.adopt.tpl"));
            case "DA-B08":
                return new FixAction(new PlannedChange(".gitignore", "create", "ignore build output, IDE files and secrets", finding.Id), TemplateRenderer.Load("V2/solution/.gitignore.tpl"));
            case "DA-B09":
                var current = File.ReadAllText(Path.Combine(root, ".gitignore"));
                var existing = current.Split('\n').Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal);
                var add = new[] { "bin/", "obj/", ".env" }.Where(l => !existing.Contains(l) && !existing.Contains("**/" + l) && !existing.Contains("/" + l) && !existing.Contains(l.TrimEnd('/'))).ToList();
                return add.Count == 0 ? null : new FixAction(new PlannedChange(".gitignore", "modify", $"append: {string.Join(", ", add)}", finding.Id), current.TrimEnd('\n') + "\n" + string.Join("\n", add) + "\n");
            case "DA-BP01":
                // The service was generated before it had a blueprint: derive one from dotnet-arch.yml and the generated code.
                var solution = Config.ConfigManager.Load(root);
                if (solution is null || !solution.IsV2 || File.Exists(Path.Combine(root, BlueprintFile.DefaultPath)))
                    return null;
                solution.SolutionPath = root;
                return new FixAction(new PlannedChange(BlueprintFile.DefaultPath, "create", "blueprint derived from the generated service (review it; its version starts at 1.0.0)", finding.Id), BlueprintDeriver.Derive(solution));
            case "DA-M09":
                // The contract snapshot is written by a test (UPDATE_OPENAPI_SNAPSHOT=1 dotnet test), so the document comes from the running API, not from a guess.
                var apiTests = Directory.EnumerateDirectories(root, "*.Api.Tests", SearchOption.AllDirectories).FirstOrDefault(d => !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
                if (apiTests is null)
                    return null;
                var testFile = Path.GetRelativePath(root, Path.Combine(apiTests, "OpenApiSnapshotTests.cs")).Replace('\\', '/');
                if (File.Exists(Path.Combine(root, testFile)))
                    return null;
                var app = Path.GetFileName(apiTests)[..^".Api.Tests".Length];
                if (File.Exists(Path.Combine(apiTests, "Support", "ApiFactory.cs")))
                    return new FixAction(new PlannedChange(testFile, "create", "OpenAPI snapshot test; then run UPDATE_OPENAPI_SNAPSHOT=1 dotnet test to write docs/api/openapi.json", finding.Id),
                            TemplateRenderer.RenderTemplate("V2/ops/openapi-snapshot-tests.cs.tpl", new Dictionary<string, string> { ["App"] = app }));

                // A service whose tests have no in-process host of the whole application: a small host over its controllers makes the document.
                var controller = FindController(root, app);
                if (controller is null)
                    return null;
                return new FixAction(new PlannedChange(testFile, "create", "OpenAPI snapshot test over a host of the controllers; then run UPDATE_OPENAPI_SNAPSHOT=1 dotnet test to write docs/api/openapi.json", finding.Id),
                        TemplateRenderer.RenderTemplate("V2/ops/openapi-snapshot-tests.hosted.cs.tpl", new Dictionary<string, string>
                        {
                            ["App"] = app,
                            ["Controller"] = controller.Value.Name,
                            ["ControllerUsing"] = $"using {controller.Value.Namespace};",
                        }));
            case "DA-G01":
                // The generators read the shape of the service from dotnet-arch.yml; a service that was only adopted gets it from what is on disk.
                var derived = ConfigDeriver.Derive(root);
                if (derived is null)
                    return null;
                var staging = Path.Combine(Path.GetTempPath(), "dotnet-arch-config-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                try
                {
                    Config.ConfigManager.Save(staging, derived);
                    return new FixAction(new PlannedChange("dotnet-arch.yml", "create", "the shape of the service, read from the solution, the Api project, the packages and the Dockerfile", finding.Id), File.ReadAllText(Path.Combine(staging, "dotnet-arch.yml")));
                }
                finally
                {
                    Directory.Delete(staging, recursive: true);
                }
            case "DA-D04":
                return new FixAction(new PlannedChange(".dockerignore", "create", "keep host bin/obj out of the build context", finding.Id), TemplateRenderer.Load("V2/ops/dockerignore.tpl"));
            default:
                return null;
        }
    }
}
