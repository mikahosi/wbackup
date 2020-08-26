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

            Parallel.For(0, 4, i => 
            {
                var nameTrans = new ZipNameTransform();

                string zipFileName = param.destinationDir + "-" + postFixString + "-" + i.ToString("00") + ".zip";
                var fos = new FileStream(zipFileName, FileMode.Create, FileAccess.Write);
                ZipOutputStream zos = new ZipOutputStream(fos);
                zos.SetLevel(4);
                zos.UseZip64 = UseZip64.On;

                int buffSize = 65536;
                byte[][] readBuffer = new byte[2][];
                readBuffer[0] = new byte[buffSize];
                readBuffer[1] = new byte[buffSize];

                while (srceFilesQueue.TryDequeue(out string srceFileName))
                {
                    try
                    {
                        var attribute = File.GetAttributes(srceFileName);
                        if (attribute.HasFlag(FileAttributes.Archive) || param.backupMode== BackupMopde.Full)
                        {
                            if (consoleLock.WaitOne())
                            {
                                Console.WriteLine("Archive, {0}", srceFileName);
                                if (logws != null)
                                    logws.WriteLine("Archive, {0}", srceFileName);
                                consoleLock.ReleaseMutex();
                            }

                            FileInfo fi = new FileInfo(srceFileName);
                            string f = nameTrans.TransformFile(srceFileName);
                            ZipEntry zipEntry = new ZipEntry(f);
                            zipEntry.DateTime = fi.LastWriteTime;
                            zipEntry.ExternalFileAttributes = (int )fi.Attributes;
                            zipEntry.Size = fi.Length;

                            // 一部の拡張子について、圧縮対象から除外する
                            int extPos = srceFileName.LastIndexOf('.');
                            if (extPos > 0)
                            {
                                string extension = srceFileName.Substring(extPos).ToLower();
                                foreach (var extMatch in storedTarget)
                                {
                                    if (extension == extMatch)
                                    {
                                        zipEntry.CompressionMethod = CompressionMethod.Stored;
                                        break;
                                    }
                                }
                            }

                            var fis = new FileStream(srceFileName, FileMode.Open, FileAccess.Read);
                            zos.PutNextEntry(zipEntry);

                            int buffSelect = 0;
                            Task<int> len = fis.ReadAsync(readBuffer[buffSelect], 0, buffSize);
                            while (true)
                            {
                                len.Wait();
                                if (len.Result <= 0)
                                    break;
                                Task writeResult = zos.WriteAsync(readBuffer[buffSelect], 0, len.Result);

                                buffSelect ^= 1;
                                len = fis.ReadAsync(readBuffer[buffSelect], 0, buffSize);

                                writeResult.Wait();
                            }
                            fis.Close();
                            zos.Flush();

                            if (param.backupMode == BackupMopde.Full || param.backupMode == BackupMopde.Differencial)
                            {
                                File.SetAttributes(srceFileName, (attribute & (~FileAttributes.Archive)));
                            }

                            Interlocked.Increment(ref archiveFileCount);
                        }
                        else
                        {
                            if (consoleLock.WaitOne())
                            {
                                Console.WriteLine("Skip, {0}", srceFileName);
                                if (logws != null)
                                    logws.WriteLine("Skip, {0}", srceFileName);
                                consoleLock.ReleaseMutex();
                            }
                        }
                    }
                    catch(Exception exp)
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

                        if (curtExceptionCnt > param.maxRetryCount)
                            break;
                    }
                }

                zos.Finish();
                zos.Close();
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
