using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGeneration.Roslyn
{
    public enum LogLevel
    {
        /// <summary>
        /// High importance, appears in less verbose logs
        /// </summary>
        High = 0,

        /// <summary>
        /// Normal importance
        /// </summary>
        Normal = 1,

        /// <summary>
        /// Low importance, appears in more verbose logs
        /// </summary>
        Low = 2,
    }
    public static class Logger
    {
        public static void Log(LogLevel logLevel, string message)
        {
            // Prefix every Line with loglevel
            var begin = 0;
            var end = message.IndexOf('\n');
            bool foundR = end > 0 && message[end - 1] == '\r';
            if(foundR)
                end--;
            while (end != -1)
            {
                Print(message.Substring(begin, end - begin));
                begin = end + (foundR ? 2 : 1);
                end = message.IndexOf('\n', begin);
                foundR = end > 0 && message[end - 1] == '\r';
                if(foundR)
                    end--;
            }
            Print(message.Substring(begin, message.Length - begin));

            void Print(string toPrint) => Console.WriteLine($"::{logLevel}::{toPrint}");
        }
    }
}
