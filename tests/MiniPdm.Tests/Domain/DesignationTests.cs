using MiniPdm.Domain;

namespace MiniPdm.Tests.Domain;

public sealed class DesignationTests
{
    [Theory]
    [InlineData("АБВГ.301245.001")]
    [InlineData("РДЦЛ.304112.300")]
    [InlineData("ЯЯЯЯ.000000.000")]
    [InlineData("  РДЦЛ.304112.300  ")]
    public void Valid_designations_are_accepted(string value)
    {
        Assert.True(Designation.IsValid(value));
        Assert.True(Designation.TryCreate(value, out var designation));
        Assert.Equal(value.Trim(), designation!.Value);
    }

    [Theory]
    [InlineData("PДЦЛ.304112.601")]   // латинская P
    [InlineData("РДЦЛ.30411.602")]    // пять цифр в середине
    [InlineData("РДЦЛ.304112.60")]    // две цифры в конце
    [InlineData("рдцл.304112.601")]   // строчные буквы
    [InlineData("РДЦЛ304112601")]     // нет точек
    [InlineData("АБВГ.30124X.001")]   // не цифра
    [InlineData("АБВГ.٣٠١٢٤٥.٠٠١")]   // арабо-индийские цифры (Unicode, не ASCII)
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_designations_are_rejected(string? value)
    {
        Assert.False(Designation.IsValid(value));
        Assert.False(Designation.TryCreate(value, out _));
    }

    [Fact]
    public void Create_throws_for_invalid_value()
    {
        var exception = Assert.Throws<InvalidDesignationException>(() => Designation.Create("PДЦЛ.304112.601"));
        Assert.Equal("PДЦЛ.304112.601", exception.Value);
    }

    [Fact]
    public void Equality_is_ordinal()
    {
        var a = Designation.Create("РДЦЛ.304112.300");
        var b = Designation.Create("РДЦЛ.304112.300");
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
