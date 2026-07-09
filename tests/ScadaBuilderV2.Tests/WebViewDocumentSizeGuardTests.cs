using ScadaBuilderV2.Application.ElementStudio;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class WebViewDocumentSizeGuardTests
{
    [TestMethod]
    public void NullDocumentDoesNotExceedLimit()
    {
        Assert.IsFalse(WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(null));
    }

    [TestMethod]
    public void SmallDocumentDoesNotExceedLimit()
    {
        var document = new string('a', 1000);
        Assert.IsFalse(WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(document));
    }

    [TestMethod]
    public void DocumentAtLimitDoesNotExceedLimit()
    {
        var document = new string('a', WebViewDocumentSizeGuard.NavigateToStringMaxCharacters);
        Assert.IsFalse(WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(document));
    }

    [TestMethod]
    public void DocumentOverLimitExceedsLimit()
    {
        var document = new string('a', WebViewDocumentSizeGuard.NavigateToStringMaxCharacters + 1);
        Assert.IsTrue(WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(document));
    }
}
