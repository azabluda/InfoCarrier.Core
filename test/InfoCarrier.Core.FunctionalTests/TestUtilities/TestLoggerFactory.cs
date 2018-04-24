// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using System;
    using Microsoft.Extensions.Logging;
    using Xunit.Abstractions;

    public class TestLoggerFactory : ILoggerFactory
    {
        private readonly TestLogger logger = new TestLogger();

        public static ITestOutputHelper TestOutputHelper { get; set; }

        public ILogger CreateLogger(string categoryName) => this.logger;

        public void AddProvider(ILoggerProvider provider)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        private class TestLogger : ILogger
        {
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                => TestOutputHelper?.WriteLine(formatter(state, exception));

            public bool IsEnabled(LogLevel logLevel) => TestOutputHelper != null;

            public IDisposable BeginScope<TState>(TState state) => new NullDisposable();

            private class NullDisposable : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}
