using MiniPdm.Application.Structure;
using MiniPdm.Domain;
using MiniPdm.Tests.TestSupport;

namespace MiniPdm.Tests.Structure;

public sealed class MassCalculatorTests
{
    private readonly MassCalculator _calculator = new();

    [Fact]
    public void Assembly_mass_sums_components_with_quantities()
    {
        var root = Guid.NewGuid();
        var partB = Guid.NewGuid();
        var partC = Guid.NewGuid();

        var structure = new StructureBuilder()
            .Add(root, "Сборка", ObjectType.Assembly)
            .Add(partB, "Деталь B", ObjectType.Part, 1.5m)
            .Add(partC, "Деталь C", ObjectType.Part, 3m)
            .Link(root, partB, 2)
            .Link(root, partC, 1)
            .Build();

        var result = _calculator.Calculate(structure);

        Assert.Equal(6m, result.TotalKg);
        Assert.Empty(result.Missing);
        Assert.False(result.HasCycle);
    }

    [Fact]
    public void Nested_assembly_mass_is_multiplied_by_quantities()
    {
        var root = Guid.NewGuid();
        var sub = Guid.NewGuid();
        var part = Guid.NewGuid();

        var structure = new StructureBuilder()
            .Add(root, "Сборка", ObjectType.Assembly)
            .Add(sub, "Подузел", ObjectType.Assembly)
            .Add(part, "Деталь", ObjectType.Part, 2m)
            .Link(root, sub, 2)
            .Link(sub, part, 3)
            .Build();

        var result = _calculator.Calculate(structure);

        Assert.Equal(12m, result.TotalKg);
    }

    [Fact]
    public void Missing_mass_propagates_to_assembly_and_is_reported()
    {
        var root = Guid.NewGuid();
        var sub = Guid.NewGuid();
        var ok = Guid.NewGuid();
        var bad = Guid.NewGuid();

        var structure = new StructureBuilder()
            .Add(root, "Сборка", ObjectType.Assembly)
            .Add(sub, "Подузел", ObjectType.Assembly)
            .Add(ok, "Деталь", ObjectType.Part, 1m)
            .Add(bad, "Прокладка", ObjectType.Part, null)
            .Link(root, sub, 2)
            .Link(sub, ok, 1)
            .Link(sub, bad, 1)
            .Build();

        var result = _calculator.Calculate(structure);

        Assert.Null(result.TotalKg);
        var missing = Assert.Single(result.Missing);
        Assert.Equal(bad, missing.ObjectId);
        Assert.Equal("Прокладка", missing.Name);
    }

    [Fact]
    public void Cycle_is_reported_instead_of_infinite_recursion()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var structure = new StructureBuilder()
            .Add(a, "Сборка A", ObjectType.Assembly)
            .Add(b, "Сборка B", ObjectType.Assembly)
            .Link(a, b, 1)
            .Link(b, a, 1)
            .Build();

        var result = _calculator.Calculate(structure);

        Assert.True(result.HasCycle);
        Assert.Null(result.TotalKg);
    }

    [Fact]
    public void Standard_part_mass_is_counted()
    {
        var root = Guid.NewGuid();
        var bolt = Guid.NewGuid();

        var structure = new StructureBuilder()
            .Add(root, "Сборка", ObjectType.Assembly)
            .Add(bolt, "Болт", ObjectType.StandardPart, 0.05m)
            .Link(root, bolt, 4)
            .Build();

        var result = _calculator.Calculate(structure);

        Assert.Equal(0.2m, result.TotalKg);
    }
}
