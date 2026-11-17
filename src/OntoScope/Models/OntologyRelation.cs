namespace OntoScope.Models;

public sealed class OntologyRelation
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public required string Label { get; init; }
    public OntologyRelationKind Kind { get; init; }
}
