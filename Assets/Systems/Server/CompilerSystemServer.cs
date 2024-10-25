using System;
using System.Linq;
using System.Text;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Tokenizing;
using Maths;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct CompilerSystemServer : ISystem
{
    void ISystem.OnUpdate(ref SystemState state)
    {
        using EntityCommandBuffer entityCommandBuffer = new(Allocator.Temp);

        foreach ((FileId file, CompiledSource source) in CompilerManager.Instance.CompiledSources.ToArray())
        {
            if (source.CompileSecuedued == default ||
                source.CompileSecuedued > Time.time) continue;

            CompiledSource _source = source;
            CompilerManager.Instance.CompileSource(ref _source, entityCommandBuffer, ref state);
            CompilerManager.Instance.CompiledSources[file] = _source;
        }

        entityCommandBuffer.Playback(state.EntityManager);
    }
}
