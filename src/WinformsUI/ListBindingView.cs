using System.Collections;
using System.ComponentModel;

namespace WinformsUI;

/// <summary>BindingList with a private display order; the caller's List is never rearranged.</summary>
internal sealed class ListBindingView<T> : BindingList<T> where T : class
{
    private readonly List<T> _source;
    private Func<T, bool>? _filter;
    private PropertyDescriptor? _sortProperty;
    private ListSortDirection _sortDirection;
    private int _page;
    private int _pageSize;
    internal int TotalCount { get; private set; }
    internal int Page => _page;

    internal ListBindingView(List<T> source)
    {
        _source = source;
        AllowNew = false;
        Refresh();
    }

    protected override bool SupportsSortingCore => true;
    protected override bool IsSortedCore => _sortProperty is not null;
    protected override PropertyDescriptor? SortPropertyCore => _sortProperty;
    protected override ListSortDirection SortDirectionCore => _sortDirection;

    protected override void ApplySortCore(PropertyDescriptor property, ListSortDirection direction)
    {
        _sortProperty = property;
        _sortDirection = direction;
        Refresh();
    }

    protected override void RemoveSortCore()
    {
        _sortProperty = null;
        Refresh();
    }

    internal void SetFilter(Func<T, bool>? predicate)
    {
        _filter = predicate;
        _page = 0;
        Refresh();
    }

    internal void SetPage(int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page); ArgumentOutOfRangeException.ThrowIfNegative(pageSize);
        _page = page; _pageSize = pageSize; Refresh();
    }

    internal void Refresh()
    {
        if (_source.Any(item => item is null))
            throw new ArgumentException("データのリストにnull要素を含めることはできません。");
        IEnumerable<T> items = _filter is null ? _source : _source.Where(_filter);
        if (_sortProperty is { } property)
        {
            var comparer = Comparer<object?>.Create(CompareValues);
            items = _sortDirection == ListSortDirection.Ascending
                ? items.OrderBy(item => property.GetValue(item), comparer)
                : items.OrderByDescending(item => property.GetValue(item), comparer);
        }
        var all = items.ToArray();
        TotalCount = all.Length;
        if (_pageSize > 0) _page = Math.Clamp(_page, 0, Math.Max(0, (TotalCount - 1) / _pageSize));
        var snapshot = _pageSize == 0 ? all : all.Skip(_page * _pageSize).Take(_pageSize).ToArray();
        RaiseListChangedEvents = false;
        try
        {
            ClearItems();
            foreach (var item in snapshot) Add(item);
        }
        finally { RaiseListChangedEvents = true; }
        ResetBindings();
    }

    private static int CompareValues(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;
        if (left is IComparable) return Comparer.Default.Compare(left, right);
        return StringComparer.CurrentCulture.Compare(left.ToString(), right.ToString());
    }
}
