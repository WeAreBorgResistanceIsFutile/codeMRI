using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using codeMRI.MCP.Services;
using codeMRI.Core.Services;
using codeMRI.Core.Interfaces;
using codeMRI.Agents.Services;
using codeMRI.Infrastructure.Services;
using codeMRI.Infrastructure.Configuration;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System;
using System.Linq;
using codeMRI.Core.Models;

namespace codeMRI.MCP.Tests.Services
{
    [TestFixture]
    public class GraphIndexServiceTests
    {
        private ServiceProvider _serviceProvider;
        private GraphIndexService _graphIndexService;

        [SetUp]
        public void Setup()
        {
            var services = new ServiceCollection();

            // Logging
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

            // Configuration for AST Service
            services.Configure<ASTServiceSettings>(options =>
            {
                options.Enabled = true; // Enabled so it enters ParseCodeAsync
                options.BaseUrl = "http://localhost:8000"; // Dummy, shouldn't be hit for C#
                options.TimeoutSeconds = 30;
            });

            // Core Services
            services.AddSingleton<IProgressService, ProgressService>();
            
            // Infrastructure Services
            services.AddHttpClient<IASTServiceClient, ASTServiceClient>();
            services.AddSingleton<ICSharpParser, RoslynCSharpParser>();

            // Agent Services
            services.AddSingleton<IComponentIdentificationService, ComponentIdentificationService>();

            // Enhanced Graph Service
            services.AddSingleton<IEnhancedDependencyGraphService, EnhancedDependencyGraphService>();

            // MCP Services
            services.AddSingleton<IndexStateService>();
            services.AddSingleton<GraphIndexService>();

            _serviceProvider = services.BuildServiceProvider();
            _graphIndexService = _serviceProvider.GetRequiredService<GraphIndexService>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        [Test]
        public async Task IndexRepositoryAsync_ShouldIdentifyCodeFiles_AndPopulateGraph()
        {
            // Arrange
            // Point to the actual codeMRI repository root
            var repositoryPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."));
            
            // Cleanup existing index to force re-indexing with new parser
            var indexDir = Path.Combine(repositoryPath, ".codemri");
            var indexFile = Path.Combine(indexDir, "graph_index.json");
            if (File.Exists(indexFile))
            {
                File.Delete(indexFile);
            }

            // Act
            await _graphIndexService.IndexRepositoryAsync(repositoryPath);
            
            // Assert
            var stats = _graphIndexService.GetStatistics();
            
            await TestContext.Out.WriteLineAsync($"Indexed {stats.nodeCount} nodes and {stats.edgeCount} edges.");

            Assert.That(stats.nodeCount, Is.GreaterThan(0), "Graph should contain nodes after indexing codeMRI repo");
            Assert.That(stats.edgeCount, Is.GreaterThan(0), "Graph should contain edges after indexing codeMRI repo");
            
            // Verify that Enums are indexed correctly
            var enums = _graphIndexService.FindNodesByType("enums");
            
            var enumNode = enums.FirstOrDefault(n => n.Id.Contains($"::{nameof(ArchitecturalPatternType)}"));
            
            Assert.That(enumNode, Is.Not.Null, "Should have found ArchitecturalPatternType enum node");
            Assert.That(enumNode.FilePath, Is.Not.Null.And.Not.Empty, "Enum node should have FilePath set");
        }
    }
}
