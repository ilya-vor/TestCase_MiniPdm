namespace MiniPdm.Domain;

/// <summary>Состояние версии объекта.</summary>
public enum ObjectState
{
    /// <summary>«В работе» — версию можно изменять.</summary>
    InWork = 0,

    /// <summary>«Утверждено» — версию изменять нельзя, изменения только через новую версию.</summary>
    Approved = 1,

    /// <summary>«Аннулировано» — версия не участвует в расчётах.</summary>
    Cancelled = 2,
}
