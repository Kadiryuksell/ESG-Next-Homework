namespace MiniDpp.Api.Import;

/// <summary>GS1 GTIN-14 yardımcıları.</summary>
public static class Gs1
{
    public const int Gtin14Length = 14;

    public static bool IsFourteenDigits(string value) =>
        value.Length == Gtin14Length && value.All(char.IsAsciiDigit);

    /// <summary>
    /// GS1 mod-10 kontrol basamağı: kontrol basamağı hariç rakamlar sağdan sola
    /// 3, 1, 3, 1... ile çarpılıp toplanır; sonuç 10'un bir sonraki katına tamamlanır.
    /// </summary>
    public static int ComputeCheckDigit(ReadOnlySpan<char> digitsWithoutCheck)
    {
        var sum = 0;
        for (var i = 0; i < digitsWithoutCheck.Length; i++)
        {
            var digit = digitsWithoutCheck[digitsWithoutCheck.Length - 1 - i] - '0';
            sum += digit * (i % 2 == 0 ? 3 : 1);
        }

        return (10 - sum % 10) % 10;
    }

    public static bool IsValidGtin14(string value) =>
        IsFourteenDigits(value) && ComputeCheckDigit(value.AsSpan(0, Gtin14Length - 1)) == value[^1] - '0';
}
