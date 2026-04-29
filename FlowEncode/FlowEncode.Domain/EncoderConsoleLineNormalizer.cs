namespace FlowEncode.Domain;

public static class EncoderConsoleLineNormalizer
{
    public static string Normalize(string? line)
    {
        return ConsoleOutputLineNormalizer.Normalize(line);
    }
}
