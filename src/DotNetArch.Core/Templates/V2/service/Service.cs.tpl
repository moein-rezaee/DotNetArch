namespace {{Namespace}};

/// <summary>Business service: logic that belongs to the Application layer but not to a single use case. Depend on this interface, never on the class.</summary>
public interface I{{ServiceName}}
{
    // TODO: declare the operations of this service.
}

internal sealed class {{ServiceName}} : I{{ServiceName}}
{
    // TODO: implement the operations. Inject ports (IUnitOfWork, kit abstractions) through the constructor.
}
