using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace wbackup
{

    public enum BackupMopde
    {
        Incremental,
        Differencial,
        Full
    };

    class CommandParser
    {
        public int maxRetryCount = 1000;
        public string sourceDir;
        public string destinationDir;
        public string logFileName;

        public BackupMopde backupMode = BackupMopde.Full;

        public CommandParser(string[] args)
        {
            bool isSource = false;
            bool isDestination = false;
            bool isLogFile = false;

            foreach (var arg in args)
            {
                if ('-' == arg.First() || '/' == arg.First())
                {
                    string command = arg.Substring(1);
                    if ("full" == command.ToLower())
                    {
                        backupMode = BackupMopde.Full;
                    }
                    if ("incremental" == command.ToLower())
                    {
                        backupMode = BackupMopde.Incremental;
                    }
                    if ("differencial" == command.ToLower())
                    {
                        backupMode = BackupMopde.Differencial;
                    }

                    if ("srce" == command.ToLower())
                    {
                        isSource = true;
                    }
                    if ("dest" == command.ToLower())
                    {
                        isDestination = true;
                    }
                    if ("log" == command.ToLower())
                    {
                        isLogFile = true;
                    }
                }
                else
                {
                    if (isSource)
                    {
                        sourceDir = arg;
                        isSource = false;
                    }

                    if (isDestination)
                    {
                        destinationDir = arg;
                        isDestination = false;
                    }

                    if (isLogFile)
                    {
                        logFileName = arg;
                        isLogFile = false;
                    }
                }
            }
        }
    }
}
