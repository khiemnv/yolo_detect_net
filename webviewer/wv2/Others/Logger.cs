using System.Diagnostics;
using System.Runtime.CompilerServices;

public class Logger
{
    static string GetName(string fileName)
    {
        return System.IO.Path.GetFileName(fileName);
    }
    public static void Error(string message,
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string methodName = null,
        [CallerFilePath] string fileName = null)
    {
        var dt = DateTimeOffset.Now.ToString("u");
        Trace.TraceError("[{4}] [{0}({1}:{2})] {3}", methodName, GetName(fileName), lineNumber, message, dt);
    }

    public static void Debug(string message,
    [CallerLineNumber] int lineNumber = 0,
    [CallerMemberName] string methodName = null,
    [CallerFilePath] string fileName = null)
    {
        var dt = DateTimeOffset.Now.ToString("u");
        Trace.TraceInformation(string.Format("[{4}] [{0}({1}:{2})] {3}", methodName, GetName(fileName), lineNumber, message, dt));
    }
}
