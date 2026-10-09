namespace DotNetArch.Core.Operations;

/// <summary>
/// One concrete change a fixer wants to make: the planned file change, its new content and an optional step run after the files are written.
/// With <see cref="MoveFrom"/> the change is a folder move (done before any content is written; <see cref="Content"/> is then empty).
/// </summary>
internal sealed record FixAction(PlannedChange Change, string Content, Action? AfterWrite = null, string? MoveFrom = null);
