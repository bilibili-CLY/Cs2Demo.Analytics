using System.Diagnostics;

namespace Cs2Demo.Analytics;

/// <summary>
/// 调用 csda CLI 解析 demo，返回其 JSON 文本。
/// <c>-output</c> 必须是已存在的**目录**（传文件路径会失败），结果写为 <c>&lt;demo名&gt;.json</c>；
/// 每次调用用独立临时目录，结束（含异常）后清理。
/// </summary>
internal static class CsdaRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    internal static string Run(string demoPath, string csdaPath)
    {
        var outDir = Path.Combine(Path.GetTempPath(), "cs2da-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        try
        {
            EnsureExecutable(csdaPath);

            var psi = new ProcessStartInfo
            {
                FileName = csdaPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-demo-path=" + demoPath);
            psi.ArgumentList.Add("-output=" + outDir);
            psi.ArgumentList.Add("-format=json");
            psi.ArgumentList.Add("-minify");

            Process process;
            try
            {
                process = Process.Start(psi)
                    ?? throw new InvalidOperationException("无法启动 csda 进程。");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"无法启动 csda（{csdaPath}）。请确认文件存在且可执行，或用 DemoAnalyzer.CsdaExecutablePath 指定。原因：{ex.Message}",
                    ex);
            }

            using (process)
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                using var cts = new CancellationTokenSource(Timeout);
                try
                {
                    process.WaitForExitAsync(cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    throw new InvalidOperationException("csda 解析超时（超过 10 分钟）。");
                }

                var stderr = stderrTask.GetAwaiter().GetResult();
                var stdout = stdoutTask.GetAwaiter().GetResult();

                if (process.ExitCode != 0)
                {
                    var message = string.Join(
                        "\n",
                        new[] { stderr, stdout }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

                    if (message.Contains("ErrUnexpectedEndOfDemo", StringComparison.Ordinal))
                        throw new InvalidOperationException("Demo 文件损坏或截断，无法解析。");

                    throw new InvalidOperationException($"csda 解析失败（退出码 {process.ExitCode}）：{message}");
                }
            }

            var jsonFiles = Directory.GetFiles(outDir, "*.json");
            if (jsonFiles.Length == 0)
                throw new InvalidOperationException("csda 未产出 JSON 结果文件。");

            return File.ReadAllText(jsonFiles[0]);
        }
        finally
        {
            try { Directory.Delete(outDir, recursive: true); }
            catch { /* 清理失败不影响结果 */ }
        }
    }

    /// <summary>nupkg 解压会丢失可执行位，启动前补上（Windows 无需处理）。</summary>
    private static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // 只读文件系统等场景：交给后续启动失败处理。
        }
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch { /* 进程可能已退出 */ }
    }
}
