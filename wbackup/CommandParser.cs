using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace wbackup
{
    class CommandParser
    {
        public string sourceDir;
        public string destinationDir;

        public CommandParser(string[] args)
        {
            bool isSource = false; ;
            bool isDestination = false;

            foreach (var arg in args)
            {
                if ('-' == arg.First() || '/' == arg.First())
                {
                    string command = arg.Substring(1);
                    if ("srce" == command.ToLower())
                    {
                        isSource = true;
                    }
                    if ("dest" == command.ToLower())
                    {
                        isDestination = true;
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
                }
            }
        }
    }
}
