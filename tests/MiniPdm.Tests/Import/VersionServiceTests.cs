using MiniPdm.Application.Import;
using MiniPdm.Domain;

namespace MiniPdm.Tests.Import;

public sealed class VersionServiceTests
{
    private readonly VersionService _service = new();

    private static readonly ImportCandidate Candidate = new(
        "Деталь.m3d",
        ObjectType.Part,
        Designation.Create("РДЦЛ.304112.301"),
        "Деталь",
        "Сталь 45",
        1.25m,
        Array.Empty<Application.Cad.CadComponent>());

    private static IReadOnlySet<BomSignature> Empty() => new HashSet<BomSignature>();

    [Fact]
    public void New_object_is_created()
    {
        var decision = _service.Decide(objectExists: false, null, Candidate, Empty());
        Assert.Equal(VersionAction.CreateObject, decision.Action);
    }

    [Fact]
    public void Approved_version_with_same_data_is_unchanged()
    {
        var snapshot = new ExistingVersionSnapshot(ObjectState.Approved, "Сталь 45", 1.25m, Empty());
        var decision = _service.Decide(true, snapshot, Candidate, Empty());
        Assert.Equal(VersionAction.NoChange, decision.Action);
    }

    [Fact]
    public void Approved_version_with_changed_data_creates_new_version()
    {
        var snapshot = new ExistingVersionSnapshot(ObjectState.Approved, "Сталь 45", 2.0m, Empty());
        var decision = _service.Decide(true, snapshot, Candidate, Empty());
        Assert.Equal(VersionAction.CreateNewVersion, decision.Action);
    }

    [Fact]
    public void In_work_version_with_changed_data_is_updated()
    {
        var snapshot = new ExistingVersionSnapshot(ObjectState.InWork, "Сталь 45", 2.0m, Empty());
        var decision = _service.Decide(true, snapshot, Candidate, Empty());
        Assert.Equal(VersionAction.UpdateInWork, decision.Action);
    }

    [Fact]
    public void In_work_version_with_same_data_is_unchanged()
    {
        var snapshot = new ExistingVersionSnapshot(ObjectState.InWork, "Сталь 45", 1.25m, Empty());
        var decision = _service.Decide(true, snapshot, Candidate, Empty());
        Assert.Equal(VersionAction.NoChange, decision.Action);
    }

    [Fact]
    public void Object_without_current_version_gets_new_version()
    {
        var decision = _service.Decide(objectExists: true, null, Candidate, Empty());
        Assert.Equal(VersionAction.CreateNewVersion, decision.Action);
    }

    [Fact]
    public void Changed_bom_creates_change_decision()
    {
        var child = Guid.NewGuid();
        var snapshot = new ExistingVersionSnapshot(
            ObjectState.Approved,
            null,
            null,
            new HashSet<BomSignature> { new(child, 1) });

        var assembly = new ImportCandidate(
            "Сборка.a3d",
            ObjectType.Assembly,
            Designation.Create("РДЦЛ.304112.300"),
            "Сборка",
            null,
            null,
            new[] { new Application.Cad.CadComponent("Деталь.m3d", 2) });

        var newBom = new HashSet<BomSignature> { new(child, 2) };
        var decision = _service.Decide(true, snapshot, assembly, newBom);

        Assert.Equal(VersionAction.CreateNewVersion, decision.Action);
    }
}
