using System.IO;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using OntoScope.Models;

namespace OntoScope.Services;

public sealed class OwlOntologyLoader
{
    private static readonly XNamespace Owl = "http://www.w3.org/2002/07/owl#";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace Rdfs = "http://www.w3.org/2000/01/rdf-schema#";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    public OntologyDocument Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            var document = XDocument.Load(reader);
            if (document.Root is null)
            {
                throw new InvalidDataException("Файл не содержит XML-документа.");
            }

            return Parse(document, path);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Не удалось прочитать RDF/XML. Проверьте структуру OWL-файла.", exception);
        }
    }

    private static OntologyDocument Parse(XDocument document, string path)
    {
        var classElements = document.Descendants()
            .Where(element => element.Name == Owl + "Class" || element.Name == Rdfs + "Class")
            .ToList();

        var nodes = classElements
            .Select(ParseNode)
            .Where(node => node is not null)
            .Cast<OntologyNode>()
            .GroupBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (nodes.Count == 0)
        {
            throw new InvalidDataException("В файле не найдены классы owl:Class или rdfs:Class.");
        }

        var knownIds = nodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var relations = nodes
            .SelectMany(node => node.ParentIds
                .Where(knownIds.Contains)
                .Select(parentId => new OntologyRelation
                {
                    SourceId = node.Id,
                    TargetId = parentId,
                    Label = "подкласс",
                    Kind = OntologyRelationKind.Inheritance
                }))
            .ToList();

        var properties = document.Descendants()
            .Where(element => element.Name == Owl + "ObjectProperty" || element.Name == Owl + "DatatypeProperty")
            .Select(ParseProperty)
            .Where(property => property is not null)
            .Cast<OntologyProperty>()
            .ToList();

        var nodeById = nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties.Where(property => property.Kind == "Object property"))
        {
            var sourceId = LocalName(property.Domain);
            var targetId = LocalName(property.Range);
            if (!knownIds.Contains(sourceId) || !knownIds.Contains(targetId))
            {
                continue;
            }

            relations.Add(new OntologyRelation
            {
                SourceId = sourceId,
                TargetId = targetId,
                Label = property.Name,
                Kind = OntologyRelationKind.Association
            });
        }

        var ontologyElement = document.Descendants(Owl + "Ontology").FirstOrDefault();
        var name = ontologyElement is null
            ? Path.GetFileNameWithoutExtension(path)
            : Label(ontologyElement) ?? Path.GetFileNameWithoutExtension(path);
        var namespaceUri = ontologyElement?.Attribute(Rdf + "about")?.Value;
        if (string.IsNullOrWhiteSpace(namespaceUri))
        {
            namespaceUri = nodes[0].Uri.Contains('#')
                ? nodes[0].Uri.Split('#')[0] + "#"
                : Path.GetFileName(path);
        }

        return new OntologyDocument
        {
            Name = name,
            FileName = Path.GetFileName(path),
            Namespace = namespaceUri,
            Nodes = nodes,
            Relations = relations,
            Properties = properties,
            IndividualCount = document.Descendants(Owl + "NamedIndividual").Count()
        };
    }

    private static OntologyNode? ParseNode(XElement element)
    {
        var uri = ResourceId(element);
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        var parentIds = element.Elements(Rdfs + "subClassOf")
            .Select(parent => parent.Attribute(Rdf + "resource")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => LocalName(value!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new OntologyNode
        {
            Id = LocalName(uri),
            Name = Label(element) ?? FriendlyName(LocalName(uri)),
            Uri = uri,
            Description = element.Elements(Rdfs + "comment").FirstOrDefault()?.Value.Trim()
                ?? "Описание для этого класса не указано.",
            ParentIds = parentIds,
            Labels = element.Elements(Rdfs + "label").Select(label => label.Value.Trim()).ToList()
        };
    }

    private static OntologyProperty? ParseProperty(XElement element)
    {
        var uri = ResourceId(element);
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        return new OntologyProperty
        {
            Name = Label(element) ?? FriendlyName(LocalName(uri)),
            Uri = uri,
            Kind = element.Name == Owl + "ObjectProperty" ? "Object property" : "Datatype property",
            Domain = element.Elements(Rdfs + "domain").FirstOrDefault()?.Attribute(Rdf + "resource")?.Value ?? "—",
            Range = element.Elements(Rdfs + "range").FirstOrDefault()?.Attribute(Rdf + "resource")?.Value ?? "—"
        };
    }

    private static string? ResourceId(XElement element) =>
        element.Attribute(Rdf + "about")?.Value ?? element.Attribute(Rdf + "ID")?.Value;

    private static string? Label(XElement element)
    {
        var labels = element.Elements(Rdfs + "label").ToList();
        return labels.FirstOrDefault(label => label.Attribute(XNamespace.Xml + "lang")?.Value == "ru")?.Value.Trim()
            ?? labels.FirstOrDefault()?.Value.Trim()
            ?? element.Elements(Dc + "title").FirstOrDefault()?.Value.Trim();
    }

    private static string LocalName(string uri)
    {
        var value = WebUtility.UrlDecode(uri.Trim());
        var separator = value.LastIndexOfAny(['#', '/', ':']);
        return separator >= 0 && separator < value.Length - 1 ? value[(separator + 1)..] : value;
    }

    private static string FriendlyName(string value) => value.Replace('_', ' ').Replace('-', ' ');
}
