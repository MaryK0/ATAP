using Xunit;
using Xunit.Abstractions;

namespace ATAP.BusinessLogic.Tests.TestOrdering;

/// <summary>
/// Перемешивает порядок между test collections (по умолчанию — одна на класс тестов).
/// Использует тот же общий <see cref="TestOrderSeed.RunSeed"/>, что и
/// <see cref="RandomTestCaseOrderer"/> — один seed воспроизводит весь прогон целиком
/// (и порядок классов, и порядок тестов внутри каждого), а не только часть.
/// </summary>
public class RandomTestCollectionOrderer : ITestCollectionOrderer
{
    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections)
    {
        var runSeed = TestOrderSeed.RunSeed;
        var random = new Random(runSeed);
        var ordered = testCollections.OrderBy(_ => random.Next()).ToList();
        Console.WriteLine(
            $"[RandomTestCollectionOrderer] runSeed={runSeed}, order={string.Join(",", ordered.Select(c => c.DisplayName))}");
        TestOrderSeed.SaveToFile();
        return ordered;
    }
}
