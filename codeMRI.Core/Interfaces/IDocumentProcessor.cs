namespace codeMRI.Core.Interfaces;

using Models;

public interface IDocumentProcessor
{
    IEnumerable<Document> Split(Document original, int chunkSize = 350, int overlap = 100);
}