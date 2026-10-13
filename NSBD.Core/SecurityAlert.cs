namespace NSBD.Core;

public sealed record SecurityAlert(
    string Level,
    string Title,
    string Description);
