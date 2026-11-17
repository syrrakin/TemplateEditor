namespace OntoScope.Models;

public sealed class OntologyNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Uri { get; init; }
    public string Description { get; init; } = "Описание для этого класса не указано.";
    public IReadOnlyList<string> ParentIds { get; init; } = [];
    public IReadOnlyList<string> Labels { get; init; } = [];
}
