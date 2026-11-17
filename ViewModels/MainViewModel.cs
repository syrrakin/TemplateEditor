using System.Collections.ObjectModel;
using System.Windows;
using OntoScope.Models;
using OntoScope.Services;

namespace OntoScope.ViewModels;

public sealed class ClassListItem(OntologyNode node, int level, bool hasChildren) : ObservableObject
{
    private bool _isExpanded = true;

    public OntologyNode Node { get; } = node;
    public int Level { get; } = level;
    public Thickness Indent { get; } = new(4 + level * 17, 0, 0, 0);
    public bool HasChildren { get; } = hasChildren;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}

public sealed class RelationDisplay(string relation, string target, string kind)
{
    public string Relation { get; } = relation;
    public string Target { get; } = target;
    public string Kind { get; } = kind;
}

public sealed class MainViewModel : ObservableObject
{
    private OntologyDocument _document = null!;
    private OntologyNode? _selectedNode;
    private ClassListItem? _selectedClassItem;
    private string _searchText = string.Empty;
    private string _statusText = "Онтология загружена";
    private bool _isGraphVisible = true;
    private bool _isTableVisible;
    private bool _isUpdatingSelection;
    private readonly HashSet<string> _collapsedNodeIds = new(StringComparer.OrdinalIgnoreCase);

    public MainViewModel()
    {
        ExpandAllCommand = new RelayCommand(() => SetExpansion(true));
        CollapseAllCommand = new RelayCommand(() => SetExpansion(false));
        ToggleExpandCommand = new RelayCommand<ClassListItem>(ToggleExpansion);
        ShowGraphCommand = new RelayCommand(() => SetView(true));
        ShowTableCommand = new RelayCommand(() => SetView(false));
        Load(SampleOntology.Create());
    }

    public ObservableCollection<ClassListItem> VisibleClasses { get; } = [];
    public ObservableCollection<OntologyNode> FilteredNodes { get; } = [];
    public ObservableCollection<RelationDisplay> SelectedRelations { get; } = [];

    public RelayCommand ExpandAllCommand { get; }
    public RelayCommand CollapseAllCommand { get; }
    public RelayCommand<ClassListItem> ToggleExpandCommand { get; }
    public RelayCommand ShowGraphCommand { get; }
    public RelayCommand ShowTableCommand { get; }

