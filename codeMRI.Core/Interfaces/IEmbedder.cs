namespace codeMRI.Core.Interfaces;

public interface IEmbedder
{
    Task<float[]> EmbedAsync(string text);
    Task<List<float[]>> EmbedBatchAsync(IEnumerable<string> texts);
}
