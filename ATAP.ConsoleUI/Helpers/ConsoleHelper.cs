namespace ATAP.ConsoleUI.Helpers;

internal static class ConsoleHelper
{
    public static void PrintHeader(string title)
    {
        var border = new string('=', Math.Max(title.Length + 4, 50));
        Console.WriteLine(border);
        Console.WriteLine($"  {title}");
        Console.WriteLine(border);
        Console.WriteLine();
    }

    public static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[OK] {message}");
        Console.ResetColor();
    }

    public static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[ERR] {message}");
        Console.ResetColor();
    }

    public static void Pause()
    {
        Console.WriteLine("\nНажмите Enter для продолжения...");
        Console.ReadLine();
    }

    public static string ReadPassword()
    {
        var pwd = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && pwd.Length > 0)
            {
                pwd.Remove(pwd.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (key.Key != ConsoleKey.Backspace)
            {
                pwd.Append(key.KeyChar);
                Console.Write('*');
            }
        }
        Console.WriteLine();
        return pwd.ToString();
    }
}
