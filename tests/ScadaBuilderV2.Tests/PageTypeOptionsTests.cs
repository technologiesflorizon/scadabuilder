using ScadaBuilderV2.App.Pages;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Locks the by-value mapping of the page-type selector: Fragment is retired from selection while every
/// remaining type, including the new QuickWindow one, round-trips by its enum value rather than by list
/// position (DEC-0052).
/// </summary>
[TestClass]
public sealed class PageTypeOptionsTests
{
    [TestMethod]
    public void FragmentIsNotSelectableAndQuickWindowIs()
    {
        var types = PageTypeOptions.Selectable.Select(option => option.Type).ToArray();

        CollectionAssert.DoesNotContain(types, ScadaPageType.Fragment);
        CollectionAssert.Contains(types, ScadaPageType.QuickWindow);
    }

    [TestMethod]
    public void EverySelectableTypeRoundTripsByValue()
    {
        foreach (var option in PageTypeOptions.Selectable)
        {
            Assert.AreEqual(option.Type, PageTypeOptions.Selectable.Single(candidate => candidate.Type == option.Type).Type);
        }
        Assert.AreEqual("Fenêtre rapide", PageTypeOptions.Label(ScadaPageType.QuickWindow));
        Assert.AreEqual("Fragment (obsolète)", PageTypeOptions.Label(ScadaPageType.Fragment));
    }

    [TestMethod]
    public void EveryPageTypeHasALabelAndSelectableOnesAreDistinct()
    {
        foreach (ScadaPageType type in Enum.GetValues<ScadaPageType>())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(PageTypeOptions.Label(type)), $"{type} must have a non-empty label.");
        }

        var labels = PageTypeOptions.Selectable.Select(option => option.Label).ToArray();
        CollectionAssert.AllItemsAreUnique(labels);
    }

    [TestMethod]
    public void ResolveRequestedTypeReturnsNullWhenNothingIsSelected()
    {
        // The state of a page still typed Fragment (absent from Selectable): no option selected must mean
        // "send no type-change request", never a silent fallback to Default (Task 8 fix round 1).
        Assert.IsNull(PageTypeOptions.ResolveRequestedType(null));
    }

    [TestMethod]
    public void ResolveRequestedTypeReturnsEachSelectableOptionsType()
    {
        foreach (var option in PageTypeOptions.Selectable)
        {
            Assert.AreEqual(option.Type, PageTypeOptions.ResolveRequestedType(option));
        }
    }
}
