namespace MiniPdm.Domain;

/// <summary>Тип объекта PDM.</summary>
public enum ObjectType
{
    /// <summary>Сборка. Обозначение обязательно, масса вычисляется по составу.</summary>
    Assembly = 0,

    /// <summary>Деталь. Обозначение, материал и масса обязательны.</summary>
    Part = 1,

    /// <summary>Стандартное изделие. Идентифицируется по наименованию, обозначения нет.</summary>
    StandardPart = 2,
}
