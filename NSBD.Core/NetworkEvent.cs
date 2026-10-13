namespace NSBD.Core;

public sealed record NetworkEvent(
    DateTime Timestamp,
    string SourceIp,
    string DestinationIp,
    int Port,
    string Protocol,
    string Status)
{
    public string Service => NetworkServiceCatalog.GetService(Protocol, Port);

    public string DestinationOwner => IpOwnerResolver.Resolve(DestinationIp);
}
