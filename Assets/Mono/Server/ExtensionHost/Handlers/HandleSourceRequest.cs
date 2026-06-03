using System;
using System.IO;
using LanguageCore;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

partial class DebugHost
{
    protected override SourceResponse HandleSourceRequest(SourceArguments arguments)
    {
        if (!Uri.TryCreate(arguments.Source.Path, UriKind.Absolute, out Uri? fileUri) || !FileId.FromUri(fileUri, out _))
        {
            return new SourceResponse();
        }

        NetcodeSourceProviderOffline sourceProvider = new();
        SourceProviderResultSync res = sourceProvider.TryLoad(arguments.Source.Path, null);

        if (res.Type != SourceProviderResultType.Success || res.Stream is null)
        {
            return new SourceResponse();
        }

        StreamReader reader = new(res.Stream);
        string content = reader.ReadToEnd();
        res.Stream.Dispose();
        reader.Dispose();

        return new SourceResponse(content);
    }
}
