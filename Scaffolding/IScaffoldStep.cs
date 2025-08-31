namespace DotNetArch.Scaffolding;

public interface IScaffoldStep
{
    void Execute(SolutionConfig config, string entity);
}
