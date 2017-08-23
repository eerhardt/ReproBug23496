using System;
using System.IO;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Diagnostics;
using System.Collections;
using System.Threading;
using System.Security;

namespace ConsoleApp1
{
    class Program
    {
        static string Command = "echo Hello World";
        static string _batchFile;
        private static readonly Encoding s_utf8WithoutBom = new UTF8Encoding(false);
        static int _exitCode;
        private static Queue _standardErrorData;
        private static Queue _standardOutputData;
        private static ManualResetEvent _standardErrorDataAvailable;
        private static ManualResetEvent _standardOutputDataAvailable;
        private static ManualResetEvent _toolExited;
        private static ManualResetEvent _toolTimeoutExpired;
        private static bool _eventsDisposed;
        private static object _eventCloseLock = new object();
        private static Timer _toolTimer;
        static ManualResetEvent ToolCanceled = new ManualResetEvent(false);
        private static bool _terminatedTool;

        static void Main(string[] args)
        {
            CreateTemporaryBatchFile();

            CommandLineBuilder commandLine = new CommandLineBuilder();

            string batchFileForCommandLine = _batchFile;

            commandLine.AppendSwitch("-c");
            commandLine.AppendTextUnquoted(" \"\"\"");
            commandLine.AppendTextUnquoted("export LANG=en_US.UTF-8; export LC_ALL=en_US.UTF-8; . ");
            commandLine.AppendFileNameIfNotNull(batchFileForCommandLine);
            commandLine.AppendTextUnquoted("\"\"\"");

            string commandLineCommands = commandLine.ToString();

            // ensure the command line arguments string is not null
            if ((commandLineCommands == null) || (commandLineCommands.Length == 0))
            {
                commandLineCommands = String.Empty;
            }
            // add a leading space to the command line arguments (if any) to
            // separate them from the tool path
            else
            {
                commandLineCommands = " " + commandLineCommands;
            }

            string pathToTool = "/bin/sh";// ComputePathToTool();
            //if (pathToTool == null)
            //{
            //    // An appropriate error should have been logged already.
            //    return false;
            //}

            commandLineCommands = AdjustCommandsForOperatingSystem(commandLineCommands);
            _exitCode = 0;

            _exitCode = ExecuteTool(pathToTool, string.Empty, /*responseFileCommands,*/ commandLineCommands);

            Console.WriteLine(_exitCode);
        }

