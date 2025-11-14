using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// <seealso href="https://github.com/Full-Metal-Bagel/unity-lang-version/blob/main/Packages/com.fullmetalbagel.unity-lang-version/LangVersionPostprocessor.cs"/>
/// </summary>
public class ProjectPostProcessor : AssetPostprocessor
{
    public static string OnGeneratedCSProject(string path, string content)
    {
        XDocument project = XDocument.Parse(content);
        XAttribute xmlns = project.Root.Attribute("xmlns");
        string @namespace = xmlns == null ? string.Empty : $"{{{xmlns.Value}}}";

        XElement propertyGroup = project.Descendants($"{@namespace}PropertyGroup").First();

        void Set(string propertyName, string value)
        {
            XElement? e = project.Descendants(@namespace + propertyName).SingleOrDefault();
            if (e == null)
            {
                propertyGroup.Add(e = new XElement(@namespace + propertyName, value));
            }
            else
            {
                e.Value = value;
            }
        }

        Set("Nullable", "enable");
        Set("EnableNETAnalyzers", "true");
        Set("AnalysisMode", "preview-all");
        Set("InvariantGlobalization", "true");

        System.Collections.Generic.IEnumerable<XElement>? noWarnElements = project.Descendants($"{@namespace}NoWarn");
        if (noWarnElements == null)
        {
            XElement noWarnElement = new($"{@namespace}NoWarn", string.Empty);
            noWarnElements = new XElement[] { noWarnElement };
            propertyGroup.Add(noWarnElement);
        }

        // foreach (XElement noWarnElement in noWarnElements)
        // {
        //     if (string.IsNullOrWhiteSpace(noWarnElement.Value))
        //     { noWarnElement.Value = "0162"; }
        //     else
        //     { noWarnElement.Value += ";0162"; }
        // }

        string? cscFilePath = project.Descendants($"{@namespace}None")
            .Select(element => element.Attribute("Include")?.Value)
            .SingleOrDefault(file => file != null && file.EndsWith("csc.rsp"));
        if (cscFilePath != null)
        {
            string csc = File.ReadAllText(Path.Combine(Application.dataPath, "..", cscFilePath));
            string[] cscArguments = csc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (string cscArgument in cscArguments)
            {
                if (cscArgument.StartsWith("-langVersion:"))
                {
                    string? langVersion = cscArgument.Replace("-langVersion:", string.Empty);
                    if (!string.IsNullOrWhiteSpace(langVersion))
                    {
                        Set("LangVersion", langVersion switch
                        {
                            "10" => "10.0",
                            "preview" => "11.0",
                            _ => "9.0",
                        });
                    }
                }
            }
        }

        return project.Declaration + Environment.NewLine + project;
    }
}
