namespace OntoScope.Models;

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
