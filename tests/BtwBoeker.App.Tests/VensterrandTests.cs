using BtwBoeker.App.Views.Thema;

namespace BtwBoeker.App.Tests;

/// <summary>
/// UX-review stap 1 (besluit 32, overgenomen uit het moederproduct): het hoofdvenster houdt de schaduw en de rand van
/// Windows; op Windows 11 afgeronde hoeken. De randkleur volgt Windows (besluit 26: geen accentkleur per administratie).
/// </summary>
public class VensterrandTests
{
    [Theory]
    [InlineData(10, 0, 19045, false)] // Windows 10 22H2
    [InlineData(10, 0, 21999, false)]
    [InlineData(10, 0, 22000, true)] // Windows 11 21H2
    [InlineData(10, 0, 26100, true)] // Windows 11 24H2
    [InlineData(6, 3, 9600, false)] // Windows 8.1
    public void Alleen_windows_11_krijgt_afgeronde_hoeken(int major, int minor, int build, bool verwacht)
    {
        Assert.Equal(verwacht, Vensterrand.IsWindows11(new Version(major, minor, build)));
    }
}
