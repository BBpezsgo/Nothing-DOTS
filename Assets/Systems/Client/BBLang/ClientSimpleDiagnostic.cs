using System;
using System.Collections.Generic;
using LanguageCore;

public class ClientSimpleDiagnostic : Diagnostic
{
    public uint Id { get; }
    public Position Position { get; }
    public Uri? File { get; }
    public new List<ClientSimpleDiagnostic> SubErrors { get; }

    public ClientSimpleDiagnostic(uint id, DiagnosticsLevel level, string message, Position position, Uri? file, List<ClientSimpleDiagnostic> suberrors)
        : base(level, message, System.Collections.Immutable.ImmutableArray<Diagnostic>.Empty)
    {
        Id = id;
        Position = position;
        File = file;
        SubErrors = suberrors;
    }
}
