namespace Evermail.Common.Runtime;

public sealed class EvermailRuntimeOptions
{
    public const string SectionName = "EvermailRuntime";

    public string Mode { get; init; } = EvermailRuntimeMode.Local.ToString();
}


