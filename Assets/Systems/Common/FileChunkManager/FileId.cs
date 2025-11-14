using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct FileId : IEquatable<FileId>
{
    public FixedString128Bytes Name;
    public NetcodeEndPoint Source;

    public FileId(FixedString128Bytes name, NetcodeEndPoint source)
    {
        Name = name;
        Source = source;
    }

    public override readonly int GetHashCode() => HashCode.Combine(Name, Source);
    public override readonly string ToString() => $"{Source} {Name}";
    public override readonly bool Equals(object obj) => obj is FileId other && Equals(other);
    public readonly bool Equals(FileId other) => Name.Equals(other.Name) && Source.Equals(other.Source);

    public static bool operator ==(FileId a, FileId b) => a.Equals(b);
    public static bool operator !=(FileId a, FileId b) => !a.Equals(b);

    readonly Uri GetBaseUri() => Source.IsServer
        ? new($"netcode://0", UriKind.Absolute)
        : new($"netcode://{Source.ConnectionId.Value}.{Source.ConnectionEntity.Index}.{Source.ConnectionEntity.Version}", UriKind.Absolute);

    public Uri ToUri() => new(GetBaseUri(), Name.ToString());

    public static bool FromUri(Uri uri, out FileId fileId)
    {
        fileId = default;

        if (uri.Scheme != "netcode")
        { return false; }

        NetcodeEndPoint netcodeEndPoint;

        if (uri.Host == "0")
        {
            netcodeEndPoint = NetcodeEndPoint.Server;
        }
        else
        {
            string[] segments = uri.Host.Split('.');

            if (!int.TryParse(segments[0], out int connectionId))
            { return false; }

            if (!int.TryParse(segments[1], out int entityIndex))
            { return false; }

            if (!int.TryParse(segments[2], out int entityVersion))
            { return false; }

            netcodeEndPoint = new NetcodeEndPoint(
                new Unity.NetCode.NetworkId()
                {
                    Value = connectionId
                },
                new Entity()
                {
                    Index = entityIndex,
                    Version = entityVersion,
                }
            );
        }

        string path = uri.AbsolutePath;
        if (path.StartsWith("/~"))
        { path = path[1..]; }

        fileId = new FileId(path, netcodeEndPoint);
        return true;
    }
}
