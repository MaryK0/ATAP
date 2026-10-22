using Xunit.Abstractions;
using Xunit.Sdk;

namespace ATAP.DataAccess.Tests.TestOrdering;

/// <summary>
/// См. ATAP.BusinessLogic.Tests/TestOrdering/RandomTestCaseOrderer.cs — та же идея, включая
/// воспроизводимость через переменную окружения <c>TEST_ORDER_SEED</c> и единый
/// <see cref="TestOrderSeed.RunSeed"/> на весь процесс.
/// </summary>
public class RandomTestCaseOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        var materialized = testCases.ToList();
        if (materialized.Count == 0)
            return materialized;

        var runSeed = TestOrderSeed.RunSeed;
        var className = materialized[0].TestMethod.TestClass.Class.Name;
        var classSeed = unchecked(runSeed * 397 ^ TestOrderSeed.StableHash(className));
        var random = new Random(classSeed);

        var ordered = materialized.OrderBy(_ => random.Next()).ToList();
        Console.WriteLine(
            $"[RandomTestCaseOrderer] runSeed={runSeed} class={className} cases={ordered.Count}, " +
            $"order={string.Join(",", ordered.Select(c => c.TestMethod.Method.Name))}");
        TestOrderSeed.SaveToFile();
        return ordered;
    }
}
