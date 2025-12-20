namespace codeMRI.Server.Api;

public class ProgressInfo
{
    public string Phase { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Percentage { get; set; }
}