using OntoScope.Models;

namespace OntoScope.Services;

public static class SampleOntology
{
    private const string Namespace = "https://ontoscope.dev/ontology/university#";

    public static OntologyDocument Create()
    {
        var nodes = new[]
        {
            Node("Person", "Человек", "Человек, связанный с образовательным или исследовательским процессом."),
            Node("Student", "Студент", "Обучающийся, зачисленный на образовательную программу и проходящий один или несколько курсов.", "Person"),
            Node("GraduateStudent", "Аспирант", "Студент, выполняющий научное исследование под руководством научного руководителя.", "Student"),
            Node("Professor", "Преподаватель", "Научно-педагогический работник, который ведёт учебные курсы и публикует исследования.", "Person"),
            Node("Course", "Учебный курс", "Структурированная часть образовательной программы с определёнными темами и результатами обучения."),
            Node("Exam", "Экзамен", "Форма аттестации, проводимая в рамках учебного курса.", "Course"),
            Node("Department", "Кафедра", "Структурное подразделение университета, отвечающее за учебные курсы и исследования."),
            Node("Faculty", "Факультет", "Объединение кафедр и образовательных программ внутри университета.", "Department"),
            Node("ResearchProject", "Исследовательский проект", "Научная работа с определёнными целями, участниками и ожидаемыми результатами."),
            Node("Publication", "Публикация", "Научный или учебный материал, опубликованный сотрудником университета."),
            Node("Thesis", "Диссертация", "Научная публикация, представленная для получения учёной степени.", "Publication"),
            Node("Laboratory", "Лаборатория", "Исследовательское подразделение, в котором реализуются научные проекты.")
        };

        var relations = new[]
        {
            Relation("Student", "Person", "подкласс"),
            Relation("GraduateStudent", "Student", "подкласс"),
            Relation("Professor", "Person", "подкласс"),
            Relation("Exam", "Course", "подкласс"),
            Relation("Faculty", "Department", "подкласс"),
            Relation("Thesis", "Publication", "подкласс"),
            Relation("Student", "Course", "проходит", OntologyRelationKind.Association),
            Relation("Professor", "Course", "преподаёт", OntologyRelationKind.Association),
            Relation("GraduateStudent", "Professor", "руководитель", OntologyRelationKind.Association),
            Relation("Professor", "Publication", "автор", OntologyRelationKind.Association),
            Relation("Course", "Department", "закреплён за", OntologyRelationKind.Association),
            Relation("ResearchProject", "Laboratory", "реализуется в", OntologyRelationKind.Association)
        };

        var properties = new[]
        {
            Property("enrolledIn", "проходит курс", "Студент", "Учебный курс"),
            Property("teaches", "преподаёт", "Преподаватель", "Учебный курс"),
            Property("hasSupervisor", "имеет руководителя", "Аспирант", "Преподаватель"),
            Property("authorOf", "является автором", "Преподаватель", "Публикация"),
            Property("belongsTo", "закреплён за", "Учебный курс", "Кафедра"),
            Property("realizedIn", "реализуется в", "Исследовательский проект", "Лаборатория"),
            Property("hasName", "имеет имя", "Человек", "xsd:string", "Datatype property"),
            Property("hasCredits", "имеет зачётные единицы", "Учебный курс", "xsd:integer", "Datatype property")
        };

        return new OntologyDocument
        {
            Name = "Умный университет",
            FileName = "university.owl",
            Namespace = Namespace,
            Nodes = nodes,
            Relations = relations,
            Properties = properties,
            IndividualCount = 24,
            IsSample = true
        };
    }

    private static OntologyNode Node(string id, string name, string description, params string[] parents) => new()
    {
        Id = id,
        Name = name,
        Uri = Namespace + id,
        Description = description,
        ParentIds = parents,
        Labels = [name]
    };

    private static OntologyRelation Relation(
        string source,
        string target,
        string label,
        OntologyRelationKind kind = OntologyRelationKind.Inheritance) => new()
        {
            SourceId = source,
            TargetId = target,
            Label = label,
            Kind = kind
        };

    private static OntologyProperty Property(
        string id,
        string name,
        string domain,
        string range,
        string kind = "Object property") => new()
        {
            Name = name,
            Uri = Namespace + id,
            Kind = kind,
            Domain = domain,
            Range = range
        };
}
