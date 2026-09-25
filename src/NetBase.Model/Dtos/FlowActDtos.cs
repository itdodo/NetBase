using System.ComponentModel.DataAnnotations;

namespace NetBase.Model.Dtos;

/// <summary>审批操作请求</summary>
public class FlowActDto
{
    /// <summary>动作：approve-同意 reject-拒绝 return-驳回至节点</summary>
    [RegularExpression("^(approve|reject|return)$", ErrorMessage = "动作仅支持 approve/reject/return")]
    public string Action { get; set; } = "approve";

    /// <summary>驳回目标节点编码（action=return 时必填；start=退回发起人）</summary>
    public string? ReturnNodeCode { get; set; }

    /// <summary>审批意见</summary>
    [StringLength(500)]
    public string? Comment { get; set; }
}

/// <summary>转办请求</summary>
public class FlowTransferDto
{
    /// <summary>转办目标人</summary>
    [Required(ErrorMessage = "请选择转办目标人")]
    public List<long> UserIds { get; set; } = [];

    /// <summary>转办说明</summary>
    [StringLength(500)]
    public string? Comment { get; set; }
}

/// <summary>加签请求</summary>
public class FlowAddSignDto
{
    /// <summary>加签人</summary>
    [Required(ErrorMessage = "请选择加签人")]
    public List<long> UserIds { get; set; } = [];

    /// <summary>true-前加签（并入当前节点共同把关）false-后加签（本节点通过后追加审批）</summary>
    public bool Before { get; set; } = true;

    /// <summary>加签说明</summary>
    [StringLength(500)]
    public string? Comment { get; set; }
}
