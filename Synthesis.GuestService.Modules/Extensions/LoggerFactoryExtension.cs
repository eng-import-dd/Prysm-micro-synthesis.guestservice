using System;
using Synthesis.Logging;

namespace Synthesis.GuestService.Extensions
{
    public static class LoggerFactoryExtensions
    {
        public static ILogger GetLogger(this ILoggerFactory factory, string topic)
        {
            return factory.Get(new LogTopic(topic));
        }

        public static ILogger GetLogger(this ILoggerFactory factory, Type topic)
        {
            return factory.Get(new LogTopic(topic.FullName));
        }

        public static ILogger GetLogger(this ILoggerFactory factory, object topic)
        {
            return factory.Get(new LogTopic(topic.GetType().FullName));
        }
    }
}