        private static void CreateTemporaryBatchFile()
        {
            var encoding = s_utf8WithoutBom;

            // Temporary file with the extension .Exec.bat
            _batchFile = FileUtilities.GetTemporaryFile(".exec.cmd");

            // UNICODE Batch files are not allowed as of WinXP. We can't use normal ANSI code pages either,
            // since console-related apps use OEM code pages "for historical reasons". Sigh.
            // We need to get the current OEM code page which will be the same language as the current ANSI code page,
            // just the OEM version.
            // See http://www.microsoft.com/globaldev/getWR/steps/wrg_codepage.mspx for a discussion of ANSI vs OEM
            // Note: 8/12/15 - Switched to use UTF8 on OS newer than 6.1 (Windows 7)
            // Note: 1/12/16 - Only use UTF8 when we detect we need to or the user specifies 'Always'
            using (StreamWriter sw = FileUtilities.OpenWrite(_batchFile, false, encoding))
            {
                //if (!NativeMethodsShared.IsUnixLike)
                //{
                //    // In some wierd setups, users may have set an env var actually called "errorlevel"
                //    // this would cause our "exit %errorlevel%" to return false.
                //    // This is because the actual errorlevel value is not an environment variable, but some commands,
                //    // such as "exit %errorlevel%" will use the environment variable with that name if it exists, instead
                //    // of the actual errorlevel value. So we must temporarily reset errorlevel locally first.
                //    sw.WriteLine("setlocal");
                //    // One more wrinkle.
                //    // "set foo=" has odd behavior: it sets errorlevel to 1 if there was no environment variable named
                //    // "foo" defined.
                //    // This has the effect of making "set errorlevel=" set an errorlevel of 1 if an environment
                //    // variable named "errorlevel" didn't already exist!
                //    // To avoid this problem, set errorlevel locally to a dummy value first.
                //    sw.WriteLine("set errorlevel=dummy");
                //    sw.WriteLine("set errorlevel=");

                //    // We may need to change the code page and console encoding.
                //    if (encoding.CodePage != EncodingUtilities.CurrentSystemOemEncoding.CodePage)
                //    {
                //        // Output to nul so we don't change output and logs.
                //        sw.WriteLine(string.Format(@"%SystemRoot%\System32\chcp.com {0}>nul", encoding.CodePage));

                //        // Ensure that the console encoding is correct.
                //        _standardOutputEncoding = encoding;
                //        _standardErrorEncoding = encoding;
                //    }

                //    // if the working directory is a UNC path, bracket the exec command with pushd and popd, because pushd
                //    // automatically maps the network path to a drive letter, and then popd disconnects it.
                //    // This is required because Cmd.exe does not support UNC names as the current directory:
                //    // https://support.microsoft.com/en-us/kb/156276
                //    if (workingDirectoryIsUNC)
                //    {
                //        sw.WriteLine("pushd " + _workingDirectory);
                //    }
                //}
                //else
                {
                    // Use sh rather than bash, as not all 'nix systems necessarily have Bash installed
                    sw.WriteLine("#!/bin/sh");
                }

                //if (NativeMethodsShared.IsUnixLike && NativeMethodsShared.IsMono)
                //{
                //    // Extract the command we are going to run. Note that the command name may
                //    // be preceded by whitespace
                //    var m = Regex.Match(Command, @"^\s*((?:(?:(?<!\\)[^\0 !$`&*()+])|(?:(?<=\\)[^\0]))+)(.*)");
                //    if (m.Success && m.Groups.Count > 1 && m.Groups[1].Captures.Count > 0)
                //    {
                //        string exe = m.Groups[1].Captures[0].ToString();
                //        string commandLine = (m.Groups.Count > 2 && m.Groups[2].Captures.Count > 0) ?
                //            m.Groups[2].Captures[0].Value : "";


                //        // If we are trying to run a .exe file, prepend mono as the file may
                //        // not be runnable
                //        if (exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                //            || exe.EndsWith(".exe\"", StringComparison.OrdinalIgnoreCase)
                //            || exe.EndsWith(".exe'", StringComparison.OrdinalIgnoreCase))
                //        {
                //            Command = "mono " + FileUtilities.FixFilePath(exe) + commandLine;
                //        }
                //    }
                //}

                sw.WriteLine(Command);

                //if (!NativeMethodsShared.IsUnixLike)
                //{
                //    if (workingDirectoryIsUNC)
                //    {
                //        sw.WriteLine("popd");
                //    }

                //    // NOTES:
                //    // 1) there's a bug in the Process class where the exit code is not returned properly i.e. if the command
                //    //    fails with exit code 9009, Process.ExitCode returns 1 -- the statement below forces it to return the
                //    //    correct exit code
                //    // 2) also because of another (or perhaps the same) bug in the Process class, when we use pushd/popd for a
                //    //    UNC path, even if the command fails, the exit code comes back as 0 (seemingly reflecting the success
                //    //    of popd) -- the statement below fixes that too
                //    // 3) the above described behaviour is most likely bugs in the Process class because batch files in a
                //    //    console window do not hide or change the exit code a.k.a. errorlevel, esp. since the popd command is
                //    //    a no-fail command, and it never changes the previous errorlevel
                //    sw.WriteLine("exit %errorlevel%");
                //}
            }
        }

        /// <summary>
        /// Replace backslashes with OS-specific path separators,
        /// except when likely that the backslash is intentional.
        /// </summary>
        /// <remarks>
        /// Not a static method so that an implementation can
        /// override with more-specific knowledge of what backslashes
        /// are likely to be correct.
        /// </remarks>
        static protected string AdjustCommandsForOperatingSystem(string input)
        {
            //if (NativeMethodsShared.IsWindows)
            //{
            //    return input;
            //}

            StringBuilder sb = new StringBuilder(input);

            int length = sb.Length;

            for (int i = 0; i < length; i++)
            {
                // Backslashes must be swapped, because we don't
                // know what inputs are paths or path fragments.
                // But it's a common pattern to have backslash-escaped
                // quotes inside quotes--especially for VB that has a default like
                //
                // /define:"CONFIG=\"Debug\",DEBUG=-1,TRACE=-1,_MyType=\"Console\",PLATFORM=\"AnyCPU\""
                //
                // So don't replace a backslash immediately
                // followed by a quote.
                if (sb[i] == '\\' && (i == length - 1 || sb[i + 1] != '"'))
                {
                    sb[i] = Path.DirectorySeparatorChar;
                }
            }

            return sb.ToString();
        }

