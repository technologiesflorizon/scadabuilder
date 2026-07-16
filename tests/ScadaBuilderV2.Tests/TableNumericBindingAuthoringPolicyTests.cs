using ScadaBuilderV2.Application.Tables;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableNumericBindingAuthoringPolicyTests
{
    [TestMethod]
    public void NormalizeKeepsEmptyDraftEmpty()
    {
        var result = TableNumericBindingAuthoringPolicy.Normalize(null, null, isReadOnly: false);

        Assert.IsNull(result.ReadTagId);
        Assert.IsNull(result.WriteTagId);
        Assert.IsFalse(result.ReadDefaultedFromWrite);
    }

    [TestMethod]
    public void NormalizeDefaultsMissingReadBindingFromWriteBinding()
    {
        var result = TableNumericBindingAuthoringPolicy.Normalize(null, " tag.write ", isReadOnly: false);

        Assert.AreEqual("tag.write", result.ReadTagId);
        Assert.AreEqual("tag.write", result.WriteTagId);
        Assert.IsTrue(result.ReadDefaultedFromWrite);
    }

    [TestMethod]
    public void NormalizePreservesDistinctReadBinding()
    {
        var result = TableNumericBindingAuthoringPolicy.Normalize("tag.read", "tag.write", isReadOnly: false);

        Assert.AreEqual("tag.read", result.ReadTagId);
        Assert.AreEqual("tag.write", result.WriteTagId);
        Assert.IsFalse(result.ReadDefaultedFromWrite);
    }

    [TestMethod]
    public void NormalizeRemovesWriteBindingForReadOnlyInput()
    {
        var result = TableNumericBindingAuthoringPolicy.Normalize(null, "tag.write", isReadOnly: true);

        Assert.IsNull(result.ReadTagId);
        Assert.IsNull(result.WriteTagId);
        Assert.IsFalse(result.ReadDefaultedFromWrite);
    }

    [TestMethod]
    public void NormalizePreservesReadWhenWriteIsRemoved()
    {
        var result = TableNumericBindingAuthoringPolicy.Normalize("tag.read", null, isReadOnly: false);

        Assert.AreEqual("tag.read", result.ReadTagId);
        Assert.IsNull(result.WriteTagId);
        Assert.IsFalse(result.ReadDefaultedFromWrite);
    }
}
