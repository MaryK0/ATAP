namespace ATAP.DataAccess.Tests.TestOrdering;

/// <summary>
/// См. ATAP.BusinessLogic.Tests/TestOrdering/TestOrderSeed.cs — тот же приём: один seed на весь
/// процесс (static readonly), не пересчитывается на каждый вызов orderer'а.
/// </summary>
internal static class TestOrderSeed
{
    public static readonly int RunSeed = Compute();

    private static int Compute()
    {
        var fromEnv = Environment.GetEnvironmentVariable("TEST_ORDER_SEED");
        return int.TryParse(fromEnv, out var seed) ? seed : Environment.TickCount;
    }

    public static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in value)
                hash = hash * 31 + c;
            return hash;
        }
    }

    public static void SaveToFile()
    {
        try
        {
            File.WriteAllText("last-seed.txt",
                $"{RunSeed}{Environment.NewLine}" +
                $"# записано {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                $"# чтобы повторить этот прогон целиком: TEST_ORDER_SEED={RunSeed} dotnet test ...{Environment.NewLine}");
        }
        catch
        {
            // best-effort
        }
    }
}
