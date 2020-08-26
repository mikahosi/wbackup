using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System.Threading.Tasks;

namespace wbackup
{
    class Program
    {
        public static CommandParser param;

        static void Main(string[] args)
        {
            var sw = new System.Diagnostics.Stopwatch();

            string[] storedTarget = { ".mov", ".mp4", ".mpg", ".mpeg", ".jpg", ".jpeg", ".png", ".gif", ".zip" };

            param = new CommandParser(args);
            ConcurrentQueue<string> srceFilesQueue = new ConcurrentQueue<string>(Directory.GetFiles(param.sourceDir, "*.*", SearchOption.AllDirectories));

            string postFixString = DateTime.Now.ToString("yyyyMMddHHmmss");

            StreamWriter logws = null;
            if (param.logFileName != "")
            {
                logws = File.CreateText(param.logFileName + "-" + postFixString + ".log");
            }
            int totalFileCount = srceFilesQueue.Count;
            int archiveFileCount = 0;
            int exceptionCount = 0;
            Mutex consoleLock = new Mutex();

            sw.Start();

            Parallel.For(0, 1, i =>
            {
                // アーカイブ先ファイルのインスタンスを作成
                string zipFileName = param.destinationDir + "-" + postFixString + "-" + i.ToString("00") + ".tar.gz";
                using (var fos = new FileStream(zipFileName, FileMode.Create, FileAccess.Write))
                using (GZipOutputStream bzo = new GZipOutputStream(fos))
                using (TarOutputStream zos = new TarOutputStream(bzo))
                {
                    bzo.IsStreamOwner = false;
                    bzo.SetLevel(9);

                    int buffSelect = 0;
                    int buffSize = zos.RecordSize;
                    byte[][] readBuffer = new byte[2][];
                    readBuffer[0] = new byte[buffSize];
                    readBuffer[1] = new byte[buffSize];

                    while (srceFilesQueue.TryDequeue(out string srceFileName))
                    {
                        try
                        {
                            var attribute = File.GetAttributes(srceFileName);
                            if (attribute.HasFlag(FileAttributes.Archive) || param.backupMode == BackupMopde.Full)
                            {
                                if (consoleLock.WaitOne())
                                {
                                    Console.WriteLine("Archive, {0}", srceFileName);
                                    if (logws != null)
                                        logws.WriteLine("Archive, {0}", srceFileName);
                                    consoleLock.ReleaseMutex();
                                }


                                // バックアップ対象を出力先に転記する
                                using (var fis = new FileStream(srceFileName, FileMode.Open, FileAccess.Read))
                                {
                                    // ZIPファイルヘッダ作成
                                    TarEntry tarEntry = TarEntry.CreateEntryFromFile(srceFileName);
                                    zos.PutNextEntry(tarEntry);

                                    Task<int> readResult = fis.ReadAsync(readBuffer[buffSelect], 0, buffSize);
                                    while (true)
                                    {
                                        readResult.Wait();
                                        int readSize = readResult.Result;
                                        if (readSize <= 0)
                                        {
                                            break;
                                        }

                                        Task writeResult = zos.WriteAsync(readBuffer[buffSelect], 0, readSize);

                                        buffSelect ^= 1;
                                        readResult = fis.ReadAsync(readBuffer[buffSelect], 0, buffSize);

                                        writeResult.Wait();
                                    }

                                    zos.CloseEntry();
                                }

                                // アーカイブフラグを解除する
                                if (param.backupMode == BackupMopde.Full || param.backupMode == BackupMopde.Differencial)
                                {
                                    File.SetAttributes(srceFileName, (attribute & (~FileAttributes.Archive)));
                                }

                                Interlocked.Increment(ref archiveFileCount);
                            }
                            else
                            {
                                // アーカイブフラグがないファイルをスキップする
                                if (consoleLock.WaitOne())
                                {
                                    Console.WriteLine("Skip, {0}", srceFileName);
                                    if (logws != null)
                                        logws.WriteLine("Skip, {0}", srceFileName);
                                    consoleLock.ReleaseMutex();
                                }
                            }
                        }
                        catch (Exception exp)
                        {
                            srceFilesQueue.Enqueue(srceFileName);

                            var curtExceptionCnt = Interlocked.Increment(ref exceptionCount);

                            if (consoleLock.WaitOne())
                            {
                                Console.WriteLine("Exception {0}, {1}", curtExceptionCnt, exp.ToString());
                                if (logws != null)
                                    logws.WriteLine("Exception {0}, {1}", curtExceptionCnt, exp.ToString());
                                consoleLock.ReleaseMutex();
                            }
                        }

                        if (exceptionCount > param.maxRetryCount)
                            break;
                    }

                    zos.Flush();
                    zos.Close();
                }
            });

            sw.Stop();
            Console.WriteLine("TotalMilliseconds = {0}", sw.Elapsed.TotalMilliseconds);

            Console.WriteLine("Finished {0} archived, {1} skipped, {2} aborted.", archiveFileCount, totalFileCount - archiveFileCount, srceFilesQueue.Count);
            if (logws != null)
            {
                logws.Flush();
                logws.WriteLine("Finished {0} archived, {1} skipped, {2} aborted.", archiveFileCount, totalFileCount - archiveFileCount, srceFilesQueue.Count);

                foreach (var fileName in srceFilesQueue)
                {
                    logws.WriteLine("aborted, {0}", fileName);
                }
                logws.Close();
            }
        }
    }
}
