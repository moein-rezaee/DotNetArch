using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace DotNetArch.Core.Scaffolding.Solution;

/// <summary>Dockerfile / docker-compose generation and docker resource probing.</summary>
public static class DockerSupport
{
    public static void CreateArtifacts(string basePath, string solutionName, string startupProject, string port)
    {
        WriteDockerfile(basePath, startupProject, port);
        RefreshCompose(basePath, solutionName, startupProject, port, "development");
    }

    public static void WriteDockerfile(string basePath, string startupProject, string port)
    {
        var dockerfilePath = Path.Combine(basePath, "Dockerfile");
        if (File.Exists(dockerfilePath)) return;
        var nl = Environment.NewLine;
        var content =
            $"FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base{nl}" +
            "WORKDIR /app" + nl +
            $"EXPOSE {port}" + nl +
            nl +
            "FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build" + nl +
            "WORKDIR /src" + nl +
            "COPY . ." + nl +
            $"RUN dotnet restore \"{startupProject}/{startupProject}.csproj\"" + nl +
            $"WORKDIR /src/{startupProject}" + nl +
            $"RUN dotnet publish \"{startupProject}.csproj\" -c Release -o /app/publish /p:UseAppHost=false" + nl +
            nl +
            "FROM base AS final" + nl +
            "WORKDIR /app" + nl +
            "COPY --from=build /app/publish ." + nl +
            $"ENTRYPOINT [\"dotnet\", \"{startupProject}.dll\"]" + nl;
        File.WriteAllText(dockerfilePath, content);
        ToolHost.Success("Dockerfile created.");
    }

    public static void RefreshCompose(string basePath, string solutionName, string startupProject, string port, string env)
    {
        var composePath = Path.Combine(basePath, "docker-compose.yml");
        var envPath = Path.Combine(basePath, startupProject, "config", "env", $".env.{env}");
        var envHash = File.Exists(envPath) ? ComputeHash(File.ReadAllText(envPath)) : string.Empty;
        var image = $"{solutionName.ToLower()}.api";
        var container = $"{solutionName.ToLower()}-api";
        var nl = Environment.NewLine;
        var content =
            $"# env-hash:{envHash}{nl}" +
            "services:" + nl +
            "  api:" + nl +
            "    build:" + nl +
            "      context: ." + nl +
            "      dockerfile: Dockerfile" + nl +
            $"    image: {image}:0.0.0{nl}" +
            $"    container_name: {container}{nl}" +
            "    ports:" + nl +
            $"      - \"{port}:{port}\"{nl}" +
            "    environment:" + nl +
            $"      - ASPNETCORE_URLS=http://+:{port}{nl}" +
            $"      - ASPNETCORE_ENVIRONMENT=${{ASPNETCORE_ENVIRONMENT:-{env}}}{nl}" +
            "    env_file:" + nl +
            $"      - {startupProject}/config/env/.env.${{ASPNETCORE_ENVIRONMENT:-{env}}}{nl}";
        if (!File.Exists(composePath) || File.ReadAllText(composePath) != content)
        {
            File.WriteAllText(composePath, content);
            ToolHost.Success("docker-compose.yml updated.");
        }
    }

    public static string ComputeHash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static bool IsPortInUse(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    public static bool ContainerExists(string name)
    {
        var (_, output) = ToolHost.RunCommandCapture($"docker ps -a --filter name={name} --format \"{{{{.Names}}}}\"");
        return !string.IsNullOrWhiteSpace(output.Trim());
    }

    public static bool ImageExists(string name)
    {
        var (_, output) = ToolHost.RunCommandCapture($"docker images -q {name}");
        return !string.IsNullOrWhiteSpace(output.Trim());
    }
}
