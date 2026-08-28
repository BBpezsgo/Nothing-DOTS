using System.Diagnostics.CodeAnalysis;
using UnityEngine.UIElements;

public interface IDocumentSchema
{
    [NotNull] VisualElement? Root { get; }
    void Q(VisualElement root);
}

public static class DocumentSchemaExtensions
{
    public static bool IsVisible([NotNullWhen(true)] this IDocumentSchema? schema) => schema is not null && schema.Root.resolvedStyle.display == DisplayStyle.Flex;
    public static void SetVisible(this IDocumentSchema schema, bool isVisible) => schema.Root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
}
