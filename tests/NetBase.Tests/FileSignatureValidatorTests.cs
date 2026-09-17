using System.Text;
using NetBase.Common.Security;
using Xunit;

namespace NetBase.Tests;

/// <summary>文件内容签名校验测试：扩展名与文件头比对，拦截改后缀伪装</summary>
public class FileSignatureValidatorTests
{
    private static MemoryStream Stream(params byte[] bytes) => new(bytes);

    [Fact]
    public void RealSignatures_ShouldPass()
    {
        Assert.True(FileSignatureValidator.IsValid(".png", Stream(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)));
        Assert.True(FileSignatureValidator.IsValid(".jpg", Stream(0xFF, 0xD8, 0xFF, 0xE0)));
        Assert.True(FileSignatureValidator.IsValid(".pdf", Stream("%PDF-1.7"u8.ToArray())));
        Assert.True(FileSignatureValidator.IsValid(".zip", Stream(0x50, 0x4B, 0x03, 0x04)));
        Assert.True(FileSignatureValidator.IsValid(".docx", Stream(0x50, 0x4B, 0x03, 0x04)));
        Assert.True(FileSignatureValidator.IsValid(".doc", Stream(0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1)));
        // webp：RIFF....WEBP
        var webp = new byte[12];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(webp, 0);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(webp, 8);
        Assert.True(FileSignatureValidator.IsValid(".webp", Stream(webp)));
    }

    [Fact]
    public void DisguisedContent_ShouldReject()
    {
        // 把可执行/文本伪装成图片
        Assert.False(FileSignatureValidator.IsValid(".png", Stream(0x4D, 0x5A, 0x90, 0x00))); // MZ
        Assert.False(FileSignatureValidator.IsValid(".jpg", Stream((byte)'H', (byte)'i')));
        Assert.False(FileSignatureValidator.IsValid(".pdf", Stream(0xFF, 0xD8, 0xFF)));
        // webp 缺 WEBP 标记
        Assert.False(FileSignatureValidator.IsValid(".webp", Stream(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0)));
    }

    [Fact]
    public void SignaturelessOrUnknownTypes_ShouldPassThrough()
    {
        // txt 无签名：内容任意均放行（交由扩展名白名单管控）
        Assert.True(FileSignatureValidator.IsValid(".txt", Stream(0x4D, 0x5A)));
        // 未登记扩展名：跳过内容比对
        Assert.True(FileSignatureValidator.IsValid(".abc", Stream(1, 2, 3)));
    }

    [Fact]
    public void StreamPosition_ShouldBeRestored()
    {
        using var ms = Stream(0x89, 0x50, 0x4E, 0x47, 9, 9, 9, 9);
        ms.Seek(4, SeekOrigin.Begin);
        FileSignatureValidator.IsValid(".png", ms);
        Assert.Equal(4, ms.Position); // 校验后位置还原，不影响调用方后续读取
    }
}
