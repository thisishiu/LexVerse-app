namespace LexVerse.Core.Pipeline;

public sealed class PipelineStageException(string stage, Exception inner) : Exception(stage + "-failed", inner)
{
    public string Stage { get; } = stage;
}
