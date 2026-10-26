using Verenigd.Application.Beveiliging;

namespace Verenigd.Application.Tests;

public class WachtwoordBeleidTests
{
    [Theory]
    [InlineData("")]
    [InlineData("kort")]
    [InlineData("elftekens!!")]
    [InlineData("            ")]
    public void Te_kort_of_leeg_wachtwoord_wordt_afgekeurd(string wachtwoord)
    {
        Assert.NotEmpty(WachtwoordBeleid.Controleer(wachtwoord));
    }

    [Theory]
    [InlineData("twaalftekens")]
    [InlineData("Een lange zin als wachtwoord")]
    public void Wachtwoord_van_minimaal_twaalf_tekens_wordt_goedgekeurd(string wachtwoord)
    {
        Assert.Empty(WachtwoordBeleid.Controleer(wachtwoord));
    }
}
