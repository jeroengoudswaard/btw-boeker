using Verenigd.Domain.Leden;

namespace Verenigd.Domain.Tests.Leden;

public class PostcodeTests
{
    [Theory]
    [InlineData("1234AB", "1234 AB")]
    [InlineData("1234 ab", "1234 AB")]
    [InlineData(" 1234  AB ", "1234 AB")]
    [InlineData("1234-AB", "1234 AB")]
    public void Nederlandse_postcode_krijgt_vaste_notatie(string invoer, string verwacht)
    {
        var postcode = Postcode.Normaliseer(invoer);

        Assert.True(postcode.Herkend);
        Assert.Equal(verwacht, postcode.Waarde);
    }

    [Theory]
    [InlineData("1000")] // Belgisch
    [InlineData("0123 AB")] // Nederlandse postcodes beginnen niet met 0
    [InlineData("SW1A 1AA")]
    public void Andere_postcode_blijft_ongewijzigd(string invoer)
    {
        var postcode = Postcode.Normaliseer(invoer);

        Assert.False(postcode.Herkend);
        Assert.Equal(invoer, postcode.Waarde);
    }
}
