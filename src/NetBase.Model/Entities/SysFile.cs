using SqlSugar;
using System.Text.Json.Serialization;

namespace NetBase.Model.Entities;

/// <summary>上传文件记录（元数据；内容落盘 ContentRoot/uploads）</summary>
[SugarTable("sys_file", TableDescription = "上传文件表")]
public class SysFile : BaseEntity
{
    /// <summary>原始文件名</summary>
    [SugarColumn(Length = 255, ColumnDescription = "原始文件名")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>磁盘存储名（uuid.扩展名）</summary>
    [SugarColumn(Length = 200, ColumnDescription = "存储名")]
    public string StorageName { get; set; } = string.Empty;

    /// <summary>相对存储目录（yyyyMMdd）</summary>
    [SugarColumn(Length = 20, ColumnDescription = "存储目录")]
    public string StorageDir { get; set; } = string.Empty;

    /// <summary>文件大小（字节）</summary>
    [SugarColumn(ColumnDescription = "文件大小(字节)")]
    public long Size { get; set; }

    /// <summary>内容类型</summary>
    [SugarColumn(IsNullable = true, Length = 100, ColumnDescription = "内容类型")]
    public string? ContentType { get; set; }

    /// <summary>业务类型（avatar 等归类）</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "业务类型")]
    public string? BizType { get; set; }

    /// <summary>上传人ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "上传人ID")]
    public long UploadUserId { get; set; }
}
