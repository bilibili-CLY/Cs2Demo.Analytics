using System.Text;
using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

public sealed class DemoAnalyzerErrorTests
{
    [Fact]
    public void ParseFile_Missing_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => DemoAnalyzer.ParseFile("/tmp/不存在的demo.dem"));
    }

    [Fact]
    public void ParseFile_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DemoAnalyzer.ParseFile(null!));
    }

    [Fact]
    public void ParseStream_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DemoAnalyzer.ParseStream(null!));
    }

    [Fact]
    public void ParseStream_Garbage_WrapsInInvalidOperation()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("this is not a demo"));
        Assert.Throws<InvalidOperationException>(() => DemoAnalyzer.ParseStream(stream, "garbage"));
    }
}
