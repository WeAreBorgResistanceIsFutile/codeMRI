using System.Diagnostics;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace codeMRI.Server.Infrastructure.Logging
{
    public class CallerInfoEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent.Level == LogEventLevel.Information) return;

            var skip = 3;
            while (true)
            {
                var stack = new StackTrace(skip, true);
                if (stack.FrameCount == 0) return;

                var frame = stack.GetFrame(0);
                var method = frame?.GetMethod();
                var declaringType = method?.DeclaringType;

                if (declaringType == null)
                {
                    skip++;
                    continue;
                }

                var assemblyName = declaringType.Assembly.GetName().Name;
                if (assemblyName != null &&
                    (assemblyName.StartsWith("Serilog") ||
                     assemblyName.StartsWith("Microsoft.Extensions.Logging") ||
                     assemblyName.StartsWith("System")))
                {
                    skip++;
                    continue;
                }

                // Found the first non-logging frame
                var callerMethod = method?.Name;
                var callerFile = frame?.GetFileName();
                var callerLine = frame?.GetFileLineNumber();

                if (callerMethod != null)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CallerMemberName", callerMethod));
                }

                if (callerFile != null)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CallerFilePath", callerFile));
                }

                if (callerLine != 0)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CallerLineNumber", callerLine));
                }

                return;
            }
        }
    }
}