        static protected int ExecuteTool
        (
            string pathToTool,
            string responseFileCommands,
            string commandLineCommands
        )
        {
            //if (!UseCommandProcessor)
            //{
            //    LogPathToTool(ToolExe, pathToTool);
            //}

            string responseFile = null;
            Process proc = null;

            _standardErrorData = new Queue();
            _standardOutputData = new Queue();

            _standardErrorDataAvailable = new ManualResetEvent(false);
            _standardOutputDataAvailable = new ManualResetEvent(false);

            _toolExited = new ManualResetEvent(false);
            _toolTimeoutExpired = new ManualResetEvent(false);

            _eventsDisposed = false;

            try
            {
                string responseFileSwitch = null;
                responseFile = null;// GetTemporaryResponseFile(responseFileCommands, out responseFileSwitch);

                // create/initialize the process to run the tool
                proc = new Process();
                proc.StartInfo = GetProcessStartInfo(pathToTool, commandLineCommands, responseFileSwitch);

                // turn on the Process.Exited event
                proc.EnableRaisingEvents = true;
                // sign up for the exit notification
                proc.Exited += new EventHandler(ReceiveExitNotification);

                // turn on async stderr notifications
                proc.ErrorDataReceived += new DataReceivedEventHandler(ReceiveStandardErrorData);
                // turn on async stdout notifications
                proc.OutputDataReceived += new DataReceivedEventHandler(ReceiveStandardOutputData);

                // if we've got this far, we expect to get an exit code from the process. If we don't
                // get one from the process, we want to use an exit code value of -1.
                _exitCode = -1;

                // Start the process
                proc.Start();

                // Close the input stream. This is done to prevent commands from
                // blocking the build waiting for input from the user.
                //if (NativeMethodsShared.IsWindows)
                //{
                //    proc.StandardInput.Dispose();
                //}

                // sign up for stderr callbacks
                proc.BeginErrorReadLine();
                // sign up for stdout callbacks
                proc.BeginOutputReadLine();

                // start the time-out timer
                _toolTimer = new Timer(new TimerCallback(ReceiveTimeoutNotification), null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite /* no periodic timeouts */);

                // deal with the various notifications
                HandleToolNotifications(proc);
            }
            finally
            {
                // Delete the temp file used for the response file.
                if (responseFile != null)
                {
                    DeleteTempFile(responseFile);
                }

                // get the exit code and release the process handle
                if (proc != null)
                {
                    try
                    {
                        _exitCode = proc.ExitCode;
                    }
                    catch (InvalidOperationException)
                    {
                        // The process was never launched successfully.
                        // Leave the exit code at -1.
                    }

                    proc.Dispose();
                    proc = null;
                }

                // If the tool exited cleanly, but logged errors then assign a failing exit code (-1)
                //if ((_exitCode == 0) && HasLoggedErrors)
                //{
                //    _exitCode = -1;
                //}

                // release all the OS resources
                // setting a bool to make sure tardy notification threads
                // don't try to set the event after this point
                lock (_eventCloseLock)
                {
                    _eventsDisposed = true;
                    _standardErrorDataAvailable.Dispose();
                    _standardOutputDataAvailable.Dispose();

                    _toolExited.Dispose();
                    _toolTimeoutExpired.Dispose();

                    if (_toolTimer != null)
                    {
                        _toolTimer.Dispose();
                    }
                }
            }

            return _exitCode;
        }

