using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using ICSharpCode.SharpZipLib.Zip;
using System.Threading.Tasks;

namespace wbackup
{
    class Program
    {
        public static CommandParser param;

        static void Main(string[] args)
        {
            param = new CommandParser(args);
            ConcurrentQueue<string> srceFilesQueue = new ConcurrentQueue<string>(Directory.GetFiles(param.sourceDir, "*.*", SearchOption.AllDirectories));

            Parallel.For(0, 4, i => 
            {
                var nameTrans = new ZipNameTransform();
                var zf = ZipFile.Create(param.destinationDir + "-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + i.ToString("00") + ".zip");
                zf.BeginUpdate();

                while (srceFilesQueue.TryDequeue(out string srceFileName))
                {
                    if (File.GetAttributes(srceFileName).HasFlag(FileAttributes.Archive))
                    {
                        string f = nameTrans.TransformFile(srceFileName);

                        zf.Add(srceFileName, f);
                    }
                }

                zf.CommitUpdate();
            });

            Console.WriteLine("Hello World!");
        }
    }
}
