namespace MiniPdm.Domain;

/// <summary>
/// Связь состава «Состоит из …»: версия сборки-родителя ссылается на дочерний объект
/// (не на его версию) с указанием количества.
/// </summary>
public sealed class BomLink
{
    public BomLink(Guid id, Guid parentVersionId, Guid childObjectId, int quantity)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор связи не может быть пустым.", nameof(id));
        }

        if (parentVersionId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор версии-родителя не может быть пустым.", nameof(parentVersionId));
        }

        if (childObjectId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор дочернего объекта не может быть пустым.", nameof(childObjectId));
        }

        if (quantity <= 0)
        {
            throw new DomainException("Количество в связи состава должно быть больше нуля.");
        }

        Id = id;
        ParentVersionId = parentVersionId;
        ChildObjectId = childObjectId;
        Quantity = quantity;
    }

    public Guid Id { get; }

    public Guid ParentVersionId { get; }

    public Guid ChildObjectId { get; }

    /// <summary>Количество, целое число больше нуля.</summary>
    public int Quantity { get; }
}
