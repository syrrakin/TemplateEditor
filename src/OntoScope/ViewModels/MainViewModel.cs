using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OntoScope.Models;
using OntoScope.Services;

namespace OntoScope.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IOntologyLoader _loader;
    private readonly HashSet<string> _collapsedNodeIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _isUpdatingSelection;

    [ObservableProperty]
    private OntologyDocument _document = null!;

    [ObservableProperty]
    private OntologyNode? _selectedNode;

    [ObservableProperty]
    private ClassListItem? _selectedClassItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Онтология загружена";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isGraphVisible = true;

    [ObservableProperty]
    private bool _isTableVisible;

    public MainViewModel(IOntologyLoader loader)
    {
        _loader = loader;
        Load(SampleOntology.Create());
    }

    public ObservableCollection<ClassListItem> VisibleClasses { get; } = [];
    public ObservableCollection<OntologyNode> FilteredNodes { get; } = [];
    public ObservableCollection<RelationDisplay> SelectedRelations { get; } = [];

    public string OntologyName => Document.Name;
    public string FileName => Document.FileName;
    public string Namespace => Document.Namespace;
    public int ClassCount => Document.Nodes.Count;
    public int RelationCount => Document.Relations.Count;
    public int PropertyCount => Document.Properties.Count;
    public int IndividualCount => Document.IndividualCount;
    public string SourceLabel => Document.IsSample ? "Встроенный пример" : "Локальный OWL-файл";

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

    public void Load(OntologyDocument document)
    {
        Document = document;
        _collapsedNodeIds.Clear();
        SearchText = string.Empty;
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

    [RelayCommand]
    private async Task LoadFileAsync(string? fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        StatusText = "Загрузка и разбор онтологии…";
        try
        {
            var document = await _loader.LoadAsync(fileName, cancellationToken);
            Load(document);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Загрузка отменена";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ErrorMessage = exception.Message;
            StatusText = "Не удалось загрузить OWL-файл";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ExpandAll() => SetExpansion(true);

    [RelayCommand]
    private void CollapseAll() => SetExpansion(false);

    [RelayCommand]
    private void ToggleExpand(ClassListItem? item)
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

    [RelayCommand]
    private void ShowGraph() => SetView(true);

    [RelayCommand]
    private void ShowTable() => SetView(false);

    partial void OnDocumentChanged(OntologyDocument value)
    {
        OnPropertyChanged(nameof(OntologyName));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(Namespace));
        OnPropertyChanged(nameof(ClassCount));
        OnPropertyChanged(nameof(RelationCount));
        OnPropertyChanged(nameof(PropertyCount));
        OnPropertyChanged(nameof(IndividualCount));
        OnPropertyChanged(nameof(SourceLabel));
    }

    partial void OnSelectedNodeChanged(OntologyNode? value)
    {
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
        RefreshSelectedRelations();
    }

    partial void OnSelectedClassItemChanged(ClassListItem? value)
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        _isUpdatingSelection = true;
        SelectedNode = value?.Node;
        _isUpdatingSelection = false;
    }

    partial void OnSearchTextChanged(string value) => RefreshClasses();

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
            OnPropertyChanged(nameof(HasRelations));
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
