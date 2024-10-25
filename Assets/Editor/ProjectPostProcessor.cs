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
        XAttribute xmlns = project.Root!.Attribute("xmlns");
        string @namespace = xmlns == null ? string.Empty : $"{{{xmlns.Value}}}";

        XElement propertyGroup = project.Descendants($"{@namespace}PropertyGroup").First();

        XElement? nullableElement = project.Descendants($"{@namespace}Nullable").SingleOrDefault();
        if (nullableElement == null)
        { propertyGroup.Add(nullableElement = new XElement($"{@namespace}Nullable", "enable")); }
        nullableElement.Value = "enable";

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
            string? langVersion = null;
            foreach (string cscArgument in cscArguments)
            {
                if (cscArgument.StartsWith("-langVersion:"))
                {
                    langVersion = cscArgument.Replace("-langVersion:", string.Empty);
                }
            }

            if (!string.IsNullOrWhiteSpace(langVersion))
            {
                XElement? langVersionElement = project.Descendants($"{@namespace}LangVersion").SingleOrDefault();
                if (langVersionElement == null)
                {
                    propertyGroup.Add(langVersionElement = new XElement($"{@namespace}LangVersion", "9.0"));
                }

                switch (langVersion)
                {
                    case "10":
                        langVersionElement.Value = "10.0";
                        break;
                    case "preview":
                        langVersionElement.Value = "11.0";
                        break;
                }
            }
        }

        return project.Declaration + Environment.NewLine + project;
    }
}
