namespace OntoScope.ViewModels;

public sealed class RelationDisplay(string relation, string target, string kind)
{
    public string Relation { get; } = relation;
    public string Target { get; } = target;
    public string Kind { get; } = kind;
}
