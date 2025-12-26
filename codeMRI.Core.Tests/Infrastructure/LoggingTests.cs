using System.Linq;
using NUnit.Framework;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.InMemory;

namespace codeMRI.Core.Tests.Infrastructure
{
    [TestFixture]
    public class LoggingTests
    {
        [Test]
        public void Verify_Caller_Info_Is_Missing_By_Default()
        {
            // Arrange
            var logger = new LoggerConfiguration()
                .WriteTo.InMemory()
                .CreateLogger();

            // Act
            logger.Information("This is a test log message");

            // Assert
            var logEvent = InMemorySink.Instance.LogEvents.LastOrDefault();
            Assert.That(logEvent, Is.Not.Null);
            Assert.That(logEvent.MessageTemplate.Text, Is.EqualTo("This is a test log message"));
            
            // By default, CallerMemberName is not explicitly captured unless enriched
            Assert.That(logEvent.Properties.ContainsKey("CallerMemberName"), Is.False, "CallerMemberName should be missing by default");
        }

        [Test]
        public void Verify_Log_Contains_Caller_Info_When_Enriched()
        {
            // Arrange
            var logger = new LoggerConfiguration()
                .Enrich.With(new codeMRI.Server.Infrastructure.Logging.CallerInfoEnricher())
                .WriteTo.InMemory()
                .CreateLogger();

            // Act
            logger.Information("This is a test log message with call info");

            // Assert
            var logEvent = InMemorySink.Instance.LogEvents.LastOrDefault(l => l.MessageTemplate.Text == "This is a test log message with call info");
            Assert.That(logEvent, Is.Not.Null);
            
            Assert.That(logEvent.Properties.ContainsKey("CallerMemberName"), Is.True, "CallerMemberName should be present");
            Assert.That(logEvent.Properties["CallerMemberName"].ToString(), Contains.Substring("Verify_Log_Contains_Caller_Info_When_Enriched"));
            
            Assert.That(logEvent.Properties.ContainsKey("CallerFilePath"), Is.True, "CallerFilePath should be present");
            Assert.That(logEvent.Properties.ContainsKey("CallerLineNumber"), Is.True, "CallerLineNumber should be present");
        }
    }
}
