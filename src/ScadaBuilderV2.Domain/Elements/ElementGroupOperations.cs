using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Domain.Elements;

public static class ElementGroupOperations
{
    public static ElementGroup Group(string groupId, string groupName, IEnumerable<Element> elements)
    {
        var selected = elements.ToArray();
        if (selected.Length == 0)
        {
            throw new InvalidOperationException("A group requires at least one element.");
        }

        ValidateGroupInput(groupId, selected);

        var groupBounds = CalculateOuterBounds(selected.Select(element => element.Bounds));
        var group = new ElementGroup(groupId, groupName, groupBounds);
        foreach (var element in selected)
        {
            var absolute = element.Bounds;
            element.AttachToParent(
                group.Id,
                new SceneBounds(
                    absolute.X - groupBounds.X,
                    absolute.Y - groupBounds.Y,
                    absolute.Width,
                    absolute.Height));
            group.AddChild(element);
        }

        return group;
    }

    public static IReadOnlyList<Element> Ungroup(ElementGroup group)
    {
        var ungrouped = group.Children.ToArray();
        foreach (var child in ungrouped)
        {
            child.DetachFromParent(ToAbsoluteChildBounds(group, child));
            group.RemoveChild(child.Id);
        }

        return ungrouped;
    }

    public static void MoveGroupBy(ElementGroup group, double deltaX, double deltaY)
    {
        group.Bounds = group.Bounds with
        {
            X = group.Bounds.X + deltaX,
            Y = group.Bounds.Y + deltaY
        };
        group.RegenerateCode();
    }

    public static void MoveChildRelative(ElementGroup parent, Element child, double relativeX, double relativeY)
    {
        if (!parent.Children.Any(existing => existing.Id == child.Id))
        {
            throw new InvalidOperationException($"Element '{child.Id}' is not a direct child of group '{parent.Id}'.");
        }

        child.AttachToParent(
            parent.Id,
            child.Bounds with
            {
                X = relativeX,
                Y = relativeY
            });
    }

    public static SceneBounds GetAbsoluteBounds(Element element, SceneBounds? parentAbsoluteBounds = null)
    {
        if (parentAbsoluteBounds is null || element.Layout.PositionMode == ElementPositionMode.Absolute)
        {
            return element.Bounds;
        }

        return new SceneBounds(
            parentAbsoluteBounds.X + element.Bounds.X,
            parentAbsoluteBounds.Y + element.Bounds.Y,
            element.Bounds.Width,
            element.Bounds.Height);
    }

    private static SceneBounds ToAbsoluteChildBounds(ElementGroup group, Element child)
    {
        return new SceneBounds(
            group.Bounds.X + child.Bounds.X,
            group.Bounds.Y + child.Bounds.Y,
            child.Bounds.Width,
            child.Bounds.Height);
    }

    private static SceneBounds CalculateOuterBounds(IEnumerable<SceneBounds> bounds)
    {
        var items = bounds.ToArray();
        var left = items.Min(item => item.X);
        var top = items.Min(item => item.Y);
        var right = items.Max(item => item.X + item.Width);
        var bottom = items.Max(item => item.Y + item.Height);
        return new SceneBounds(left, top, right - left, bottom - top);
    }

    private static void ValidateGroupInput(string groupId, IReadOnlyList<Element> selected)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            throw new ArgumentException("Group id is required.", nameof(groupId));
        }

        if (selected.Any(element => element.Id == groupId))
        {
            throw new InvalidOperationException("A group cannot contain itself.");
        }

        var selectedIds = selected.Select(element => element.Id).ToArray();
        if (selectedIds.Distinct(StringComparer.Ordinal).Count() != selectedIds.Length)
        {
            throw new InvalidOperationException("A group cannot contain the same element more than once.");
        }

        foreach (var group in selected.OfType<ElementGroup>())
        {
            if (group.ContainsDescendant(groupId))
            {
                throw new InvalidOperationException("A group cannot contain one of its ancestors.");
            }

            var nestedSelected = selectedIds
                .Where(id => group.ContainsDescendant(id))
                .ToArray();
            if (nestedSelected.Length > 0)
            {
                throw new InvalidOperationException("A group selection cannot contain both a group and one of its descendants.");
            }
        }
    }
}
