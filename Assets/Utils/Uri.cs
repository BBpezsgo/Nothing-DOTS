using System;
using Unity.Entities;

public static class UriExtensions
{
    public static Uri ToUri(this FileId fileId)
        => new(new Uri($"netcode://{fileId.Source.ConnectionId.Value}", UriKind.Absolute), fileId.Name.ToString());

    public static bool TryGetNetcode(this Uri uri, out FileId fileId)
    {
        fileId = default;

        if (uri.Scheme != "netcode")
        { return false; }

        if (!int.TryParse(uri.Host, out int connectionId))
        { return false; }

        string path = uri.AbsolutePath;
        if (path.StartsWith("/~"))
        {
            path = path[1..];
        }

        if (path.StartsWith('/'))
        {
            path = path[1..];
        }

        fileId = new FileId(path, new NetcodeEndPoint(new Unity.NetCode.NetworkId()
        {
            Value = connectionId
        }, Entity.Null));
        return true;
    }
}
