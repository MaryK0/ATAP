using Xunit;

// Тот же приём, что в ATAP.BusinessLogic.Tests — см. пояснения там (RandomTestCaseOrderer.cs).
// Отдельная реализация не нужна: используем orderer из BusinessLogic.Tests было бы кросс-сборочной
// зависимостью тестового проекта от другого тестового проекта, поэтому здесь — тонкая копия.
[assembly: TestCaseOrderer("ATAP.DataAccess.Tests.TestOrdering.RandomTestCaseOrderer", "ATAP.DataAccess.Tests")]
