using Xunit;

// Подключаем случайный порядок выполнения тестов на уровне всей сборки (требование ЛР №1, п.10).
[assembly: TestCaseOrderer("ATAP.BusinessLogic.Tests.TestOrdering.RandomTestCaseOrderer", "ATAP.BusinessLogic.Tests")]
[assembly: TestCollectionOrderer("ATAP.BusinessLogic.Tests.TestOrdering.RandomTestCollectionOrderer", "ATAP.BusinessLogic.Tests")]
