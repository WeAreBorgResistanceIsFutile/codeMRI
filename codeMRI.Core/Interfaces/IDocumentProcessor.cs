using codeMRI.Shared.Models;

namespace codeMRI.Core.Interfaces;

public interface IDocumentProcessor
{
    IEnumerable<Document> Split(Document original, int chunkSize = 350, int overlap = 100);
}