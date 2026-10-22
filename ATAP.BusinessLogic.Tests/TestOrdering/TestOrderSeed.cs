namespace ATAP.BusinessLogic.Tests.TestOrdering;

/// <summary>
/// Единый источник seed'а для ОБОИХ orderer'ов (<see cref="RandomTestCaseOrderer"/> и
/// <see cref="RandomTestCollectionOrderer"/>) в рамках одного процесса (testhost).
///
/// Раньше каждый orderer сам вызывал Environment.TickCount при каждом своём вызове — а так как
/// RandomTestCaseOrderer вызывается ОТДЕЛЬНО на каждый класс, разные классы в одном и том же
/// прогоне получали РАЗНЫЙ runSeed (TickCount успевал чуть сдвинуться между вызовами на разных
/// классах, особенно при параллельном выполнении). Из-за этого ни сохранённый в файл, ни
/// переданный через TEST_ORDER_SEED seed не воспроизводил прогон целиком — только часть классов.
///
/// Решение: runSeed вычисляется один раз на весь процесс — `static readonly` поле, инициализируется
/// при первом обращении и переиспользуется всеми вызовами обоих orderer'ов до конца прогона.
/// </summary>
internal static class TestOrderSeed
{
    public static readonly int RunSeed = Compute();

    private static int Compute()
    {
        var fromEnv = Environment.GetEnvironmentVariable("TEST_ORDER_SEED");
        return int.TryParse(fromEnv, out var seed) ? seed : Environment.TickCount;
    }

    /// <summary>Стабильный (не зависящий от рандомизации string.GetHashCode() в .NET) хэш строки.</summary>
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

    /// <summary>
    /// Пишет RunSeed в файл рядом со сборкой (best-effort — ошибка записи не должна ломать тесты).
    /// Вызывается из обоих orderer'ов; перезаписывает одним и тем же значением — безопасно
    /// вызывать многократно за один прогон.
    /// </summary>
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
            // best-effort — отсутствие файла не должно ломать сам прогон тестов
        }
    }
}
