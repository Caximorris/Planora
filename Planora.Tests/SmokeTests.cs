namespace Planora.Tests;

/// <summary>
/// Harness smoke test: confirms the test project builds against Planora.Api and
/// the xUnit runner executes. Real behavioral tests (IDOR, lockout, refresh
/// reuse) are added on top of the WebApplicationFactory fixture.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void TestHarness_IsWiredUp()
    {
        Assert.True(true);
    }
}
