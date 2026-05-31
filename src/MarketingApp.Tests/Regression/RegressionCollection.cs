namespace MarketingApp.Tests.Regression;

/// <summary>
/// xUnit collection definition — every regression test class joins this
/// collection so they all share the same RegressionTestFactory instance
/// (one DB lifecycle per test run) and never run in parallel against each other.
/// </summary>
[CollectionDefinition("Regression")]
public class RegressionCollection : ICollectionFixture<RegressionTestFactory>
{
    // intentionally empty — xUnit picks this up by the [CollectionDefinition] attribute
}
