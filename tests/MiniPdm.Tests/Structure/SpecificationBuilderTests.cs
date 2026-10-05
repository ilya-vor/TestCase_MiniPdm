using MiniPdm.Application.Structure;
using MiniPdm.Domain;
using MiniPdm.Tests.TestSupport;

namespace MiniPdm.Tests.Structure;

public sealed class SpecificationBuilderTests
{
    private readonly SpecificationBuilder _builder = new();

    [Fact]
    public void Quantities_are_multiplied_along_path_and_summed()
    {
        var root = Guid.NewGuid();
        var sub = Guid.NewGuid();
        var gear = Guid.NewGuid();

        // root -> sub (x2) -> gear (x3): 6
        // root -> gear (x1): 1
        // итого 7
        var structure = new StructureBuilder()
            .Add(root, "Сборка", ObjectType.Assembly)
            .Add(sub, "Подузел", ObjectType.Assembly)
            .Add(gear, "Колесо", ObjectType.Part, 5m, "РДЦЛ.304112.302")
            .Link(root, sub, 2)
            .Link(sub, gear, 3)
            .Link(root, gear, 1)
            .Build();

        var result = _builder.Build(structure);

        var row = Assert.Single(result.Rows);
        Assert.Equal(7, row.TotalQuantity);
        Assert.Equal(5m, row.UnitMassKg);
        Assert.Equal(35m, row.TotalMassKg);
        Assert.False(result.HasCycle);
    }

    [Fact]
    public void Rows_without_mass_have_empty_mass()
    {
        var root = Guid.NewGuid();
        var part = Guid.NewGuid();

        var structure = new StructureBuilder()
            .Add(root, "Сборка", ObjectType.Assembly)
            .Add(part, "Прокладка", ObjectType.Part, null, "РДЦЛ.304112.902")
            .Link(root, part, 2)
            .Build();

        var result = _builder.Build(structure);

        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.TotalQuantity);
        Assert.Null(row.UnitMassKg);
        Assert.Null(row.TotalMassKg);
    }

    [Fact]
    public void Standard_parts_are_included_with_name()
    {
        var root = Guid.NewGuid();
        var bolt = Guid.NewGuid();

        var structure = new StructureBuilder()
            .Add(root, "Сборка", ObjectType.Assembly)
            .Add(bolt, "Болт М12", ObjectType.StandardPart, 0.05m)
            .Link(root, bolt, 8)
            .Build();

        var result = _builder.Build(structure);

        var row = Assert.Single(result.Rows);
        Assert.Null(row.Designation);
        Assert.Equal("Болт М12", row.Name);
        Assert.Equal(8, row.TotalQuantity);
        Assert.Equal(0.4m, row.TotalMassKg);
    }

    [Fact]
    public void Cycle_is_reported()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var structure = new StructureBuilder()
            .Add(a, "A", ObjectType.Assembly)
            .Add(b, "B", ObjectType.Assembly)
            .Link(a, b, 1)
            .Link(b, a, 1)
            .Build();

        var result = _builder.Build(structure);

        Assert.True(result.HasCycle);
        Assert.Empty(result.Rows);
    }
}
