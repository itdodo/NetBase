using System.ComponentModel.DataAnnotations;

namespace NetBase.Common.Validation;

/// <summary>
/// 可选手机号：null / 空白字符串视为未填写（通过），有值时按手机号格式校验。
/// 替代 [Phone]——后者对空字符串也会做格式校验，导致选填字段无法留空。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class OptionalPhoneAttribute : DataTypeAttribute
{
    public OptionalPhoneAttribute() : base(DataType.PhoneNumber) => ErrorMessage = "手机号格式不正确";

    public override bool IsValid(object? value)
    {
        if (value is not string text)
        {
            return true;
        }
        return string.IsNullOrWhiteSpace(text) || new PhoneAttribute().IsValid(text);
    }
}

/// <summary>可选邮箱：null / 空白字符串视为未填写（通过），有值时按邮箱格式校验</summary>
[AttributeUsage(AttributeTargets.Property)]
public class OptionalEmailAttribute : DataTypeAttribute
{
    public OptionalEmailAttribute() : base(DataType.EmailAddress) => ErrorMessage = "邮箱格式不正确";

    public override bool IsValid(object? value)
    {
        if (value is not string text)
        {
            return true;
        }
        return string.IsNullOrWhiteSpace(text) || new EmailAddressAttribute().IsValid(text);
    }
}
