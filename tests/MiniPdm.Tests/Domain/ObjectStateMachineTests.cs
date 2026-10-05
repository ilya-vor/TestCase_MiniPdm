using MiniPdm.Domain;

namespace MiniPdm.Tests.Domain;

public sealed class ObjectStateMachineTests
{
    [Theory]
    [InlineData(ObjectState.InWork, ObjectState.Approved)]
    [InlineData(ObjectState.InWork, ObjectState.Cancelled)]
    [InlineData(ObjectState.Approved, ObjectState.Cancelled)]
    public void Allowed_transitions(ObjectState from, ObjectState to) => Assert.True(ObjectStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(ObjectState.Approved, ObjectState.InWork)]
    [InlineData(ObjectState.Cancelled, ObjectState.InWork)]
    [InlineData(ObjectState.Cancelled, ObjectState.Approved)]
    [InlineData(ObjectState.InWork, ObjectState.InWork)]
    [InlineData(ObjectState.Approved, ObjectState.Approved)]
    [InlineData(ObjectState.Cancelled, ObjectState.Cancelled)]
    public void Forbidden_transitions(ObjectState from, ObjectState to)
    {
        Assert.False(ObjectStateMachine.CanTransition(from, to));
        Assert.Throws<InvalidStateTransitionException>(() => ObjectStateMachine.EnsureCanTransition(from, to));
    }

    [Fact]
    public void Version_is_editable_only_in_work()
    {
        Assert.True(ObjectStateMachine.IsEditable(ObjectState.InWork));
        Assert.False(ObjectStateMachine.IsEditable(ObjectState.Approved));
        Assert.False(ObjectStateMachine.IsEditable(ObjectState.Cancelled));
    }

    [Fact]
    public void Cancelled_version_does_not_participate_in_calculations()
    {
        Assert.True(ObjectStateMachine.ParticipatesInCalculations(ObjectState.InWork));
        Assert.True(ObjectStateMachine.ParticipatesInCalculations(ObjectState.Approved));
        Assert.False(ObjectStateMachine.ParticipatesInCalculations(ObjectState.Cancelled));
    }

    [Fact]
    public void Version_update_is_forbidden_when_approved()
    {
        var version = new ObjectVersion(Guid.NewGuid(), Guid.NewGuid(), 1, ObjectState.Approved, "Сталь", 1m, DateTimeOffset.UtcNow);
        Assert.Throws<DomainException>(() => version.UpdateAttributes("Сталь", 2m));
    }

    [Fact]
    public void Object_requires_designation_for_assembly_and_part()
    {
        Assert.Throws<DomainException>(() => new PdmObject(Guid.NewGuid(), ObjectType.Part, null, "Деталь"));
        Assert.Throws<DomainException>(() => new PdmObject(Guid.NewGuid(), ObjectType.Assembly, null, "Сборка"));
    }

    [Fact]
    public void Standard_part_must_not_have_designation()
    {
        Assert.Throws<DomainException>(() =>
            new PdmObject(Guid.NewGuid(), ObjectType.StandardPart, Designation.Create("АБВГ.301245.001"), "Болт"));
    }
}
