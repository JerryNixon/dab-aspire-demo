using Microsoft.Extensions.Logging;
using Web.Library.Data;
using Web.Library.Models;

namespace Web.Library.Tests;

public class DataApiTableFactoryTests
{
    [Fact]
    public void Constructor_NormalizesBaseAddress()
    {
        var options = new DataApiOptions("http://localhost:5000");
        var factory = new DataApiTableFactory(options, LoggerFactory.Create(_ => { }));

        var table = factory.Create<Todo>();

        Assert.NotNull(table);
    }

    [Fact]
    public void Create_WithEmptyEntityName_Throws()
    {
        var options = new DataApiOptions("http://localhost:5000/");
        var factory = new DataApiTableFactory(options, LoggerFactory.Create(_ => { }));

        Assert.Throws<ArgumentException>(() => factory.Create<Todo>(string.Empty));
    }
}
