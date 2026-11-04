using System.Text;
using MiniDpp.Api.Import;

namespace MiniDpp.Tests;

public class CsvRowReaderTests
{
    private const string Header = "gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e";

    private static IReadOnlyList<CsvRow> Read(string csv) =>
        CsvRowReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

    [Fact]
    public void Header_is_row_1_and_first_data_row_is_row_2()
    {
        var rows = Read($"{Header}\n08690000000012,A,B1,98,2,Denizli,1\n08690000000029,B,B2,99,1,Bursa,2\n");

        Assert.Equal([2, 3], rows.Select(r => r.RowNumber));
    }

    [Fact]
    public void Blank_lines_are_skipped_but_still_counted_in_row_numbers()
    {
        var rows = Read($"{Header}\n08690000000012,A,B1,98,2,Denizli,1\n\n08690000000029,B,B2,99,1,Bursa,2\n");

        Assert.Equal([2, 4], rows.Select(r => r.RowNumber));
    }

    [Fact]
    public void Gtin_is_read_as_text_with_leading_zero()
    {
        var row = Read($"{Header}\n08690000000012,A,B1,98,2,Denizli,1").Single();

        Assert.Equal("08690000000012", row.Gtin);
    }

    [Fact]
    public void Utf8_bom_and_windows_line_endings_are_supported()
    {
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes($"{Header}\r\n08690000000012,Ürün,B1,98,2,İzmir,1\r\n"))
            .ToArray();

        var row = CsvRowReader.Read(new MemoryStream(bytes)).Single();

        Assert.Equal("08690000000012", row.Gtin);
        Assert.Equal("Ürün", row.ModelName);
        Assert.Equal("İzmir", row.ProductionPlace);
    }

    [Fact]
    public void Header_columns_are_matched_by_name_not_position()
    {
        var csv = "karbon_kg_co2e,uretim_yeri,elyaf_elastan_yuzde,elyaf_pamuk_yuzde,batch_no,model_adi,GTIN\n" +
                  "12.4,Denizli,2,98,B1,Slim,08690000000012";

        var row = Read(csv).Single();

        Assert.Equal("08690000000012", row.Gtin);
        Assert.Equal("12.4", row.CarbonKgCo2e);
    }

    [Fact]
    public void Quoted_field_with_comma_is_supported()
    {
        var row = Read($"{Header}\n08690000000012,\"Slim, Denim\",B1,98,2,Denizli,1").Single();

        Assert.Equal("Slim, Denim", row.ModelName);
        Assert.Empty(row.ParseErrors);
    }

    [Fact]
    public void Wrong_column_count_is_a_row_error()
    {
        var row = Read($"{Header}\n08690000000012,A,B1,98,2,Denizli").Single();

        Assert.Contains(row.ParseErrors, e => e.Contains("Sütun sayısı hatalı"));
    }

    [Fact]
    public void Missing_header_column_rejects_the_file()
    {
        var ex = Assert.Throws<InvalidCsvException>(() => Read("gtin,model_adi\n08690000000012,A"));

        Assert.Contains("batch_no", ex.Message);
    }

    [Fact]
    public void Empty_file_is_rejected()
    {
        Assert.Throws<InvalidCsvException>(() => Read(""));
    }
}
