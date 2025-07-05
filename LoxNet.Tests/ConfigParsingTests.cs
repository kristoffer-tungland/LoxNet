using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace LoxNet.Tests;

public class ConfigParsingTests
{
    [Fact]
    public void ParseDemoConfig_ContainsRoomsCategoriesAndControls()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Demo Case Config UK.Loxone");
        var doc = XDocument.Load(path);

        var categories = doc.Descendants("C")
            .Where(e => (string?)e.Attribute("Type") == "Category")
            .Select(e => (string?)e.Attribute("Title"))
            .ToList();
        Assert.Contains("Lighting", categories);

        var rooms = doc.Descendants("C")
            .Where(e => (string?)e.Attribute("Type") == "Place")
            .Select(e => (string?)e.Attribute("Title"))
            .ToList();
        Assert.Contains("Kitchen", rooms);

        var controls = doc.Descendants("C")
            .Where(e => e.Attribute("U") != null)
            .Where(e => (string?)e.Attribute("Type") != "Category" && (string?)e.Attribute("Type") != "Place")
            .Select(e => (string?)e.Attribute("Title"))
            .ToList();
        Assert.Contains("Lighting controller", controls);
    }
}
