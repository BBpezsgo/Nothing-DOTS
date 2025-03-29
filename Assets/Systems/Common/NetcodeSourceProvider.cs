using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LanguageCore;
using Unity.Entities;
using UnityEngine;

class NetcodeSourceProvider : ISourceProviderAsync
{
    readonly CompiledSource source;
    readonly List<ProgressRecord<(int, int)>> progresses;
    readonly bool EnableLogging;

    public NetcodeSourceProvider(CompiledSource source, List<ProgressRecord<(int, int)>> progresses, bool enableLogging)
    {
        this.source = source;
        this.progresses = progresses;
        EnableLogging = enableLogging;
    }

    public IEnumerable<Uri> GetQuery(string requestedFile, Uri? currentFile)
    {
        if (!requestedFile.EndsWith($".{LanguageConstants.LanguageExtension}", StringComparison.Ordinal))
        {
            requestedFile += $".{LanguageConstants.LanguageExtension}";
        }

        if (requestedFile.StartsWith("/~")) requestedFile = requestedFile[1..];
        if (requestedFile.StartsWith('~'))
        {
            requestedFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "." + requestedFile[1..]);
        }

        if (Uri.TryCreate(currentFile, requestedFile, out Uri? result))
        {
            yield return result;
        }
    }

    public SourceProviderResultAsync TryLoad(string requestedFile, Uri? currentFile)
    {
        Uri? lastUri = null;

        foreach (Uri uri in GetQuery(requestedFile, currentFile))
        {
            if (EnableLogging) Debug.Log($"Try load {uri} ...");
            lastUri = uri;

            if (!uri.TryGetNetcode(out FileId fileId))
            {
                Debug.LogError($"[{nameof(CompilerSystemServer)}]: Uri \"{uri}\" aint a netcode uri");
                return SourceProviderResultAsync.NextHandler();
            }

            if (EnableLogging) Debug.Log($"Try load netcode file {fileId.Name} ...");

            if (fileId.Source.IsServer)
            {
                FileData? localFile = FileChunkManagerSystem.GetFileData(fileId.Name.ToString());
                if (!localFile.HasValue)
                { return SourceProviderResultAsync.NextHandler(); }

                AwaitableCompletionSource<Stream> task = new();
                task.SetResult(new MemoryStream(localFile.Value.Data));
                if (EnableLogging) Debug.Log($"Successfully loaded {uri}");
                return SourceProviderResultAsync.Success(uri, task.Awaitable);
            }

            // if (FileChunkManagerSystem.TryGetRemoteFile(fileId, out RemoteFile remoteFile))
            // {
            //     var task = new AwaitableCompletionSource<Stream?>();
            //     task.SetResult(new MemoryStream(remoteFile.File.Data));
            //     return task.Awaitable;
            // }

            FileStatus status = FileChunkManagerSystem.GetInstance(World.DefaultGameObjectInjectionWorld).GetRequestStatus(fileId);

            if (status == FileStatus.NotFound)
            {
                Debug.LogError($"[{nameof(CompilerSystemServer)}]: Remote file \"{uri}\" not found");
                return SourceProviderResultAsync.NotFound(uri);
            }

            ProgressRecord<(int, int)> progress = new(v =>
            {
                float total = progresses.Sum(v => v.Progress.Item2 == 0 ? 0f : (float)v.Progress.Item1 / (float)v.Progress.Item2);
                source.Progress = total / (float)progresses.Count;
                source.Diagnostics.Clear();
                source.StatusChanged = true;
            });
            progresses.Add(progress);
            if (EnableLogging) Debug.Log($"[{nameof(CompilerSystemServer)}]: Source needs file \"{fileId}\" ...");

            {
                AwaitableCompletionSource<Stream> result = new();
                Awaitable<RemoteFile> task = FileChunkManagerSystem.GetInstance(World.DefaultGameObjectInjectionWorld).RequestFile(fileId, progress);
                task.GetAwaiter().OnCompleted(() =>
                {
                    try
                    {
                        MemoryStream data = new(task.GetAwaiter().GetResult().File.Data);
                        result.SetResult(data);
                        if (EnableLogging) Debug.Log($"[{nameof(CompilerSystemServer)}]: Source \"{fileId}\" downloaded ...");
                        if (source.Status == CompilationStatus.Secuedued &&
                            source.CompileSecuedued != 1f)
                        { source.CompileSecuedued = 1f; }
                    }
                    catch (Exception ex)
                    {
                        result.SetException(ex);
                    }
                });

                if (EnableLogging) Debug.Log($"Successfully loaded {uri}");
                return SourceProviderResultAsync.Success(uri, result.Awaitable);
            }

        }

        if (lastUri is not null)
        {
            return SourceProviderResultAsync.NotFound(lastUri!);
        }
        else
        {
            return SourceProviderResultAsync.NextHandler();
        }
    }
}
