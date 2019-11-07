using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CMDUninstallerUtility
{
    // START LogWriter_Class ------------------------------------------------------------------------------------------------------------
    public static class LogWriter
    {
        // This class is a simple thread safe log writer/displayer.
        private static ReaderWriterLockSlim logLocker = new ReaderWriterLockSlim();

        // Since this class spins up threads, there is a possiblity that a console app can close before a thread finishes.
        public static ConcurrentQueue<string> LogQueue { get; private set; } = new ConcurrentQueue<string>();

        private static string logApplication = "";
        private static string logPath = "";
        private static string logUser = "";
        private static string logFile = "";

        public static bool StorePostData { get; set; } = false;
        public static int MaxLogCount { get; set; } = 100;

        // Can be used for internal storage of logs that you may want to post later.
        private static List<string> _LogData = new List<string>();
        public static List<string> LogData
        {
            get
            {
                return _LogData;
            }
        }

        public static bool SetPath(string path, string user, string application)
        {
            if (System.IO.Directory.Exists(path) == false)
            {
                return false;
            }

            logApplication = application;
            logPath = path;
            logUser = user;

            return true;
        }

        // Creates a new log file if appropriate
        // -----------------------------------------------------------------------
        private static bool generateLogFile()
        {
            // Get the path of the application. Failure indicates that the user specific temp folder can be used IF access is available.
            if (logPath.Length <= 0)
            {
                try
                {
                    logPath = System.IO.Path.GetTempPath();
                }
                catch (Exception Ex)
                {
                    LogWriter.PostException("Error getting system temporary directory path.", Ex);
                    return false;
                }
            }

            // Log file name is defined.
            logFile = string.Format(@"{0}\log\{1}_{2}.log", logPath, logApplication, DateTime.Now.ToString("yyyy-MM-dd"));

            try
            {
                System.IO.Directory.CreateDirectory(logPath + @"\log"); // Generates the folder for the log files if non exists.
            }
            catch (Exception Ex)
            {
                LogWriter.PostException("Error creating log file directory.", Ex);
                return false;
            }

            // Log file is created if none exists.
            if (System.IO.File.Exists(logFile) == false)
            {
                try
                {
                    System.IO.File.Create(logFile).Dispose(); // Generates a log file if non exists.
                    return true;
                }
                catch (Exception Ex)
                {
                    LogWriter.PostException("Error blank log file.", Ex);
                    return false;
                }
            }

            return true;
        }
        // -----------------------------------------------------------------------

        // Logs an exception message in a specific format for a log file.
        // -----------------------------------------------------------------------
        public static void Exception(string log, Exception ex)
        {
            string message = log + Environment.NewLine + Convert.ToString(ex);
            LogEntry(message);
        }

        // Logs string to log file.
        // -----------------------------------------------------------------------
        public static void LogEntry(string log)
        {
            // TODO: These might be used for timeouts later. Needed at this time for proper task creation.
            var source = new CancellationTokenSource();
            var token = source.Token;

            LogQueue.Enqueue(log);

            // Creates a thread that will write to a log file.
            Task.Factory.StartNew(() =>
            {
                logEntryThreadCall(log, true);
            },
            token, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
        }

        // Log Exception storage without writing to disk.
        // -----------------------------------------------------------------------
        public static void PostException(string log, Exception ex)
        {
            string message = log + Environment.NewLine + Convert.ToString(ex);
            Post(message);
        }

        // Log storage without writing to disk.
        // -----------------------------------------------------------------------
        public static void Post(string log)
        {
            if (StorePostData == false)
            {
                return;
            }

            // TODO: These might be used for timeouts later. Needed at this time for proper task creation.
            var source = new CancellationTokenSource();
            var token = source.Token;

            LogQueue.Enqueue(log);

            // Creates a thread that will write to a log file.
            Task.Factory.StartNew(() =>
            {
                logEntryThreadCall(log, false);
            },
            token, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
        }

        private static void logEntryThreadCall(string log, bool write)
        {
            logLocker.EnterWriteLock();

            string formatedLog = "Time Stamp Error - " + logUser + " - " + log + Environment.NewLine;
            try
            {
                formatedLog = DateTime.Now.ToString("yyyy-MM-dd - HH:mm:ss") + " - " + logUser + " - " + log + Environment.NewLine;
            }
            catch (Exception Ex)
            {
                LogWriter.PostException("Error formatting log entry.", Ex);
            }

            if (write)
            {
                saveLogToFile(formatedLog);
            }
            else
            {
                if (_LogData.Count >= MaxLogCount)
                {
                    _LogData.RemoveAt(0);
                }

                _LogData.Add(formatedLog);
            }

            LogQueue.TryDequeue(out string loggedMessage);
            logLocker.ExitWriteLock();
        }

        // TODO (DB): Add LogData to a file.
        public static void WriteLogData()
        {

        }

        private static void saveLogToFile(string logData)
        {
            if (generateLogFile())
            {
                try
                {
                    System.IO.File.AppendAllText(logFile, logData);
                }
                catch (Exception Ex)
                {
                    LogWriter.PostException("Error saving log entry to file on disk.", Ex);
                }
            }
        }
    }
    // END LogWriter_Class --------------------------------------------------------------------------------------------------------------
}
