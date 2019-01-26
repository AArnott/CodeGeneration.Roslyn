// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn
{
    using System;

    /// <summary>
    /// Defines level and behavior expected on given message.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Informational content, logged in verbose outputs.
        /// </summary>
        Info = 0,

        /// <summary>
        /// Warning, impacting build in non-fatal way. May stop build pipeline (when treating warnings as errors).
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Unrecoverable error, stops build pipeline.
        /// </summary>
        Error = 2,
    }

    /// <summary>
    /// Logs messages in MSBuild-recognized format to standard output.
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// Log message to build output with <see cref="LogLevel.Error"/>. Will fail build.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="diagnosticCode">Diagnostic code to prepend message with.</param>
        public static void Error(string message, string diagnosticCode)
            => Log(LogLevel.Error, message, diagnosticCode);

        /// <summary>
        /// Log message to build output with <see cref="LogLevel.Warning"/>. May fail build.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="diagnosticCode">Diagnostic code to prepend message with.</param>
        public static void Warning(string message, string diagnosticCode)
            => Log(LogLevel.Warning, message, diagnosticCode);

        /// <summary>
        /// Log message to build output with <see cref="LogLevel.Info"/>.
        /// </summary>
        /// <param name="message">Message to log.</param>
        public static void Info(string message)
            => Log(LogLevel.Info, message);

        /// <summary>
        /// Log message to build output.
        /// </summary>
        /// <param name="logLevel">Level with which it'll be logged, may impact build result.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="diagnosticCode">Diagnostic code to prepend message with.</param>
        public static void Log(LogLevel logLevel, string message, string diagnosticCode = null)
        {
            // Prefix every Line with loglevel
            var begin = 0;
            var end = message.IndexOf('\n');
            bool foundR = end > 0 && message[end - 1] == '\r';
            if (foundR)
            {
                end--;
            }

            while (end != -1)
            {
                Print(message.Substring(begin, end - begin));
                begin = end + (foundR ? 2 : 1);
                end = message.IndexOf('\n', begin);
                foundR = end > 0 && message[end - 1] == '\r';
                if (foundR)
                {
                    end--;
                }
            }

            Print(message.Substring(begin, message.Length - begin));

            void Print(string toPrint)
            {
                if (logLevel == LogLevel.Info)
                {
                    Console.WriteLine(toPrint);
                }
                else
                {
                    // log using MSBuild convention of [Origin:] [Subcategory] Category Code: [Text]
                    Console.WriteLine($"dotnet-codegen: {logLevel} {diagnosticCode}: {toPrint}");
                }
            }
        }
    }
}
