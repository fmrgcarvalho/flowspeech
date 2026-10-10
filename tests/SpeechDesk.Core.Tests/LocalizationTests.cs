using SpeechDesk.Core.Localization;
using Xunit;

namespace SpeechDesk.Core.Tests;

public class LocalizationTests
{
    [Fact]
    public void English_is_the_default_and_portuguese_is_available()
    {
        var previous = L.Language;
        try
        {
            L.Language = "";
            Assert.Equal("Exit", L.T("tray.exit"));
            L.Language = "pt-PT";
            Assert.Equal("Sair", L.T("tray.exit"));
            Assert.Equal("A descarregar modelo… 42%", L.T("overlay.downloading", 42));
        }
        finally
        {
            L.Language = previous;
        }
    }

    [Fact]
    public void Unknown_keys_fall_back_to_the_key() => Assert.Equal("no.such.key", L.T("no.such.key"));
}
