using ScadaBuilderV2.Application.Tables;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableCellAddressTests
{
    [DataTestMethod]
    [DataRow(0, 0, "A1")]
    [DataRow(6, 9, "J7")]
    [DataRow(0, 25, "Z1")]
    [DataRow(0, 26, "AA1")]
    [DataRow(41, 51, "AZ42")]
    [DataRow(0, 52, "BA1")]
    [DataRow(63, 63, "BL64")]
    public void FromZeroBasedFormatsSpreadsheetAddresses(int row, int column, string expected)
    {
        Assert.AreEqual(expected, TableCellAddress.FromZeroBased(row, column));
    }

    [TestMethod]
    public void FromZeroBasedRejectsNegativeCoordinates()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => TableCellAddress.FromZeroBased(-1, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => TableCellAddress.FromZeroBased(0, -1));
    }
}
