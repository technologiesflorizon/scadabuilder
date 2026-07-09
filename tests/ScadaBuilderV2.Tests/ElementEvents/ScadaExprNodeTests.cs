using System.Text.Json;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaExprNodeTests
{
    [TestMethod]
    public void TagRef_WithTagId_RoundTripsThroughJson()
    {
        var tagRef = new ScadaExprTagRef("PE_16", "tf100.mapping.161");
        var json = JsonSerializer.Serialize<ScadaExprNode>(tagRef);

        // Default serialization uses PascalCase (no camelCase policy in tests)
        StringAssert.Contains(json, "\"TagName\":\"PE_16\"");
        StringAssert.Contains(json, "\"TagId\":\"tf100.mapping.161\"");

        var deserialized = JsonSerializer.Deserialize<ScadaExprNode>(json);
        Assert.IsInstanceOfType(deserialized, typeof(ScadaExprTagRef));
        var roundTripped = (ScadaExprTagRef)deserialized;
        Assert.AreEqual("PE_16", roundTripped.TagName);
        Assert.AreEqual("tf100.mapping.161", roundTripped.TagId);
    }

    [TestMethod]
    public void TagRef_WithoutTagId_OmitsTagIdFromJson()
    {
        var tagRef = new ScadaExprTagRef("PE_16");
        var json = JsonSerializer.Serialize<ScadaExprNode>(tagRef);

        StringAssert.Contains(json, "\"TagName\":\"PE_16\"");
        Assert.IsFalse(json.Contains("\"TagId\""),
            "Null TagId must not appear in serialized JSON.");
    }

    [TestMethod]
    public void TagRef_LegacyJsonWithoutTagId_DeserializesWithNullTagId()
    {
        var legacyJson = "{\"type\":\"tagRef\",\"TagName\":\"PE_16\"}";
        var deserialized = JsonSerializer.Deserialize<ScadaExprNode>(legacyJson);

        Assert.IsInstanceOfType(deserialized, typeof(ScadaExprTagRef));
        var tagRef = (ScadaExprTagRef)deserialized;
        Assert.AreEqual("PE_16", tagRef.TagName);
        Assert.IsNull(tagRef.TagId);
    }

    [TestMethod]
    public void BinaryNodeHoldsOperatorAndOperands()
    {
        ScadaExprNode left = new ScadaExprTagRef("Temp");
        ScadaExprNode right = new ScadaExprLiteralNumber(80);
        var node = new ScadaExprBinary(ScadaExprBinaryOp.GreaterThanOrEqual, left, right);

        Assert.AreEqual(ScadaExprBinaryOp.GreaterThanOrEqual, node.Op);
        Assert.AreEqual("Temp", ((ScadaExprTagRef)node.Left).TagName);
        Assert.AreEqual(80, ((ScadaExprLiteralNumber)node.Right).Value);
    }

    [TestMethod]
    public void FuncNodeHoldsNameAndArguments()
    {
        var node = new ScadaExprFunc("BIT", new ScadaExprNode[] { new ScadaExprTagRef("Status"), new ScadaExprLiteralNumber(3) });

        Assert.AreEqual("BIT", node.Name);
        Assert.AreEqual(2, node.Args.Count);
    }

    [TestMethod]
    public void ExprNode_RoundTripsThroughJsonWithTypeDiscriminator()
    {
        ScadaExprNode expr = new ScadaExprBinary(
            ScadaExprBinaryOp.GreaterThan,
            new ScadaExprTagRef("Temp"),
            new ScadaExprLiteralNumber(80));

        var json = JsonSerializer.Serialize(expr);

        StringAssert.Contains(json, "\"type\":\"binary\"");
        StringAssert.Contains(json, "\"type\":\"tagRef\"");
        StringAssert.Contains(json, "\"type\":\"literalNumber\"");

        var deserialized = JsonSerializer.Deserialize<ScadaExprNode>(json);

        Assert.IsNotNull(deserialized);
        Assert.IsInstanceOfType(deserialized, typeof(ScadaExprBinary));
        var binary = (ScadaExprBinary)deserialized;
        Assert.AreEqual(ScadaExprBinaryOp.GreaterThan, binary.Op);
        Assert.IsInstanceOfType(binary.Left, typeof(ScadaExprTagRef));
        Assert.AreEqual("Temp", ((ScadaExprTagRef)binary.Left).TagName);
        Assert.IsInstanceOfType(binary.Right, typeof(ScadaExprLiteralNumber));
        Assert.AreEqual(80, ((ScadaExprLiteralNumber)binary.Right).Value);
    }
}
