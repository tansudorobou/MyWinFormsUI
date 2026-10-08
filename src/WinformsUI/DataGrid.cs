using System.ComponentModel;

namespace WinformsUI;

/// <summary>Common grid behavior used by the search panel. Create a DataGrid&lt;T&gt; for a List&lt;T&gt;.</summary>
public abstract class DataGrid : UserControl
{
    protected readonly BindingSource Source = new();

    protected DataGrid()
    {
        Margin = Padding.Empty;
        MinimumSize = new Size(0, 160);
        Grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, DataSource = Source
        };
        Controls.Add(Grid);
        Source.ListChanged += (_, _) => ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    public DataGridView Grid { get; }
    public int RowCount => Source.Count;
    public event EventHandler? ViewChanged;

    internal abstract PropertyDescriptor FindProperty(string name);
    internal abstract bool HasDataSource { get; }
    internal abstract void ApplySearch(Func<object, bool> predicate);
    public abstract void ClearFilter();
    public abstract void RefreshData();

    protected override void Dispose(bool disposing)
    {
        if (disposing) Source.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>A native grid over model instances. DisplayName supplies column headers.</summary>
public sealed class DataGrid<T> : DataGrid where T : class
{
    private readonly PropertyDescriptorCollection _properties = TypeDescriptor.GetProperties(typeof(T));
    private ListBindingView<T>? _view;

    public DataGrid() => GenerateColumns();

    /// <summary>The displayed model instances after filtering and sorting.</summary>
    public IReadOnlyList<T> Items => _view is null ? Array.Empty<T>() : _view;
    public T? SelectedItem => Grid.CurrentRow?.DataBoundItem as T;
    public IReadOnlyList<T> SelectedItems => Grid.SelectedRows.Cast<DataGridViewRow>().Select(row => row.DataBoundItem).OfType<T>().ToArray();
    public int FilteredCount => _view?.TotalCount ?? 0;
    internal override bool HasDataSource => _view is not null;

    public DataGrid<T> SetData(List<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var view = new ListBindingView<T>(items);
        var oldView = _view;
        _view = view;
        Source.DataSource = view;
        oldView?.Clear();
        return this;
    }

    public DataGrid<T> FormatColumn(string propertyName, string format)
    {
        var column = Grid.Columns[propertyName]
            ?? throw new ArgumentException($"表示列 '{propertyName}' がありません。", nameof(propertyName));
        column.DefaultCellStyle.Format = format;
        return this;
    }

    public DataGrid<T> SetColumnVisible(string propertyName, bool visible)
    {
        var column = Grid.Columns[propertyName] ?? throw new ArgumentException("表示列がありません。", nameof(propertyName));
        column.Visible = visible; return this;
    }

    public void SetPage(int page, int pageSize) => RequireView().SetPage(page, pageSize);

    /// <summary>Connect paging controls. Dispose the returned binding before reusing either control.</summary>
    public IDisposable ConnectPagination(Pagination pagination)
    {
        RequireView();
        bool updating = false;
        void UpdatePage(object? sender, EventArgs e)
        {
            if (updating) return;
            updating = true;
            try { SetPage(pagination.Page, pagination.PageSize); pagination.SetTotal(FilteredCount); }
            finally { updating = false; }
        }
        void UpdateTotal(object? sender, EventArgs e)
        {
            if (updating) return;
            updating = true;
            try { pagination.SetTotal(FilteredCount); pagination.Page = RequireView().Page; }
            finally { updating = false; }
        }
        pagination.PageChanged += UpdatePage; ViewChanged += UpdateTotal;
        UpdatePage(null, EventArgs.Empty);
        return new CallbackDisposable(() => { pagination.PageChanged -= UpdatePage; ViewChanged -= UpdateTotal; });
    }

    public void ApplyFilter(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        RequireView().SetFilter(predicate);
    }

    internal override void ApplySearch(Func<object, bool> predicate) => ApplyFilter(item => predicate(item));
    public override void ClearFilter() => RequireView().SetFilter(null);

    /// <summary>Reflect changes to the original List while retaining the current filter and sort.</summary>
    public override void RefreshData() => RequireView().Refresh();

    internal override PropertyDescriptor FindProperty(string name) => _properties.Find(name, false)
        ?? throw new ArgumentException($"{typeof(T).Name} にプロパティ '{name}' がありません。", nameof(name));

    private ListBindingView<T> RequireView() => _view
        ?? throw new InvalidOperationException("SetDataを先に呼び出してください。");

    private void GenerateColumns()
    {
        foreach (PropertyDescriptor property in _properties)
        {
            var info = typeof(T).GetProperty(property.Name);
            if (!property.IsBrowsable || info?.GetMethod?.IsPublic != true || info.GetIndexParameters().Length != 0) continue;
            Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            DataGridViewColumn column = type == typeof(bool)
                ? new DataGridViewCheckBoxColumn { ThreeState = Nullable.GetUnderlyingType(property.PropertyType) is not null }
                : new DataGridViewTextBoxColumn();
            column.Name = property.Name;
            column.DataPropertyName = property.Name;
            column.HeaderText = property.DisplayName;
            column.ValueType = property.PropertyType;
            column.SortMode = DataGridViewColumnSortMode.Automatic;
            Grid.Columns.Add(column);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _view?.Clear();
    }
}

internal sealed class CallbackDisposable(Action dispose) : IDisposable
{
    private Action? _dispose = dispose;
    public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
}
