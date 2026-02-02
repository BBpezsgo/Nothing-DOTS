using System.Collections.Generic;
using LanguageCore;

public interface ICompiledSource
{
    FileId SourceFile { get; }
    CompilationStatus Status { get; }
    float Progress { get; }
    bool IsSuccess { get; }
    IEnumerable<Diagnostic> Diagnostics { get; }
    IReadOnlyDictionary<FileId, ProgressRecord<(int Current, int Total)>> SubFiles { get; }
}
