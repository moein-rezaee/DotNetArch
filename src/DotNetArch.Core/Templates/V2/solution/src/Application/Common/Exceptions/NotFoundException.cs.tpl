namespace {{App}}.Application.Common.Exceptions;

/// <summary>A requested aggregate does not exist. Mapped to a 404 problem response at the API boundary.</summary>
public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.");
