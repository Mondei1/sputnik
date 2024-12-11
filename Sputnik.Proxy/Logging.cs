using System.Globalization;

namespace Sputnik.Proxy;

internal static class Logging
{
    static string Now { get => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); }

    public static void LogInfo(string message)
    {
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.Write("Info");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" {Now}] {message}");
    }

    public static void LogReqest(string message)
    {
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("Req");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" {Now}] {message}");
    }

    public static void LogResponse(string message)
    {
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("Res");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" {Now}] {message}");
    }

    public static void LogDebug(string message)
    {
        if (!Program.DEBUG)
        {
            return;
        }

        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.DarkBlue;
        Console.Write("Dbg");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" {Now}] {message}");
    }

    public static void LogWarn(string message)
    {
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("Warn");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" {Now}] {message}");
    }

    public static void LogConnection(string message)
    {
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("Conn");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" {Now}] {message}");
    }
}
