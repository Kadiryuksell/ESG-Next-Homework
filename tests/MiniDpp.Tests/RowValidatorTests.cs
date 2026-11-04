using MiniDpp.Api.Import;

namespace MiniDpp.Tests;

public class RowValidatorTests
{
    private static CsvRow Row(
        string gtin = "08690000000012",
        string model = "Slim Denim 501",
        string batch = "B2027-01",
        string cotton = "98",
        string elastane = "2",
        string place = "Denizli",
        string carbon = "12.4") =>
        new(2, gtin, model, batch, cotton, elastane, place, carbon, []);

    [Fact]
    public void Valid_row_is_converted_to_typed_values()
    {
        var result = RowValidator.Validate(Row());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        var value = result.Value!;
        Assert.Equal("08690000000012", value.Gtin); // baştaki sıfır korunur
        Assert.Equal(98m, value.CottonPct);
        Assert.Equal(2m, value.ElastanePct);
        Assert.Equal(12.4m, value.CarbonKgCo2e);
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed()
    {
        var result = RowValidator.Validate(Row(gtin: " 08690000000012 ", model: "  Slim Denim 501 "));

        Assert.True(result.IsValid);
        Assert.Equal("08690000000012", result.Value!.Gtin);
        Assert.Equal("Slim Denim 501", result.Value.ModelName);
    }

    [Theory]
    [InlineData("", "GTIN boş olamaz")]
    [InlineData("8690000000012", "14 haneli")]
    [InlineData("0869000000001X", "14 haneli")]
    [InlineData("08690000000013", "kontrol basamağı hatalı (beklenen: 2, gelen: 3)")]
    public void Invalid_gtin_is_reported(string gtin, string expectedMessagePart)
    {
        var result = RowValidator.Validate(Row(gtin: gtin));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains(expectedMessagePart));
    }

    [Fact]
    public void Fiber_total_must_be_exactly_100()
    {
        var result = RowValidator.Validate(Row(cotton: "95", elastane: "3"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("toplamı %100") && e.Contains("= 98"));
    }

    [Fact]
    public void Decimal_fibers_summing_to_100_are_valid()
    {
        Assert.True(RowValidator.Validate(Row(cotton: "97.5", elastane: "2.5")).IsValid);
    }

    [Theory]
    [InlineData("101", "0")]
    [InlineData("-1", "101")]
    public void Each_fiber_must_be_between_0_and_100(string cotton, string elastane)
    {
        var result = RowValidator.Validate(Row(cotton: cotton, elastane: elastane));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("0–100 aralığında"));
        // Aralık dışı değerle toplam kontrolü yapılmaz; aynı sorun iki kez raporlanmaz.
        Assert.DoesNotContain(result.Errors, e => e.Contains("toplamı"));
    }

    [Theory]
    [InlineData("abc", "sayısal olmalı")]
    [InlineData("", "boş olamaz")]
    [InlineData("98,5", "nokta (.) kullanın")]
    public void Fiber_must_be_numeric(string cotton, string expectedMessagePart)
    {
        var result = RowValidator.Validate(Row(cotton: cotton));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("elyaf_pamuk_yuzde") && e.Contains(expectedMessagePart));
    }

    [Theory]
    [InlineData("-0.1", "negatif olamaz")]
    [InlineData("yok", "sayısal olmalı")]
    [InlineData("", "boş olamaz")]
    public void Carbon_must_be_non_negative_number(string carbon, string expectedMessagePart)
    {
        var result = RowValidator.Validate(Row(carbon: carbon));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("karbon_kg_co2e") && e.Contains(expectedMessagePart));
    }

    [Fact]
    public void Zero_carbon_is_allowed()
    {
        Assert.True(RowValidator.Validate(Row(carbon: "0")).IsValid);
    }

    [Theory]
    [InlineData("model")]
    [InlineData("batch")]
    [InlineData("place")]
    public void Required_text_fields_cannot_be_blank(string field)
    {
        var row = field switch
        {
            "model" => Row(model: "   "),
            "batch" => Row(batch: ""),
            _ => Row(place: " "),
        };

        var result = RowValidator.Validate(row);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("boş olamaz"));
    }

    [Fact]
    public void All_errors_of_a_row_are_reported_together()
    {
        var result = RowValidator.Validate(Row(gtin: "", model: "", cotton: "95", elastane: "3", carbon: "-1"));

        Assert.Equal(4, result.Errors.Count);
    }

    [Fact]
    public void Trailing_zeros_are_normalized()
    {
        var value = RowValidator.Validate(Row(cotton: "98.00", elastane: "2.0", carbon: "12.40")).Value!;

        Assert.Equal("98", value.CottonPct.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("12.4", value.CarbonKgCo2e.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
