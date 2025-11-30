using System.Threading;
using System.Threading.Tasks;
using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces
{
    /// <summary>
    /// Interface for hierarchical decomposition service
    /// </summary>
    public interface IHierarchicalDecompositionService
    {
        Task<Models.ModuleTree> DecomposeHierarchicallyAsync(string repositoryPath, CancellationToken cancellationToken = default);
    }
}
