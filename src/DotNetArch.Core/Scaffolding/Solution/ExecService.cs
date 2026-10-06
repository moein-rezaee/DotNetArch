namespace DotNetArch.Core.Scaffolding.Solution;

public sealed record ExecOptions(bool UseDocker, bool Detach, bool DockerStop);

/// <summary>Runs a generated solution locally or in Docker (<c>exec</c>), keeping wiring and migrations in sync first.</summary>
public static class ExecService
{
    public static void Run(SolutionConfig config, ExecOptions options)
    {
        var solutionPath = config.SolutionPath;
        var (image, container, tag) = DockerNames(config);

        if (options.DockerStop)
        {
            Teardown(solutionPath, container, tag);
            return;
        }

        var hasDatabase = !string.Equals(config.DatabaseProvider, "None", StringComparison.OrdinalIgnoreCase);

        // Legacy layout: keep the generated wiring (unit of work, DI registrations) in sync before running.
        // Layout v2 wires everything through per-layer DI extensions, so nothing needs patching.
        if (!config.IsV2)
        {
            if (hasDatabase)
                ApplyUnitOfWork(config);

            new ProjectUpdateStep().Execute(config, string.Empty);

            // run unit of work step again to apply registrations if DI files were recreated
            if (hasDatabase)
                ApplyUnitOfWork(config);
        }

        // ensure any pending migrations are applied before running (skip in no-db)
        if (hasDatabase)
            MigrationService.UpdateMigrations(config, solutionPath);

        if (options.UseDocker)
            RunInDocker(config, options.Detach, image, container, tag);
        else
            RunLocally(config);
    }

    private static void ApplyUnitOfWork(SolutionConfig config)
    {
        foreach (var entity in config.Entities.Keys)
            new UnitOfWorkStep().Execute(config, entity);
    }

    private static (string Image, string Container, string Tag) DockerNames(SolutionConfig config)
    {
        var image = string.IsNullOrWhiteSpace(config.DockerImage) ? $"{config.SolutionName.ToLower()}.api" : config.DockerImage;
        var container = string.IsNullOrWhiteSpace(config.DockerContainer) ? $"{config.SolutionName.ToLower()}-api" : config.DockerContainer;
        return (image, container, $"{image}:0.0.0");
    }

    private static void Teardown(string solutionPath, string container, string tag)
    {
        ToolHost.Step("Docker Cleanup", "Tearing down containers and images");
        var down = ToolHost.RunCommand("docker compose down", solutionPath);
        ToolHost.SubStep(down, $"Container stopped: {container}");
        ToolHost.SubStep(down, $"Container removed: {container}");
        if (DockerSupport.ImageExists(tag))
        {
            var rm = ToolHost.RunCommand($"docker rmi {tag}", solutionPath);
            ToolHost.SubStep(rm, $"Image removed: {tag}");
        }
        ToolHost.Blank();
    }

    private static void RunLocally(SolutionConfig config)
    {
        var project = config.ProjectFile(config.StartupProject);
        ToolHost.Runner.RunInteractive(new ProcessSpec("dotnet", new[] { "run", "--project", project }, config.SolutionPath));
    }

    private static void RunInDocker(SolutionConfig config, bool detach, string image, string container, string tag)
    {
        var solutionPath = config.SolutionPath;
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "development";
        if (!int.TryParse(config.ApiPort, out var port)) port = 5000;
        if (DockerSupport.IsPortInUse(port))
        {
            ToolHost.Error($"Port {port} is already in use.");
            return;
        }

        DockerSupport.RefreshCompose(solutionPath, config.SolutionName, config.StartupProject, config.ApiPort, env);

        var runCleanup = false;
        var cleaned = false;
        void Cleanup()
        {
            if (cleaned || !runCleanup) return;
            cleaned = true;
            Teardown(solutionPath, container, tag);
        }

        try
        {
            if (DockerSupport.ContainerExists(container) || DockerSupport.ImageExists(tag))
            {
                if (!ToolHost.AskYesNo("Existing Docker resources found. Kill and recreate?", true))
                {
                    ToolHost.Error("Docker run aborted.");
                    return;
                }

                ToolHost.Step("Docker Cleanup", "Removing existing resources");
                if (DockerSupport.ContainerExists(container))
                    ToolHost.SubStep(ToolHost.RunCommand($"docker rm -f {container}"), $"Removed container: {container}");
                if (DockerSupport.ImageExists(tag))
                    ToolHost.SubStep(ToolHost.RunCommand($"docker rmi {tag}"), $"Removed image: {tag}");
                ToolHost.Blank();
            }

            config.DockerImage = image;
            config.DockerContainer = container;
            ConfigManager.Save(solutionPath, config);
            runCleanup = !detach;

            ToolHost.Step("Docker Build", $"Building image {tag}");
            var buildOk = ToolHost.RunCommand($"ASPNETCORE_ENVIRONMENT={env} docker compose build", solutionPath);
            ToolHost.SubStep(buildOk, $"Image built: {tag}");
            ToolHost.Blank();

            ToolHost.Step("Docker Run", $"Starting container {container}");
            var createOk = ToolHost.RunCommand($"ASPNETCORE_ENVIRONMENT={env} docker compose create", solutionPath);
            ToolHost.SubStep(createOk, $"Container created: {container}");
            var startOk = createOk && ToolHost.RunCommand($"ASPNETCORE_ENVIRONMENT={env} docker compose start", solutionPath);
            ToolHost.SubStep(startOk, $"Container started: {container}");
            if (startOk)
            {
                var baseUrl = $"http://localhost:{port}";
                ToolHost.SubStep(true, $"Application running at {baseUrl}");
                if (env.Equals("development", StringComparison.OrdinalIgnoreCase) ||
                    env.Equals("test", StringComparison.OrdinalIgnoreCase))
                    ToolHost.SubStep(true, $"Swagger UI available at {baseUrl}/swagger/index.html");
            }
            ToolHost.Blank();

            if (!detach)
                ToolHost.RunCommand("docker compose logs -f", solutionPath);
        }
        finally
        {
            if (!detach)
            {
                Cleanup();
                ToolHost.Runner.ResetCancel();
            }
        }
    }
}
