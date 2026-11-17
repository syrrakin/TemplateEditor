using OntoScope.Models;

namespace OntoScope.Services;

public interface IOntologyLoader
{
    OntologyDocument Load(string path);

    Task<OntologyDocument> LoadAsync(string path, CancellationToken cancellationToken = default);
}
