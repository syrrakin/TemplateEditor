using System.IO;
using OntoScope.Models;
using OntoScope.Services;

namespace OntoScope.Tests;

public sealed class OwlOntologyLoaderTests
{
    private const string OntologyXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                 xmlns:rdfs="http://www.w3.org/2000/01/rdf-schema#"
                 xmlns:owl="http://www.w3.org/2002/07/owl#">
          <owl:Ontology rdf:about="https://example.org/robotics">
            <rdfs:label xml:lang="ru">Робототехника</rdfs:label>
          </owl:Ontology>
          <owl:Class rdf:about="https://example.org/robotics#Robot">
            <rdfs:label xml:lang="ru">Робот</rdfs:label>
          </owl:Class>
          <owl:Class rdf:about="https://example.org/robotics#MobileRobot">
            <rdfs:label xml:lang="ru">Мобильный робот</rdfs:label>
            <rdfs:subClassOf rdf:resource="https://example.org/robotics#Robot" />
          </owl:Class>
          <owl:Class rdf:about="https://example.org/robotics#Sensor">
            <rdfs:label xml:lang="ru">Датчик</rdfs:label>
          </owl:Class>
          <owl:ObjectProperty rdf:about="https://example.org/robotics#hasSensor">
            <rdfs:label xml:lang="ru">оснащён датчиком</rdfs:label>
            <rdfs:domain rdf:resource="https://example.org/robotics#MobileRobot" />
            <rdfs:range rdf:resource="https://example.org/robotics#Sensor" />
          </owl:ObjectProperty>
          <owl:NamedIndividual rdf:about="https://example.org/robotics#R1" />
        </rdf:RDF>
        """;

    [Fact]
    public async Task LoadAsync_ValidRdfXml_ReturnsOntologyStructure()
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"ontology-{Guid.NewGuid():N}.owl");
        await File.WriteAllTextAsync(path, OntologyXml);
        IOntologyLoader loader = new OwlOntologyLoader();

        try
        {
            var document = await loader.LoadAsync(path);

            Assert.Equal("Робототехника", document.Name);
            Assert.Equal(3, document.Nodes.Count);
            Assert.Single(document.Properties);
            Assert.Equal(1, document.IndividualCount);
            Assert.Equal("Robot", Assert.Single(document.Nodes.Single(node => node.Id == "MobileRobot").ParentIds));
            Assert.Contains(document.Relations, relation =>
                relation.Kind == OntologyRelationKind.Association
                && relation.SourceId == "MobileRobot"
                && relation.TargetId == "Sensor");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
