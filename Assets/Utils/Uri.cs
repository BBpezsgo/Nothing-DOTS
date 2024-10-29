using System;
using Unity.Entities;

public static class UriExtensions
{
    public static Uri ToUri(this FileId fileId)
        => new(new Uri($"netcode://{fileId.Source.ConnectionId.Value}_{fileId.Source.ConnectionEntity.Index}_{fileId.Source.ConnectionEntity.Version}", UriKind.Absolute), fileId.Name.ToString());

    public static bool TryGetNetcode(this Uri uri, out FileId fileId)
    {
        fileId = default;

        if (uri.Scheme != "netcode")
        { return false; }

        string[] segments = uri.Host.Split('_');

        if (!int.TryParse(segments[0], out int connectionId))
        { return false; }

        if (!int.TryParse(segments[1], out int entityIndex))
        { return false; }

        if (!int.TryParse(segments[2], out int entityVersion))
        { return false; }

        string path = uri.AbsolutePath;
        if (path.StartsWith("/~"))
        { path = path[1..]; }

        if (path.StartsWith('/'))
        { path = path[1..]; }

        fileId = new FileId(path, new NetcodeEndPoint(
            new Unity.NetCode.NetworkId()
            {
                Value = connectionId
            },
            new Entity()
            {
                Index = entityIndex,
                Version = entityVersion,
            }
        ));
        return true;
    }
}
