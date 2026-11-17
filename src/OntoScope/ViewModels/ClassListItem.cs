using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using OntoScope.Models;

namespace OntoScope.ViewModels;

public sealed partial class ClassListItem(OntologyNode node, int level, bool hasChildren) : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    public OntologyNode Node { get; } = node;
    public int Level { get; } = level;
    public Thickness Indent { get; } = new(4 + level * 17, 0, 0, 0);
    public bool HasChildren { get; } = hasChildren;
}
