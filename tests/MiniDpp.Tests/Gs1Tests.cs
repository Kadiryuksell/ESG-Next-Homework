using MiniDpp.Api.Import;

namespace MiniDpp.Tests;

public class Gs1Tests
{
    [Theory]
    [InlineData("08690000000012")]
    [InlineData("08690000000029")]
    [InlineData("08690000000043")]
    [InlineData("08690000000050")]
    [InlineData("04006381333931")] // gerçek bir EAN-13 (4006381333931) başına 0 eklenmiş hali
    public void Valid_gtin14_is_accepted(string gtin) => Assert.True(Gs1.IsValidGtin14(gtin));

    [Theory]
    [InlineData("08690000000013")] // kontrol basamağı 2 olmalı
    [InlineData("08690000000020")] // kontrol basamağı 9 olmalı
    public void Wrong_check_digit_is_rejected(string gtin) => Assert.False(Gs1.IsValidGtin14(gtin));

    [Theory]
    [InlineData("")]
    [InlineData("8690000000012")]   // 13 hane (Excel'in baştaki sıfırı sildiği durum)
    [InlineData("086900000000120")] // 15 hane
    [InlineData("0869000000001A")]
    [InlineData("0869000000001 ")]
    [InlineData("０８６９００００００００１２")] // tam genişlikli Unicode rakamlar ASCII değildir
    public void Non_fourteen_ascii_digits_are_rejected(string gtin) => Assert.False(Gs1.IsValidGtin14(gtin));

    [Fact]
    public void Check_digit_is_computed_with_3_1_weights_from_the_right()
    {
        // 0869000000001 → sağdan: 1*3 + 9*1 + 6*3 + 8*1 = 38 → 40 - 38 = 2
        Assert.Equal(2, Gs1.ComputeCheckDigit("0869000000001"));
    }
}
