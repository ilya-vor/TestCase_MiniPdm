using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace MiniPdm.Domain;

/// <summary>
/// Обозначение изделия по упрощённому ЕСКД: четыре заглавные кириллические буквы,
/// точка, шесть цифр, точка, три цифры. Пример: <c>АБВГ.301245.001</c>.
/// </summary>
public sealed class Designation : IEquatable<Designation>
{
    // Кириллица только заглавная (включая Ё), латиница не допускается.
    private static readonly Regex Format = new(
        @"^[А-ЯЁ]{4}\.[0-9]{6}\.[0-9]{3}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private Designation(string value) => Value = value;

    public string Value { get; }

    public static bool IsValid([NotNullWhen(true)] string? value)
        => value is not null && Format.IsMatch(value.Trim());

    public static Designation Create(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (!Format.IsMatch(normalized))
        {
            throw new InvalidDesignationException(value);
        }

        return new Designation(normalized);
    }

    public static bool TryCreate(string? value, [NotNullWhen(true)] out Designation? designation)
    {
        designation = null;
        if (!IsValid(value))
        {
            return false;
        }

        designation = new Designation(value!.Trim());
        return true;
    }

    public bool Equals(Designation? other)
        => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as Designation);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