    public OntologyDocument Document
    {
        get => _document;
        private set
        {
            if (!SetProperty(ref _document, value))
            {
                return;
            }

            OnPropertyChanged(nameof(OntologyName));
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(Namespace));
            OnPropertyChanged(nameof(ClassCount));
            OnPropertyChanged(nameof(RelationCount));
            OnPropertyChanged(nameof(PropertyCount));
            OnPropertyChanged(nameof(IndividualCount));
            OnPropertyChanged(nameof(SourceLabel));
        }
    }

    public string OntologyName => Document.Name;
    public string FileName => Document.FileName;
    public string Namespace => Document.Namespace;
    public int ClassCount => Document.Nodes.Count;
    public int RelationCount => Document.Relations.Count;
    public int PropertyCount => Document.Properties.Count;
    public int IndividualCount => Document.IndividualCount;
    public string SourceLabel => Document.IsSample ? "Встроенный пример" : "Локальный OWL-файл";

    public OntologyNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (!SetProperty(ref _selectedNode, value))
            {
                return;
            }

            if (!_isUpdatingSelection)
            {
                _isUpdatingSelection = true;
                SelectedClassItem = VisibleClasses.FirstOrDefault(item => ReferenceEquals(item.Node, value));
                _isUpdatingSelection = false;
            }

            OnPropertyChanged(nameof(SelectedName));
            OnPropertyChanged(nameof(SelectedUri));
            OnPropertyChanged(nameof(SelectedDescription));
            OnPropertyChanged(nameof(ParentNames));
            OnPropertyChanged(nameof(HasParents));
            OnPropertyChanged(nameof(HasRelations));
            RefreshSelectedRelations();
        }
    }

    public string SelectedName => SelectedNode?.Name ?? "Выберите класс";
    public string SelectedUri => SelectedNode?.Uri ?? string.Empty;
    public string SelectedDescription => SelectedNode?.Description ?? string.Empty;
    public string ParentNames => SelectedNode is null
        ? string.Empty
        : string.Join(", ", SelectedNode.ParentIds
            .Select(id => Document.Nodes.FirstOrDefault(node => node.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name)));
    public bool HasParents => !string.IsNullOrWhiteSpace(ParentNames);
    public bool HasRelations => SelectedRelations.Count > 0;

    public ClassListItem? SelectedClassItem
    {
        get => _selectedClassItem;
        set
        {
            if (!SetProperty(ref _selectedClassItem, value) || _isUpdatingSelection)
            {
                return;
            }

            _isUpdatingSelection = true;
            SelectedNode = value?.Node;
            _isUpdatingSelection = false;
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshClasses();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsGraphVisible
    {
        get => _isGraphVisible;
        private set => SetProperty(ref _isGraphVisible, value);
    }

    public bool IsTableVisible
    {
        get => _isTableVisible;
        private set => SetProperty(ref _isTableVisible, value);
    }

    public void Load(OntologyDocument document)
    {
        Document = document;
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        RefreshClasses();
        var preferred = document.Nodes.FirstOrDefault(node => node.Id.Equals("Student", StringComparison.OrdinalIgnoreCase))
            ?? document.Nodes.First();
        SelectNode(preferred);
        StatusText = document.IsSample ? "Открыт демонстрационный пример" : "OWL-файл успешно загружен";
        SetView(true);
    }

    public void SelectNode(OntologyNode node)
    {
        _isUpdatingSelection = true;
        SelectedNode = node;
        SelectedClassItem = VisibleClasses.FirstOrDefault(item => ReferenceEquals(item.Node, node));
        _isUpdatingSelection = false;
    }

    private void SetView(bool graphVisible)
    {
        IsGraphVisible = graphVisible;
        IsTableVisible = !graphVisible;
    }

    private void SetExpansion(bool expanded)
    {
        if (expanded)
        {
            _collapsedNodeIds.Clear();
        }
        else
        {
            foreach (var node in Document.Nodes.Where(node => Document.Nodes.Any(child => child.ParentIds.Contains(node.Id, StringComparer.OrdinalIgnoreCase))))
            {
                _collapsedNodeIds.Add(node.Id);
            }
        }

        RefreshClasses();
    }

    private void ToggleExpansion(ClassListItem? item)
    {
        if (item is null || !item.HasChildren || !string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        if (!_collapsedNodeIds.Remove(item.Node.Id))
        {
            _collapsedNodeIds.Add(item.Node.Id);
        }

        item.IsExpanded = !item.IsExpanded;
        RefreshClasses();
    }

    private void RefreshClasses()
    {
        if (Document is null)
        {
            return;
        }

        VisibleClasses.Clear();
        FilteredNodes.Clear();

        var filtered = Document.Nodes
            .Where(MatchesSearch)
            .OrderBy(node => node.Name, StringComparer.CurrentCulture)
            .ToList();

        foreach (var node in filtered)
        {
            FilteredNodes.Add(node);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var node in filtered)
            {
                VisibleClasses.Add(new ClassListItem(node, 0, false));
            }
        }
        else
        {
            var knownIds = Document.Nodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var roots = Document.Nodes
                .Where(node => node.ParentIds.Count == 0 || !node.ParentIds.Any(knownIds.Contains))
                .OrderBy(node => node.Name, StringComparer.CurrentCulture);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
            {
                AddTreeItem(root, 0, visited);
            }

            foreach (var node in Document.Nodes.Where(node => !visited.Contains(node.Id) && !HasCollapsedAncestor(node, [])))
            {
                AddTreeItem(node, 0, visited);
            }
        }

        if (SelectedNode is not null)
        {
            SelectNode(SelectedNode);
        }
    }

    private void AddTreeItem(OntologyNode node, int level, HashSet<string> visited)
    {
        if (!visited.Add(node.Id))
        {
            return;
        }

        var children = Document.Nodes
            .Where(child => child.ParentIds.Any(parent => parent.Equals(node.Id, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(child => child.Name, StringComparer.CurrentCulture)
            .ToList();
        var item = new ClassListItem(node, level, children.Count > 0)
        {
            IsExpanded = !_collapsedNodeIds.Contains(node.Id)
        };
        VisibleClasses.Add(item);

        if (!item.IsExpanded)
        {
            return;
        }

        foreach (var child in children)
        {
            AddTreeItem(child, level + 1, visited);
        }
    }

    private bool HasCollapsedAncestor(OntologyNode node, HashSet<string> path)
    {
        foreach (var parentId in node.ParentIds)
        {
            if (_collapsedNodeIds.Contains(parentId))
            {
                return true;
            }

            var parent = Document.Nodes.FirstOrDefault(item => item.Id.Equals(parentId, StringComparison.OrdinalIgnoreCase));
            if (parent is not null && path.Add(parent.Id) && HasCollapsedAncestor(parent, path))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesSearch(OntologyNode node) =>
        string.IsNullOrWhiteSpace(SearchText)
        || node.Name.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase)
        || node.Id.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase)
        || node.Uri.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase);

    private void RefreshSelectedRelations()
    {
        SelectedRelations.Clear();
        if (SelectedNode is null)
        {
            return;
        }

        foreach (var relation in Document.Relations.Where(relation => relation.SourceId.Equals(SelectedNode.Id, StringComparison.OrdinalIgnoreCase)))
        {
            var target = Document.Nodes.FirstOrDefault(node => node.Id.Equals(relation.TargetId, StringComparison.OrdinalIgnoreCase));
            if (target is not null)
            {
                SelectedRelations.Add(new RelationDisplay(
                    relation.Label,
                    target.Name,
                    relation.Kind == OntologyRelationKind.Inheritance ? "Наследование" : "Связь"));
            }
        }

        OnPropertyChanged(nameof(HasRelations));
    }
}
