﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Arc4u.Diagnostics
{
    public class LoggerMessage
    {
        static LoggerMessage()
        {
            try
            {
                ProcessId = Process.GetCurrentProcess().Id;
            }
            catch (PlatformNotSupportedException)
            {
                ProcessId = -1;
            }
        }

        private static int ProcessId { get; }
        private readonly ILogger _logger;

        internal LoggerMessage(ILogger logger, MessageCategory category, string methodName, string typeClass)
        {
            _logger = logger;
            MethodName = methodName;
            TypeClass = typeClass;
            Category = category;
            Properties = new Dictionary<string, object>();
        }

        public MessageCategory Category { get; }
        public LogLevel LogLevel { get; set; }
        public string Text { get; set; }
        public string StackTrace { get; set; }


        public string MethodName { get; }
        public string TypeClass { get; }
        public object[] Args { get; set; }


        internal Dictionary<string, object> Properties { get; }
        internal Exception Exception { get; set; }


        internal void Log()
        {
            if (LogLevel < LoggerBase.FilterLevel) return;

            if (null == _logger) return;

            if (null != LoggerContext.Current?.All())
            {
                foreach (var property in LoggerContext.Current.All())
                    Properties.AddIfNotExist(property.Key, property.Value);
            }

            Properties.AddIfNotExist(LoggingConstants.Application, LoggerBase.Application);
            Properties.AddIfNotExist(LoggingConstants.ThreadId, Thread.CurrentThread.ManagedThreadId);
            Properties.AddIfNotExist(LoggingConstants.Class, TypeClass);
            Properties.AddIfNotExist(LoggingConstants.MethodName, MethodName);
            Properties.AddIfNotExist(LoggingConstants.ProcessId, ProcessId);
            Properties.AddIfNotExist(LoggingConstants.Category, (short)Category);
            Properties.AddIfNotExist(LoggingConstants.Stacktrace, StackTrace);

            // Add the internal Arc4u properties to whatever the TState provides already before logging
            var stateLogger = new StateLogger(Properties, _logger);
            stateLogger.Log(LogLevel, 0, Exception, Text, Args);
        }
    }



    class StateLogger : ILogger
    {
        private readonly IReadOnlyDictionary<string, object> _properties;
        private readonly ILogger _logger;

        public StateLogger(IReadOnlyDictionary<string, object> properties, ILogger logger)
        {
            _properties = properties;
            _logger = logger;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            throw new NotImplementedException();
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object>> pairs)
            {
                Dictionary<string, object> mergedState = new();
                foreach (KeyValuePair<string, object> pair in pairs)
                    mergedState[pair.Key] = pair.Value;

                foreach (var property in _properties)
                    mergedState.AddIfNotExist(property.Key, property.Value);

                _logger.Log(logLevel, eventId, mergedState, exception, LocalFormatter);

                // we can do this since the template is not aware of the internal Arc4u properties.
                string LocalFormatter(Dictionary<string, object> extendedState, Exception e) => formatter(state, exception);
            }
            else
                _logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }


}