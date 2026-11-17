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

public sealed class OntologyRelation
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public required string Label { get; init; }
    public OntologyRelationKind Kind { get; init; }
}

public enum OntologyRelationKind
{
    Inheritance,
    Association
}

public sealed class OntologyProperty
{
    public required string Name { get; init; }
    public required string Uri { get; init; }
    public string Kind { get; init; } = "Object property";
    public string Domain { get; init; } = "—";
    public string Range { get; init; } = "—";
}

public sealed class OntologyDocument
{
    public required string Name { get; init; }
    public required string FileName { get; init; }
    public required string Namespace { get; init; }
    public required IReadOnlyList<OntologyNode> Nodes { get; init; }
    public required IReadOnlyList<OntologyRelation> Relations { get; init; }
    public required IReadOnlyList<OntologyProperty> Properties { get; init; }
    public int IndividualCount { get; init; }
    public bool IsSample { get; init; }

    public int InheritanceCount => Relations.Count(relation => relation.Kind == OntologyRelationKind.Inheritance);
    public int AssociationCount => Relations.Count(relation => relation.Kind == OntologyRelationKind.Association);
}
