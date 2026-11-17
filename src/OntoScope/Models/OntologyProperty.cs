namespace OntoScope.Models;

public sealed class OntologyProperty
{
    public required string Name { get; init; }
    public required string Uri { get; init; }
    public string Kind { get; init; } = "Object property";
    public string Domain { get; init; } = "—";
    public string Range { get; init; } = "—";
}
