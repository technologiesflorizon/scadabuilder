using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementStudioEditorStateTests
{
    [TestMethod]
    public void ShiftSelectionAddsElement()
    {
        var editor = CreateEditor();

        editor.Select(["a"], ElementStudioSelectionMode.Replace);
        editor.Select(["b"], ElementStudioSelectionMode.Add);

        CollectionAssert.AreEquivalent(new[] { "a", "b" }, editor.SelectedIds.ToArray());
    }

    [TestMethod]
    public void AltSelectionRemovesElement()
    {
        var editor = CreateEditor();

        editor.Select(["a", "b", "c"], ElementStudioSelectionMode.Replace);
        editor.Select(["b"], ElementStudioSelectionMode.Remove);

        CollectionAssert.AreEquivalent(new[] { "a", "c" }, editor.SelectedIds.ToArray());
    }

    [TestMethod]
    public void RectangleSelectionCanReplaceAddAndRemove()
    {
        var editor = CreateEditor();

        editor.SelectIntersecting(new SceneBounds(0, 0, 12, 12), ElementStudioSelectionMode.Replace);
        CollectionAssert.AreEquivalent(new[] { "a" }, editor.SelectedIds.ToArray());

        editor.SelectIntersecting(new SceneBounds(18, 0, 20, 12), ElementStudioSelectionMode.Add);
        CollectionAssert.AreEquivalent(new[] { "a", "b" }, editor.SelectedIds.ToArray());

        editor.SelectIntersecting(new SceneBounds(0, 0, 12, 12), ElementStudioSelectionMode.Remove);
        CollectionAssert.AreEquivalent(new[] { "b" }, editor.SelectedIds.ToArray());
    }

    [TestMethod]
    public void MoveSelectionPreservesRelativeOffsets()
    {
        var editor = CreateEditor();
        editor.Select(["a", "b"], ElementStudioSelectionMode.Replace);

        editor.MoveSelectionBy(7, 3);

        var a = editor.Items.Single(item => item.Id == "a");
        var b = editor.Items.Single(item => item.Id == "b");
        Assert.AreEqual(7, a.Bounds.X);
        Assert.AreEqual(3, a.Bounds.Y);
        Assert.AreEqual(27, b.Bounds.X);
        Assert.AreEqual(3, b.Bounds.Y);
        Assert.AreEqual(20, b.Bounds.X - a.Bounds.X);
    }

    [TestMethod]
    public void DuplicateSelectionCreatesOffsetCopiesAndSelectsThem()
    {
        var editor = CreateEditor();
        editor.Select(["a", "b"], ElementStudioSelectionMode.Replace);

        editor.DuplicateSelection();

        Assert.AreEqual(5, editor.Items.Count);
        Assert.AreEqual(2, editor.SelectedIds.Count);
        Assert.IsTrue(editor.SelectedIds.All(id => id.Contains("-copy-", StringComparison.Ordinal)));
        Assert.IsTrue(editor.Items.Any(item => item.Id.StartsWith("a-copy-", StringComparison.Ordinal) && item.Bounds.X == 16));
        Assert.IsTrue(editor.Items.Any(item => item.Id.StartsWith("b-copy-", StringComparison.Ordinal) && item.Bounds.X == 36));
    }

    [TestMethod]
    public void CopyPasteCreatesNewOffsetSelection()
    {
        var editor = CreateEditor();
        editor.Select(["c"], ElementStudioSelectionMode.Replace);

        editor.CopySelection();
        editor.Select(["a"], ElementStudioSelectionMode.Replace);
        editor.PasteClipboard(5, 9);

        Assert.AreEqual(4, editor.Items.Count);
        var pastedId = editor.SelectedIds.Single();
        var pasted = editor.Items.Single(item => item.Id == pastedId);
        Assert.AreEqual(45, pasted.Bounds.X);
        Assert.AreEqual(9, pasted.Bounds.Y);
    }

    [TestMethod]
    public void DeleteSelectionRemovesItemsAndClearsSelection()
    {
        var editor = CreateEditor();
        editor.Select(["a", "c"], ElementStudioSelectionMode.Replace);

        editor.DeleteSelection();

        CollectionAssert.AreEqual(new[] { "b" }, editor.Items.Select(item => item.Id).ToArray());
        Assert.AreEqual(0, editor.SelectedIds.Count);
    }

    [TestMethod]
    public void AlignLeftAlignsSelectedItemsOnly()
    {
        var editor = CreateEditor();
        editor.Select(["b", "c"], ElementStudioSelectionMode.Replace);

        editor.AlignLeft();

        Assert.AreEqual(20, editor.Items.Single(item => item.Id == "b").Bounds.X);
        Assert.AreEqual(20, editor.Items.Single(item => item.Id == "c").Bounds.X);
        Assert.AreEqual(0, editor.Items.Single(item => item.Id == "a").Bounds.X);
    }

    [TestMethod]
    public void AlignRightAndBottomAlignSelectedItems()
    {
        var editor = CreateEditor();
        editor.Select(["a", "b"], ElementStudioSelectionMode.Replace);

        editor.AlignHorizontal(ElementStudioHorizontalAlignment.Right);
        editor.AlignVertical(ElementStudioVerticalAlignment.Bottom);

        Assert.AreEqual(20, editor.Items.Single(item => item.Id == "a").Bounds.X);
        Assert.AreEqual(20, editor.Items.Single(item => item.Id == "b").Bounds.X);
        Assert.AreEqual(0, editor.Items.Single(item => item.Id == "a").Bounds.Y);
        Assert.AreEqual(0, editor.Items.Single(item => item.Id == "b").Bounds.Y);
    }

    [TestMethod]
    public void DistributeHorizontalAndVerticalSpacesSelectedItems()
    {
        var editor = new ElementStudioEditorState(
        [
            new ElementStudioEditableItem("a", "Element001", "polygon", new SceneBounds(0, 0, 10, 10)),
            new ElementStudioEditableItem("b", "Element002", "line", new SceneBounds(80, 80, 10, 10)),
            new ElementStudioEditableItem("c", "Element003", "text", new SceneBounds(20, 20, 10, 10))
        ]);
        editor.Select(["a", "b", "c"], ElementStudioSelectionMode.Replace);

        editor.DistributeHorizontally();
        editor.DistributeVertically();

        Assert.AreEqual(0, editor.Items.Single(item => item.Id == "a").Bounds.X);
        Assert.AreEqual(40, editor.Items.Single(item => item.Id == "c").Bounds.X);
        Assert.AreEqual(80, editor.Items.Single(item => item.Id == "b").Bounds.X);
        Assert.AreEqual(0, editor.Items.Single(item => item.Id == "a").Bounds.Y);
        Assert.AreEqual(40, editor.Items.Single(item => item.Id == "c").Bounds.Y);
        Assert.AreEqual(80, editor.Items.Single(item => item.Id == "b").Bounds.Y);
    }

    [TestMethod]
    public void GroupAndUngroupSelectionAssignsLogicalGroupIds()
    {
        var editor = CreateEditor();
        editor.Select(["a", "b"], ElementStudioSelectionMode.Replace);

        var groupId = editor.GroupSelection();

        Assert.IsFalse(string.IsNullOrWhiteSpace(groupId));
        Assert.AreEqual(groupId, editor.Items.Single(item => item.Id == "a").GroupId);
        Assert.AreEqual(groupId, editor.Items.Single(item => item.Id == "b").GroupId);

        editor.UngroupSelection();

        Assert.IsNull(editor.Items.Single(item => item.Id == "a").GroupId);
        Assert.IsNull(editor.Items.Single(item => item.Id == "b").GroupId);
    }

    [TestMethod]
    public void LockedElementsAreNotSelectableOrMovable()
    {
        var editor = new ElementStudioEditorState(
        [
            new ElementStudioEditableItem("a", "Element001", "polygon", new SceneBounds(0, 0, 10, 10), IsLocked: true),
            new ElementStudioEditableItem("b", "Element002", "line", new SceneBounds(20, 0, 10, 10))
        ]);

        editor.Select(["a", "b"], ElementStudioSelectionMode.Replace);
        editor.MoveSelectionBy(15, 0);

        CollectionAssert.AreEquivalent(new[] { "b" }, editor.SelectedIds.ToArray());
        Assert.AreEqual(0, editor.Items.Single(item => item.Id == "a").Bounds.X);
        Assert.AreEqual(35, editor.Items.Single(item => item.Id == "b").Bounds.X);
    }

    [TestMethod]
    public void HiddenElementsAreNotSelectableAndCanBeRestoredByUndo()
    {
        var editor = CreateEditor();
        editor.Select(["a"], ElementStudioSelectionMode.Replace);

        editor.SetSelectionVisible(false);

        Assert.IsFalse(editor.Items.Single(item => item.Id == "a").IsVisible);
        Assert.AreEqual(0, editor.SelectedIds.Count);

        editor.Undo();

        Assert.IsTrue(editor.Items.Single(item => item.Id == "a").IsVisible);
        CollectionAssert.AreEquivalent(new[] { "a" }, editor.SelectedIds.ToArray());
    }

    [TestMethod]
    public void UndoAndRedoRestoreMutationSnapshots()
    {
        var editor = CreateEditor();
        editor.Select(["a", "b"], ElementStudioSelectionMode.Replace);

        editor.MoveSelectionBy(10, 0);
        editor.Undo();

        Assert.AreEqual(0, editor.Items.Single(item => item.Id == "a").Bounds.X);
        Assert.AreEqual(20, editor.Items.Single(item => item.Id == "b").Bounds.X);
        Assert.IsTrue(editor.CanRedo);

        editor.Redo();

        Assert.AreEqual(10, editor.Items.Single(item => item.Id == "a").Bounds.X);
        Assert.AreEqual(30, editor.Items.Single(item => item.Id == "b").Bounds.X);
    }

    [TestMethod]
    public void ResizeSelectionChangesSizeWithoutMovingItems()
    {
        var editor = CreateEditor();
        editor.Select(["a", "b"], ElementStudioSelectionMode.Replace);

        editor.ResizeSelectionBy(5, 7);

        Assert.AreEqual(0, editor.Items.Single(item => item.Id == "a").Bounds.X);
        Assert.AreEqual(15, editor.Items.Single(item => item.Id == "a").Bounds.Width);
        Assert.AreEqual(17, editor.Items.Single(item => item.Id == "b").Bounds.Height);
    }

    [TestMethod]
    public void SetSelectionBoundsAppliesCommonGeometry()
    {
        var editor = CreateEditor();
        editor.Select(["a", "b"], ElementStudioSelectionMode.Replace);

        editor.SetSelectionBounds(12, 14, 22, 24);

        foreach (var item in editor.Items.Where(item => item.Id is "a" or "b"))
        {
            Assert.AreEqual(12, item.Bounds.X);
            Assert.AreEqual(14, item.Bounds.Y);
            Assert.AreEqual(22, item.Bounds.Width);
            Assert.AreEqual(24, item.Bounds.Height);
        }
    }

    [TestMethod]
    public void EqualizeWidthAndHeightUsePrimarySelectedSize()
    {
        var editor = new ElementStudioEditorState(
        [
            new ElementStudioEditableItem("a", "Element001", "polygon", new SceneBounds(0, 0, 14, 18)),
            new ElementStudioEditableItem("b", "Element002", "line", new SceneBounds(20, 0, 30, 44))
        ]);
        editor.Select(["a", "b"], ElementStudioSelectionMode.Replace);

        editor.EqualizeWidth();
        editor.EqualizeHeight();

        Assert.AreEqual(14, editor.Items.Single(item => item.Id == "b").Bounds.Width);
        Assert.AreEqual(18, editor.Items.Single(item => item.Id == "b").Bounds.Height);
    }

    [TestMethod]
    public void CombinedSelectionMoveDuplicateSubtractAlignAndDeleteScenario()
    {
        var editor = CreateEditor();

        editor.Select(["a"], ElementStudioSelectionMode.Replace);
        editor.Select(["b"], ElementStudioSelectionMode.Add);
        editor.MoveSelectionBy(10, 5);
        editor.DuplicateSelection();
        var duplicateIds = editor.SelectedIds.ToArray();
        editor.Select([duplicateIds[0]], ElementStudioSelectionMode.Remove);
        editor.AlignLeft();
        editor.GroupSelection();
        editor.UngroupSelection();
        editor.ResizeSelectionBy(4, 6);
        editor.DeleteSelection();
        editor.Undo();
        editor.SetSelectionLocked(true);

        Assert.AreEqual(5, editor.Items.Count);
        Assert.IsTrue(editor.Items.Single(item => item.Id == duplicateIds[1]).IsLocked);
        Assert.IsTrue(editor.Items.Any(item => item.Id == "a" && item.Bounds.X == 10 && item.Bounds.Y == 5));
        Assert.IsTrue(editor.Items.Any(item => item.Id == "b" && item.Bounds.X == 30 && item.Bounds.Y == 5));
        Assert.IsTrue(editor.Items.Any(item => item.Id == "c"));
        Assert.IsTrue(editor.Items.Any(item => item.Id == duplicateIds[0]));
        Assert.AreEqual(0, editor.SelectedIds.Count);
    }

    private static ElementStudioEditorState CreateEditor()
    {
        return new ElementStudioEditorState(
        [
            new ElementStudioEditableItem("a", "Element001", "polygon", new SceneBounds(0, 0, 10, 10)),
            new ElementStudioEditableItem("b", "Element002", "line", new SceneBounds(20, 0, 10, 10)),
            new ElementStudioEditableItem("c", "Element003", "text", new SceneBounds(40, 0, 10, 10))
        ]);
    }
}
