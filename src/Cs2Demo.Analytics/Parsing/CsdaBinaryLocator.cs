using System.Runtime.InteropServices;

namespace Cs2Demo.Analytics;

/// <summary>
/// 定位随包分发的 csda 可执行文件。查找顺序（相对当前运行目录与库自身目录各试一遍）：
/// <c>csda/&lt;rid&gt;/csda[.exe]</c>、<c>runtimes/&lt;rid&gt;/native/csda[.exe]</c>。
/// 找不到时抛 <see cref="FileNotFoundException"/>，提示可用 <see cref="DemoAnalyzer.CsdaExecutablePath"/> 手动指定。
/// </summary>
internal static class CsdaBinaryLocator
{
    internal static string Locate()
    {
        var exeName = OperatingSystem.IsWindows() ? "csda.exe" : "csda";
        var rid = CurrentRid();

        var roots = new List<string> { AppContext.BaseDirectory };
        var assemblyDir = Path.GetDirectoryName(typeof(CsdaBinaryLocator).Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDir) && !roots.Contains(assemblyDir))
            roots.Add(assemblyDir);

        foreach (var root in roots)
        {
            var candidates = new[]
            {
                Path.Combine(root, "csda", rid, exeName),
                Path.Combine(root, "runtimes", rid, "native", exeName),
            };
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException(
            $"未找到 csda 可执行文件（RID={rid}）。请确认输出目录下存在 csda/{rid}/{exeName}，" +
            "或通过 DemoAnalyzer.CsdaExecutablePath 指定路径。");
    }

    /// <summary>当前平台的 .NET RID（仅支持 csda 官方预编译的 5 个平台）。</summary>
    internal static string CurrentRid()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        if (OperatingSystem.IsWindows())
            return arch == Architecture.X64 ? "win-x64" : Unsupported("Windows", arch);
        if (OperatingSystem.IsMacOS())
            return arch switch
            {
                Architecture.X64 => "osx-x64",
                Architecture.Arm64 => "osx-arm64",
                _ => Unsupported("macOS", arch),
            };
        if (OperatingSystem.IsLinux())
            return arch switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                _ => Unsupported("Linux", arch),
            };
        return Unsupported("当前系统", arch);
    }

    private static string Unsupported(string os, Architecture arch) =>
        throw new PlatformNotSupportedException($"csda 未提供 {os} / {arch} 的预编译二进制。");
}
