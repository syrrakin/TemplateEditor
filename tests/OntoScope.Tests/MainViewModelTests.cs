using System.IO;
using OntoScope.Services;
using OntoScope.ViewModels;

namespace OntoScope.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void SearchText_ExistingClassId_FiltersVisibleClasses()
    {
        var viewModel = CreateViewModel();

        viewModel.SearchText = "Student";

        Assert.Equal(2, viewModel.VisibleClasses.Count);
        Assert.All(viewModel.VisibleClasses, item =>
            Assert.Contains("Student", item.Node.Id, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ShowTableCommand_Execute_SwitchesVisibleView()
    {
        var viewModel = CreateViewModel();

        viewModel.ShowTableCommand.Execute(null);

        Assert.True(viewModel.IsTableVisible);
        Assert.False(viewModel.IsGraphVisible);
    }

    [Fact]
    public async Task LoadFileCommand_MissingFile_SetsErrorMessage()
    {
        var viewModel = CreateViewModel();
        var missingPath = Path.Combine(AppContext.BaseDirectory, $"missing-{Guid.NewGuid():N}.owl");

        await viewModel.LoadFileCommand.ExecuteAsync(missingPath);

        Assert.NotNull(viewModel.ErrorMessage);
    }

    private static MainViewModel CreateViewModel() => new(new OwlOntologyLoader());
}
