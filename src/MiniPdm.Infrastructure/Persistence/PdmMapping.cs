using MiniPdm.Domain;

namespace MiniPdm.Infrastructure.Persistence;

/// <summary>Преобразование перечислений предметной области в строковые значения БД и обратно.</summary>
internal static class PdmMapping
{
    public static string ToDb(this ObjectType type) => type switch
    {
        ObjectType.Assembly => "Assembly",
        ObjectType.Part => "Part",
        ObjectType.StandardPart => "StandardPart",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static ObjectType ToObjectType(string value) => value switch
    {
        "Assembly" => ObjectType.Assembly,
        "Part" => ObjectType.Part,
        "StandardPart" => ObjectType.StandardPart,
        _ => throw new InvalidOperationException($"Неизвестный тип объекта в БД: «{value}»."),
    };

    public static string ToDb(this ObjectState state) => state switch
    {
        ObjectState.InWork => "InWork",
        ObjectState.Approved => "Approved",
        ObjectState.Cancelled => "Cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static ObjectState ToObjectState(string value) => value switch
    {
        "InWork" => ObjectState.InWork,
        "Approved" => ObjectState.Approved,
        "Cancelled" => ObjectState.Cancelled,
        _ => throw new InvalidOperationException($"Неизвестное состояние в БД: «{value}»."),
    };
}
