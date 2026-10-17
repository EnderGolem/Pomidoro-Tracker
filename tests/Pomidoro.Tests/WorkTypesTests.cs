using Pomidoro.Domain;
using Xunit;

namespace Pomidoro.Tests;

public class WorkTypesTests
{
    private static readonly string[] Catalog = ["Работа", "Рисование"];

    [Theory]
    [InlineData("Работа")]
    [InlineData("работа")]
    [InlineData("РАБОТА")]
    [InlineData("  работа  ")]
    public void Any_case_resolves_to_the_spelling_from_the_catalog(string stored)
    {
        Assert.Equal("Работа", WorkTypes.Canonical(stored, Catalog));
    }

    [Fact]
    public void Type_outside_the_catalog_keeps_its_own_spelling()
    {
        Assert.Equal("Созвон", WorkTypes.Canonical("Созвон", Catalog));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Missing_type_becomes_an_empty_key(string? stored)
    {
        Assert.Equal(string.Empty, WorkTypes.Canonical(stored, Catalog));
    }

    // Служебные подписи закрыты в обоих языках сразу: тип, заведённый в английском
    // интерфейсе, не должен столкнуться с корзиной «без типа» после переключения.
    [Theory]
    [InlineData("Без типа")]
    [InlineData("без типа")]
    [InlineData("No type")]
    [InlineData("NO TYPE")]
    [InlineData(WorkTypes.NoTypeOption)]
    public void Service_names_cannot_be_used_for_a_real_type(string name)
    {
        Assert.True(WorkTypes.IsReservedName(name));
    }

    [Fact]
    public void Set_of_types_ignores_case()
    {
        HashSet<string> hidden = WorkTypes.NewSet();
        hidden.Add("Работа");

        Assert.Contains("РАБОТА", hidden);
    }
}
