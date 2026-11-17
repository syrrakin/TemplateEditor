using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using OntoScope.Models;
using OntoScope.Services;
using OntoScope.ViewModels;

namespace OntoScope;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly OwlOntologyLoader _loader = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Graph.Ontology = _viewModel.Document;
        Graph.SelectedNode = _viewModel.SelectedNode;
        Graph.NodeSelected += node => _viewModel.SelectNode(node);
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += async (_, _) =>
        {
            Graph.FitToView();
            var arguments = Environment.GetCommandLineArgs();
            if (arguments.Length > 1 && File.Exists(arguments[1]))
            {
                await LoadFile(arguments[1]);
            }
        };
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Открыть онтологию",
            Filter = "OWL-онтологии (*.owl;*.rdf;*.xml)|*.owl;*.rdf;*.xml|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            await LoadFile(dialog.FileName);
        }
    }

    private async Task LoadFile(string fileName)
    {
        OpenButton.IsEnabled = false;
        _viewModel.StatusText = "Загрузка и разбор онтологии…";
        try
        {
            var document = await Task.Run(() => _loader.Load(fileName));
            _viewModel.Load(document);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _viewModel.StatusText = "Не удалось загрузить OWL-файл";
            MessageBox.Show(
                this,
                exception.Message,
                "Ошибка открытия онтологии",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            OpenButton.IsEnabled = true;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Document))
        {
            Graph.Ontology = _viewModel.Document;
        }
        else if (e.PropertyName == nameof(MainViewModel.SelectedNode))
        {
            Graph.SelectedNode = _viewModel.SelectedNode;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => Graph.ZoomIn();

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => Graph.ZoomOut();

    private void FitGraph_Click(object sender, RoutedEventArgs e) => Graph.FitToView();

    private void CopyIri_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.SelectedUri))
        {
            return;
        }

        try
        {
            Clipboard.SetText(_viewModel.SelectedUri);
            _viewModel.StatusText = "IRI скопирован в буфер обмена";
        }
        catch (ExternalException)
        {
            _viewModel.StatusText = "Буфер обмена занят другим приложением";
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            OpenFile_Click(sender, e);
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D0)
        {
            Graph.FitToView();
        }
        else if (e.Key == Key.Escape && SearchBox.IsKeyboardFocusWithin)
        {
            _viewModel.SearchText = string.Empty;
            Keyboard.ClearFocus();
        }
        else
        {
            return;
        }

        e.Handled = true;
    }
}
