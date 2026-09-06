namespace NetBase.Common.Results;

/// <summary>分页查询基类（作为请求参数）</summary>
public class PageQuery
{
    private int _pageIndex = 1;
    private int _pageSize = 20;

    /// <summary>页码，从 1 开始</summary>
    public int PageIndex
    {
        get => _pageIndex;
        set => _pageIndex = value < 1 ? 1 : value;
    }

    /// <summary>每页条数，上限 200</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > 200 ? 20 : value;
    }

    /// <summary>排序列名（实体属性名，不区分大小写；无效值回退主键 Id）</summary>
    public string? SortField { get; set; }

    /// <summary>是否降序，默认 true</summary>
    public bool SortDesc { get; set; } = true;
}

/// <summary>分页返回结果</summary>
public class PageResult<T>
{
    /// <summary>当前页数据</summary>
    public List<T> Items { get; set; } = [];

    /// <summary>总记录数</summary>
    public int Total { get; set; }

    /// <summary>页码</summary>
    public int PageIndex { get; set; }

    /// <summary>每页条数</summary>
    public int PageSize { get; set; }

    /// <summary>总页数</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    public static PageResult<T> Of(List<T> items, long total, int pageIndex, int pageSize) =>
        new() { Items = items, Total = (int)total, PageIndex = pageIndex, PageSize = pageSize };
}
