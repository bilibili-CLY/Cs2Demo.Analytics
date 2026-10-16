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
    public void ParseFile_Garbage_WrapsInInvalidOperation()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dem");
        File.WriteAllText(path, "this is not a demo");
        try
        {
            Assert.Throws<InvalidOperationException>(() => DemoAnalyzer.ParseFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
