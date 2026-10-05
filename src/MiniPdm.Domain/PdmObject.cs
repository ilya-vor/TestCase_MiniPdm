namespace MiniPdm.Domain;

/// <summary>
/// Объект PDM: сборка, деталь или стандартное изделие. Обозначение есть у сборок и деталей,
/// стандартные изделия идентифицируются по наименованию.
/// </summary>
public sealed class PdmObject
{
    public PdmObject(
        Guid id,
        ObjectType type,
        Designation? designation,
        string name,
        Guid? currentVersionId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор объекта не может быть пустым.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Наименование объекта не может быть пустым.");
        }

        if (type == ObjectType.StandardPart && designation is not null)
        {
            throw new DomainException("Стандартное изделие не имеет обозначения.");
        }

        if (type != ObjectType.StandardPart && designation is null)
        {
            throw new DomainException("Для сборки и детали обозначение обязательно.");
        }

        Id = id;
        Type = type;
        Designation = designation;
        Name = name.Trim();
        CurrentVersionId = currentVersionId;
    }

    public Guid Id { get; }

    public ObjectType Type { get; }

    public Designation? Designation { get; }

    public string Name { get; }

    /// <summary>Текущая версия — последняя неаннулированная.</summary>
    public Guid? CurrentVersionId { get; private set; }

    public bool IsAssembly => Type == ObjectType.Assembly;

    public void SetCurrentVersion(Guid? versionId) => CurrentVersionId = versionId;
}
