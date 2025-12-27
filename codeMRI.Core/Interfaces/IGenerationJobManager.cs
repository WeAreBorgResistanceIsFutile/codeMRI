using System.Collections.Generic;
using System.Threading.Tasks;
using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IGenerationJobManager
{
    Task<GenerationJob> StartJobAsync(string repoPath, StructureRequest request, string? connectionId = null);
    Task<GenerationJob?> GetJobAsync(string jobId);
    Task<List<GenerationJob>> ListActiveJobsAsync();
    Task<List<GenerationJob>> ListAllJobsAsync();
    Task CancelJobAsync(string jobId);
    Task DeleteJobAsync(string jobId);
}
