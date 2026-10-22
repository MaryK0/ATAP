using Xunit.Abstractions;
using Xunit.Sdk;

namespace ATAP.BusinessLogic.Tests.TestOrdering;

/// <summary>
/// Требование ЛР: "предусмотреть запуск тестов в случайном порядке".
/// xUnit по умолчанию выполняет тесты внутри класса в порядке их обнаружения рефлексией
/// (детерминированно, но не гарантированно = порядку объявления в файле). Этот orderer
/// явно перемешивает набор test case'ов внутри каждого класса на каждый запуск — так
/// регрессии, зависящие от порядка (shared state, "тест Б работает только после теста А"),
/// становятся видимыми.
///
/// Подключается через <c>[assembly: TestCaseOrderer(...)]</c> — см. <see cref="AssemblyInfo"/>.
/// Пара — <see cref="RandomTestCollectionOrderer"/> (перемешивает порядок между классами);
/// оба используют один общий <see cref="TestOrderSeed.RunSeed"/> на весь процесс.
///
/// Воспроизводимость: seed можно задать переменной окружения <c>TEST_ORDER_SEED</c>; без неё
/// берётся <see cref="Environment.TickCount"/>. Фактический seed печатается в консоль и
/// сохраняется в файл <c>last-seed.txt</c> (см. <see cref="TestOrderSeed.SaveToFile"/>).
/// </summary>
public class RandomTestCaseOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        var materialized = testCases.ToList();
        if (materialized.Count == 0)
            return materialized;

        var runSeed = TestOrderSeed.RunSeed; // один и тот же для всех классов этого прогона
        var className = materialized[0].TestMethod.TestClass.Class.Name;

        // Seed на класс выводится из общего runSeed + имени класса (через StableHash, не через
        // string.GetHashCode()/HashCode.Combine — тот рандомизирован на каждый запуск процесса),
        // иначе все классы получили бы одну и ту же перестановку.
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
