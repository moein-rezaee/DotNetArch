namespace DotNetArch.Scaffolding;

public interface IScaffoldStep
{
    void Execute(string solution, string entity, string provider, string basePath, string startupProject);
}
