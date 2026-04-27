namespace FlowEncode.Domain;

public static class EncoderArgumentValueNormalizer
{
    public static string NormalizePresetForCli(EncoderKind kind, string preset)
    {
        var normalized = preset?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return kind == EncoderKind.SvtAv1 ? normalized : normalized.ToLowerInvariant();
    }

    public static string NormalizeTuneForCli(EncoderKind kind, string tune)
    {
        var normalized = tune?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return kind switch
        {
            EncoderKind.SvtAv1 => NormalizeSvtTuneToCliValue(normalized),
            _ => normalized.ToLowerInvariant()
        };
    }

    public static string NormalizeProfileForCli(EncoderKind kind, string profile)
    {
        var normalized = profile?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return kind switch
        {
            EncoderKind.SvtAv1 => NormalizeSvtProfileToCliValue(normalized),
            _ => normalized.ToLowerInvariant()
        };
    }

    public static string NormalizeTuneForUi(EncoderKind kind, string tune)
    {
        var normalized = tune?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return kind switch
        {
            EncoderKind.SvtAv1 => NormalizeSvtTuneToUiValue(normalized),
            _ => normalized
        };
    }

    public static string NormalizeProfileForUi(EncoderKind kind, string profile)
    {
        var normalized = profile?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return kind switch
        {
            EncoderKind.SvtAv1 => NormalizeSvtProfileToUiValue(normalized),
            _ => normalized
        };
    }

    private static string NormalizeSvtTuneToCliValue(string tune)
    {
        return tune.ToLowerInvariant() switch
        {
            "0" or "vq" => "0",
            "1" or "psnr" => "1",
            "2" or "ssim" => "2",
            "3" or "iq" or "image-quality" or "image_quality" or "image quality" => "3",
            "4" or "ms-ssim" or "ms_ssim" or "ms ssim" or "subjective-ssim" or "subjective_ssim" or "subjective ssim" or "still-picture" or "still_picture" or "still picture" or "stillpicture" => "4",
            _ => tune
        };
    }

    private static string NormalizeSvtTuneToUiValue(string tune)
    {
        return NormalizeSvtTuneToCliValue(tune) switch
        {
            "0" => "VQ",
            "1" => "PSNR",
            "2" => "SSIM",
            "3" => "IQ",
            "4" => "MS-SSIM",
            _ => tune
        };
    }

    private static string NormalizeSvtProfileToCliValue(string profile)
    {
        return profile.ToLowerInvariant() switch
        {
            "0" or "main" => "0",
            "1" or "high" => "1",
            "2" or "professional" => "2",
            _ => profile
        };
    }

    private static string NormalizeSvtProfileToUiValue(string profile)
    {
        return NormalizeSvtProfileToCliValue(profile) switch
        {
            "0" => "main",
            "1" => "high",
            "2" => "professional",
            _ => profile
        };
    }
}