        static protected ProcessStartInfo GetProcessStartInfo
        (
            string pathToTool,
            string commandLineCommands,
            string responseFileSwitch
        )
        {
            // Build up the command line that will be spawned.
            string commandLine = commandLineCommands;

            //if (!UseCommandProcessor)
            //{
            //    if (!String.IsNullOrEmpty(responseFileSwitch))
            //    {
            //        commandLine += " " + responseFileSwitch;
            //    }
            //}

            // If the command is too long, it will most likely fail. The command line
            // arguments passed into any process cannot exceed 32768 characters, but
            // depending on the structure of the command (e.g. if it contains embedded
            // environment variables that will be expanded), longer commands might work,
            // or shorter commands might fail -- to play it safe, we warn at 32000.
            // NOTE: cmd.exe has a buffer limit of 8K, but we're not using cmd.exe here,
            // so we can go past 8K easily.
            if (commandLine.Length > 32000)
            {
                //LogPrivate.LogWarningWithCodeFromResources("ToolTask.CommandTooLong", this.GetType().Name);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo(pathToTool, commandLine);
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            // ensure the redirected streams have the encoding we want
            startInfo.StandardErrorEncoding = Encoding.UTF8; //StandardErrorEncoding;
            startInfo.StandardOutputEncoding = Encoding.UTF8; //StandardOutputEncoding;

            //if (NativeMethodsShared.IsWindows)
            //{
            //    // Some applications such as xcopy.exe fail without error if there's no stdin stream.
            //    // We only do it under Windows, we get Pipe Broken IO exception on other systems if
            //    // the program terminates very fast.
            //    startInfo.RedirectStandardInput = true;
            //}

            // Generally we won't set a working directory, and it will use the current directory
            string workingDirectory = null;// GetWorkingDirectory();
            if (null != workingDirectory)
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

//            // Old style environment overrides
//#pragma warning disable 0618 // obsolete
//            Dictionary<string, string> envOverrides = EnvironmentOverride;
//            if (null != envOverrides)
//            {
//                foreach (KeyValuePair<string, string> entry in envOverrides)
//                {
//#if FEATURE_PROCESSSTARTINFO_ENVIRONMENT
//                    startInfo.Environment[entry.Key] = entry.Value;
//#else
//                    startInfo.EnvironmentVariables[entry.Key] = entry.Value;
//#endif

//                }
//#pragma warning restore 0618
//            }

//            // New style environment overrides
//            if (_environmentVariablePairs != null)
//            {
//                foreach (KeyValuePair<object, object> variable in _environmentVariablePairs)
//                {
//#if FEATURE_PROCESSSTARTINFO_ENVIRONMENT
//                    startInfo.Environment[(string)variable.Key] = (string)variable.Value;
//#else
//                    startInfo.EnvironmentVariables[(string)variable.Key] = (string)variable.Value;
//#endif
//                }
//            }

            return startInfo;
        }

        private static void ReceiveExitNotification(object sender, EventArgs e)
        {
            //ErrorUtilities.VerifyThrow(_toolExited != null,
            //    "The signalling event for tool exit must be available.");

            lock (_eventCloseLock)
            {
                if (!_eventsDisposed)
                {
                    _toolExited.Set();
                }
            }
        }

        private static void ReceiveTimeoutNotification(object unused)
        {
            //ErrorUtilities.VerifyThrow(_toolTimeoutExpired != null,
            //    "The signalling event for tool time-out must be available.");
            lock (_eventCloseLock)
            {
                if (!_eventsDisposed)
                {
                    _toolTimeoutExpired.Set();
                }
            }
        }

        /// <summary>
        /// Handles all the notifications sent while the tool is executing. The
        /// notifications can be for tool output, tool time-out, or tool completion.
        /// </summary>
        /// <remarks>
        /// The slightly convoluted use of the async stderr/stdout streams of the
        /// Process class is necessary because we want to log all our messages from
        /// the main thread, instead of from a worker or callback thread.
        /// </remarks>
        /// <param name="proc"></param>
        private static void HandleToolNotifications(Process proc)
        {
            // NOTE: the ordering of this array is deliberate -- if multiple
            // notifications are sent simultaneously, we want to handle them
            // in the order specified by the array, so that we can observe the
            // following rules:
            // 1) if a tool times-out we want to abort it immediately regardless
            //    of whether its stderr/stdout queues are empty
            // 2) if a tool exits, we first want to flush its stderr/stdout queues
            // 3) if a tool exits and times-out at the same time, we want to let
            //    it exit gracefully
            WaitHandle[] notifications = new WaitHandle[]
                                            {
                                                _toolTimeoutExpired,
                                                ToolCanceled,
                                                _standardErrorDataAvailable,
                                                _standardOutputDataAvailable,
                                                _toolExited
                                            };

            bool isToolRunning = true;

            //if (YieldDuringToolExecution)
            //{
            //    BuildEngine3.Yield();
            //}

            try
            {
                while (isToolRunning)
                {
                    // wait for something to happen -- we block the main thread here
                    // because we don't want to uselessly consume CPU cycles; in theory
                    // we could poll the stdout and stderr queues, but polling is not
                    // good for performance, and so we use ManualResetEvents to wake up
                    // the main thread only when necessary
                    // NOTE: the return value from WaitAny() is the array index of the
                    // notification that was sent; if multiple notifications are sent
                    // simultaneously, the return value is the index of the notification
                    // with the smallest index value of all the sent notifications
                    int notificationIndex = WaitHandle.WaitAny(notifications);

                    switch (notificationIndex)
                    {
                        // tool timed-out
                        case 0:
                        // tool was canceled
                        case 1:
                            //TerminateToolProcess(proc, notificationIndex == 1);
                            _terminatedTool = true;
                            isToolRunning = false;
                            break;
                        // tool wrote to stderr (and maybe stdout also)
                        case 2:
                            LogMessagesFromStandardError();
                            // if stderr and stdout notifications were sent simultaneously, we
                            // must alternate between the queues, and not starve the stdout queue
                            LogMessagesFromStandardOutput();
                            break;

                        // tool wrote to stdout
                        case 3:
                            LogMessagesFromStandardOutput();
                            break;

                        // tool exited
                        case 4:
                            // We need to do this to guarantee the stderr/stdout streams
                            // are empty -- there seems to be no other way of telling when the
                            // process is done sending its async stderr/stdout notifications; why
                            // is the Process class sending the exit notification prematurely?
                            WaitForProcessExit(proc);

                            // flush the stderr and stdout queues to clear out the data placed
                            // in them while we were waiting for the process to exit
                            LogMessagesFromStandardError();
                            LogMessagesFromStandardOutput();
                            isToolRunning = false;
                            break;

                        default:
                            //ErrorUtilities.VerifyThrow(false, "Unknown tool notification.");
                            break;
                    }
                }
            }
            finally
            {
                //if (YieldDuringToolExecution)
                //{
                //    BuildEngine3.Reacquire();
                //}
            }
        }

        /// <summary>
        /// Confirms that the given process has really and truly exited. If the
        /// process is still finishing up, this method waits until it is done.
        /// </summary>
        /// <remarks>
        /// This method is a hack, but it needs to be called after both
        /// Process.WaitForExit() and Process.Kill().
        /// </remarks>
        /// <param name="proc"></param>
        private static void WaitForProcessExit(Process proc)
        {
            proc.WaitForExit();

            // Process.WaitForExit() may return prematurely. We need to check to be sure.
            while (!proc.HasExited)
            {
                System.Threading.Thread.Sleep(50);
            }
        }

        /// <summary>
        /// Logs all the messages that the tool wrote to stderr. The messages
        /// are read out of the stderr data queue.
        /// </summary>
        private static void LogMessagesFromStandardError()
        {
            LogMessagesFromStandardErrorOrOutput(_standardErrorData, _standardErrorDataAvailable, /*_standardErrorImportanceToUse,*/ StandardOutputOrErrorQueueType.StandardError);
        }

        /// <summary>
        /// Logs all the messages that the tool wrote to stdout. The messages
        /// are read out of the stdout data queue.
        /// </summary>
        private static void LogMessagesFromStandardOutput()
        {
            LogMessagesFromStandardErrorOrOutput(_standardOutputData, _standardOutputDataAvailable, /*_standardOutputImportanceToUse,*/ StandardOutputOrErrorQueueType.StandardOutput);
        }

        /// <summary>
        /// Enumeration which indicates what kind of queue is being passed
        /// </summary>
        private enum StandardOutputOrErrorQueueType
        {
            StandardError = 0,
            StandardOutput = 1
        }

        /// <summary>
        /// Logs all the messages that the tool wrote to either stderr or stdout.
        /// The messages are read out of the given data queue. This method is a
        /// helper for the <see cref="LogMessagesFromStandardError"/>() and <see
        /// cref="LogMessagesFromStandardOutput"/>() methods.
        /// </summary>
        /// <param name="dataQueue"></param>
        /// <param name="dataAvailableSignal"></param>
        /// <param name="messageImportance"></param>
        /// <param name="queueType"></param>
        private static void LogMessagesFromStandardErrorOrOutput
        (
            Queue dataQueue,
            ManualResetEvent dataAvailableSignal,
            //MessageImportance messageImportance,
            StandardOutputOrErrorQueueType queueType
        )
        {
            //ErrorUtilities.VerifyThrow(dataQueue != null,
            //    "The data queue must be available.");

            // synchronize access to the queue -- this is a producer-consumer problem
            // NOTE: the synchronization problem here is actually not about the queue
            // at all -- if we only cared about reading from and writing to the queue,
            // we could use a synchronized wrapper around the queue, and things would
            // work perfectly; the synchronization problem here is actually around the
            // ManualResetEvent -- while a ManualResetEvent itself is a thread-safe
            // type, the information we infer from the state of a ManualResetEvent is
            // not thread-safe; because a ManualResetEvent does not have a ref count,
            // we cannot safely set (or reset) it outside of a synchronization block;
            // therefore instead of using synchronized queue wrappers, we just lock the
            // entire queue, empty it, and reset the ManualResetEvent before releasing
            // the lock; this also allows proper alternation between the stderr and
            // stdout queues -- otherwise we would continuously read from one queue and
            // starve the other; locking out the producer allows the consumer to
            // alternate between the queues
            lock (dataQueue.SyncRoot)
            {
                while (dataQueue.Count > 0)
                {
                    string errorOrOutMessage = dataQueue.Dequeue() as String;
                    Console.WriteLine(errorOrOutMessage);
                    //if (!LogStandardErrorAsError || queueType == StandardOutputOrErrorQueueType.StandardOutput)
                    //{
                    //    this.LogEventsFromTextOutput(errorOrOutMessage, messageImportance);
                    //}
                    //else if (LogStandardErrorAsError && queueType == StandardOutputOrErrorQueueType.StandardError)
                    //{
                    //    Log.LogError(errorOrOutMessage);
                    //}
                }

                //ErrorUtilities.VerifyThrow(dataAvailableSignal != null,
                //    "The signalling event must be available.");

                // the queue is empty, so reset the notification
                // NOTE: intentionally, do the reset inside the lock, because
                // ManualResetEvents don't have ref counts, and we want to make
                // sure we don't reset the notification just after the producer
                // signals it
                dataAvailableSignal.Reset();
            }
        }

        /// <summary>
        /// Queues up the output from the stderr stream of the process executing
        /// the tool, and signals the availability of the data. The Process object
        /// executing the tool calls this method for every line of text that the
        /// tool writes to stderr.
        /// </summary>
        /// <remarks>This method is used as a System.Diagnostics.DataReceivedEventHandler delegate.</remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void ReceiveStandardErrorData(object sender, DataReceivedEventArgs e)
        {
            ReceiveStandardErrorOrOutputData(e, _standardErrorData, _standardErrorDataAvailable);
        }

        /// <summary>
        /// Queues up the output from the stdout stream of the process executing
        /// the tool, and signals the availability of the data. The Process object
        /// executing the tool calls this method for every line of text that the
        /// tool writes to stdout.
        /// </summary>
        /// <remarks>This method is used as a System.Diagnostics.DataReceivedEventHandler delegate.</remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void ReceiveStandardOutputData(object sender, DataReceivedEventArgs e)
        {
            ReceiveStandardErrorOrOutputData(e, _standardOutputData, _standardOutputDataAvailable);
        }

        /// <summary>
        /// Queues up the output from either the stderr or stdout stream of the
        /// process executing the tool, and signals the availability of the data.
        /// This method is a helper for the <see cref="ReceiveStandardErrorData"/>()
        /// and <see cref="ReceiveStandardOutputData"/>() methods.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="dataQueue"></param>
        /// <param name="dataAvailableSignal"></param>
        private static void ReceiveStandardErrorOrOutputData
        (
            DataReceivedEventArgs e,
            Queue dataQueue,
            ManualResetEvent dataAvailableSignal
        )
        {
            // NOTE: don't ignore empty string, because we need to log that
            if (e.Data != null)
            {
                //ErrorUtilities.VerifyThrow(dataQueue != null,
                //    "The data queue must be available.");

                // synchronize access to the queue -- this is a producer-consumer problem
                // NOTE: we lock the entire queue instead of using synchronized queue
                // wrappers, because ManualResetEvents don't have ref counts, and it's
                // difficult to discretely signal the availability of each instance of
                // data in the queue -- so instead we let the consumer lock and empty
                // the queue and reset the ManualResetEvent, before we add more data
                // into the queue, and signal the ManualResetEvent again
                lock (dataQueue.SyncRoot)
                {
                    dataQueue.Enqueue(e.Data);

                    //ErrorUtilities.VerifyThrow(dataAvailableSignal != null,
                    //    "The signalling event must be available.");

                    // signal the availability of data
                    // NOTE: intentionally, do the signalling inside the lock, because
                    // ManualResetEvents don't have ref counts, and we want to make sure
                    // we don't signal the notification just before the consumer resets it
                    lock (_eventCloseLock)
                    {
                        if (!_eventsDisposed)
                        {
                            dataAvailableSignal.Set();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Delete temporary file. If the delete fails for some reason (e.g. file locked by anti-virus) then
        /// the call will not throw an exception. Instead a warning will be logged, but the build will not fail.
        /// </summary>
        /// <param name="fileName">File to delete</param>
        protected static void DeleteTempFile(string fileName)
        {
            //if (s_preserveTempFiles)
            //{
            //    Log.LogMessageFromText($"Preserving temporary file '{fileName}'", MessageImportance.Low);
            //    return;
            //}

            try
            {
                File.Delete(fileName);
            }
            catch (Exception e) when (IsIoRelatedException(e))
            {
                // Warn only -- occasionally temp files fail to delete because of virus checkers; we 
                // don't want the build to fail in such cases
                //LogShared.LogWarningWithCodeFromResources("Shared.FailedDeletingTempFile", fileName, e.Message);
            }
        }

        internal static bool IsIoRelatedException(Exception e)
        {
            // These all derive from IOException
            //     DirectoryNotFoundException
            //     DriveNotFoundException
            //     EndOfStreamException
            //     FileLoadException
            //     FileNotFoundException
            //     PathTooLongException
            //     PipeException
            return e is UnauthorizedAccessException
                   || e is NotSupportedException
                   || (e is ArgumentException && !(e is ArgumentNullException))
                   || e is SecurityException
                   || e is IOException;
        }
    }
}

namespace Microsoft.Build.Shared
{ 
    static class FileUtilities
    {
        internal static StreamWriter OpenWrite(string path, bool append, Encoding encoding = null)
        {
            const int DefaultFileStreamBufferSize = 4096;
            FileMode mode = append ? FileMode.Append : FileMode.Create;
            Stream fileStream = new FileStream(path, mode, FileAccess.Write, FileShare.Read, DefaultFileStreamBufferSize, FileOptions.SequentialScan);
            if (encoding == null)
            {
                return new StreamWriter(fileStream);
            }
            else
            {
                return new StreamWriter(fileStream, encoding);
            }
        }

        /// <summary>
        /// Generates a unique temporary file name with a given extension in the temporary folder.
        /// File is guaranteed to be unique.
        /// Extension may have an initial period.
        /// Caller must delete it when finished.
        /// May throw IOException.
        /// </summary>
        internal static string GetTemporaryFile(string extension)
        {
            return GetTemporaryFile(null, extension);
        }

        /// <summary>
        /// Creates a file with unique temporary file name with a given extension in the specified folder.
        /// File is guaranteed to be unique.
        /// Extension may have an initial period.
        /// If folder is null, the temporary folder will be used.
        /// Caller must delete it when finished.
        /// May throw IOException.
        /// </summary>
        internal static string GetTemporaryFile(string directory, string extension, bool createFile = true)
        {
            //ErrorUtilities.VerifyThrowArgumentLengthIfNotNull(directory, "directory");
            //ErrorUtilities.VerifyThrowArgumentLength(extension, "extension");

            if (extension[0] != '.')
            {
                extension = '.' + extension;
            }

            //try
            {
                directory = directory ?? Path.GetTempPath();

                Directory.CreateDirectory(directory);

                string file = Path.Combine(directory, string.Format("tmp{0}{1}", Guid.NewGuid().ToString("N"), extension));

                //ErrorUtilities.VerifyThrow(!File.Exists(file), "Guid should be unique");

                if (createFile)
                {
                    File.WriteAllText(file, String.Empty);
                }

                return file;
            }
            //catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            //{
            //    throw new IOException(ResourceUtilities.FormatResourceString("Shared.FailedCreatingTempFile", ex.Message), ex);
            //}
        }

        internal static string FixFilePath(string path)
        {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', '/');//.Replace("//", "/");
        }
    }
}
