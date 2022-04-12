namespace lidaex;

internal static class Con
{
    public static int WarnCounter { get; private set; }
    public static int ErrorCounter { get; private set; }

    public static void Info(string message)
    {
        Console.WriteLine(message);
    }

    public static void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
        ++WarnCounter;
    }

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
        ++ErrorCounter;
    }
}