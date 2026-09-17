namespace NetBase.Common.Security;

/// <summary>
/// 文件内容签名（magic bytes）校验：扩展名与文件头比对，拦截改后缀伪装。
/// 无签名的类型（如 txt）跳过校验；未知扩展名放行（扩展名白名单已先行过滤）。
/// </summary>
public static class FileSignatureValidator
{
    /// <summary>每种扩展名的合法文件头候选（任一前缀命中即通过）</summary>
    private static readonly Dictionary<string, byte[][]> Signatures = new()
    {
        [".jpg"] = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
        [".png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        [".gif"] = [System.Text.Encoding.ASCII.GetBytes("GIF8")],
        [".webp"] = [], // 特殊：RIFF....WEBP，见 IsWebP
        [".pdf"] = [System.Text.Encoding.ASCII.GetBytes("%PDF")],
        [".zip"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 },
                    new byte[] { 0x50, 0x4B, 0x05, 0x06 },
                    new byte[] { 0x50, 0x4B, 0x07, 0x08 }],
        [".docx"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }],
        [".xlsx"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }],
        [".doc"] = [new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }]
    };

    /// <summary>校验流内容与扩展名是否相符。校验后流位置重置回起点。</summary>
    public static bool IsValid(string extension, Stream content)
    {
        var ext = extension.ToLowerInvariant();

        // webp 特检（RIFF....WEBP，跨偏移双标记）须在无签名判断之前
        if (ext == ".webp")
        {
            return CheckWebP(content);
        }

        if (!Signatures.TryGetValue(ext, out var candidates) || candidates.Length == 0)
        {
            return true; // 无签名类型（txt 等）或未登记扩展名：跳过内容比对
        }

        var originalPosition = content.CanSeek ? content.Position : -1;
        Span<byte> head = stackalloc byte[12];
        int read;
        try
        {
            if (!content.CanSeek)
            {
                return true; // 不可回读的流无法安全嗅探，交由后续处理
            }
            content.Seek(0, SeekOrigin.Begin);
            read = content.Read(head);
        }
        finally
        {
            if (content.CanSeek)
            {
                content.Seek(originalPosition < 0 ? 0 : originalPosition, SeekOrigin.Begin);
            }
        }

        // 内容长度不足以容纳任何候选签名的，按不匹配拒绝（防止截断文件绕过）
        var minLength = candidates.Min(sig => sig.Length);
        if (read < minLength)
        {
            return false;
        }

        foreach (var sig in candidates)
        {
            if (read >= sig.Length && head[..sig.Length].SequenceEqual(sig))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>webp：RIFF 容器头 + 偏移 8 处 WEBP 标记</summary>
    private static bool CheckWebP(Stream content)
    {
        var originalPosition = content.CanSeek ? content.Position : -1;
        Span<byte> head = stackalloc byte[12];
        int read;
        var ok = false;
        try
        {
            content.Seek(0, SeekOrigin.Begin);
            read = content.Read(head);
            ok = read >= 12
                && head[0] == (byte)'R' && head[1] == (byte)'I' && head[2] == (byte)'F' && head[3] == (byte)'F'
                && head[8] == (byte)'W' && head[9] == (byte)'E' && head[10] == (byte)'B' && head[11] == (byte)'P';
        }
        finally
        {
            if (content.CanSeek)
            {
                content.Seek(originalPosition < 0 ? 0 : originalPosition, SeekOrigin.Begin);
            }
        }
        return ok;
    }
}
