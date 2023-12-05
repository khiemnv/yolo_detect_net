using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ConsoleApp1
{
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
            Trace.TraceError("[{0}({1}:{2})] {3}", methodName, GetName(fileName), lineNumber, message);
        }

        public static void Debug(string message,
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string methodName = null,
        [CallerFilePath] string fileName = null)
        {
            Trace.WriteLine(string.Format("[{0}({1}:{2})] {3}", methodName, GetName(fileName), lineNumber, message));
        }
    }
}
