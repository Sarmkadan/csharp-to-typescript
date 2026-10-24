using System;
using System.IO;
using System.Text;

namespace CSharpToTypeScript.Emit;

/// <summary>
/// Writes generated TypeScript next to the consuming project. The file is only rewritten when its content changes,
/// so no-op builds leave the timestamp alone and do not trigger downstream rebuilds.
/// </summary>
public static class TypeScriptOutputWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes <paramref name="content"/> to <c>Path.Combine(projectDir, relativeOutputFile)</c>, creating the parent
    /// directory when needed. Content is written as UTF-8 without BOM using <c>\n</c> line endings.
    /// </summary>
    /// <param name="projectDir">Directory of the consuming project.</param>
    /// <param name="relativeOutputFile">Output path, relative to <paramref name="projectDir"/>.</param>
    /// <param name="content">TypeScript source to write.</param>
    /// <param name="fullPath">Normalized absolute path of the output file.</param>
    /// <param name="error">I/O error message when the write fails; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when the file is up to date or was written; <c>false</c> when an I/O error occurred.</returns>
    public static bool TryWrite(string projectDir, string relativeOutputFile, string content, out string fullPath, out string? error)
    {
        // RS1035 bans file IO in analyzers. Writing the .ts file next to the project is this generator's purpose, and the
        // write is idempotent, so the diagnostic is suppressed for this method only.
#pragma warning disable RS1035
        fullPath = Path.GetFullPath(Path.Combine(projectDir, relativeOutputFile));
        error = null;

        var normalized = content.Replace("\r\n", "\n");

        try
        {
            if (File.Exists(fullPath) && string.Equals(File.ReadAllText(fullPath), normalized, StringComparison.Ordinal))
            {
                return true;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, normalized, Utf8WithoutBom);
            return true;
        }
        catch (IOException exception)
        {
            error = exception.Message;
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            error = exception.Message;
            return false;
        }
#pragma warning restore RS1035
    }
}
