namespace MiniPdm.Domain;

/// <summary>Правила переходов состояний версии объекта.</summary>
public static class ObjectStateMachine
{
    /// <summary>
    /// Разрешены только переходы «вперёд»: В работе → Утверждено, В работе → Аннулировано,
    /// Утверждено → Аннулировано. Обратных переходов нет.
    /// </summary>
    public static bool CanTransition(ObjectState from, ObjectState to) => (from, to) switch
    {
        (ObjectState.InWork, ObjectState.Approved) => true,
        (ObjectState.InWork, ObjectState.Cancelled) => true,
        (ObjectState.Approved, ObjectState.Cancelled) => true,
        _ => false,
    };

    public static void EnsureCanTransition(ObjectState from, ObjectState to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidStateTransitionException(from, to);
        }
    }

    /// <summary>Версию можно изменять только в состоянии «В работе».</summary>
    public static bool IsEditable(ObjectState state) => state == ObjectState.InWork;

    /// <summary>Версия участвует в расчётах, если она не аннулирована.</summary>
    public static bool ParticipatesInCalculations(ObjectState state) => state != ObjectState.Cancelled;
}
