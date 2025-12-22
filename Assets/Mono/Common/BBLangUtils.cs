using System.IO;
using System.Text;
using LanguageCore.Runtime;
using NaughtyAttributes;
using UnityEngine;

class BBLangUtils : MonoBehaviour
{
    const string FilePath = "/home/bb/Projects/Nothing-DOTS/Assets/StreamingAssets/lib/bbl.conf";

    [Button("Generate Config")]
    public void GenerateConfig()
    {
        StringBuilder result = new();
        foreach (IExternalFunction externalFunction in ProcessorAPI.GenerateManagedExternalFunctions())
        {
            result.AppendLine($"externalfunc={externalFunction.Name} {externalFunction.ReturnValueSize} {externalFunction.ParametersSize}");
        }
        File.WriteAllText(FilePath, result.ToString());
    }
}
