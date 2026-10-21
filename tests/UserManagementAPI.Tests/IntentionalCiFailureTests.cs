using Xunit;
namespace UserManagementAPI.Tests;
// Isolated evidence branch only. Never merge this deliberate failure.
public sealed class IntentionalCiFailureTests
{
    [Fact]
    public void DeliberateFailureForWorkflowVerification() => Assert.Fail("Intentional CI failure proof for issue #18; do not merge this branch.");
}
