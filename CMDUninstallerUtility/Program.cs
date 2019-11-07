using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CMDUninstallerUtility
{
    public class Program
    {
        //TODO(DB): Pass around args less and just us a global.

        public static string CurrentUser { get; } = Environment.UserName.ToLower();
        public static string ProgramPath { get; } = getProgramPath();

        private static void Main(string[] args)
        {
            LogWriter.SetPath(ProgramPath, CurrentUser, "CMDUninstallerUtility");

            LaunchType launchType = readArgs(args);
            switch (launchType)
            {
                case LaunchType.None:
                    Console.WriteLine("");
                    Console.WriteLine("CMDUninstallerUtility Options Guide:");
                    Console.WriteLine("");
                    Console.WriteLine("Usage: CMDUninstallerUtility [-operation:<type>] [-terms:<search1>::<search2>::<search3>::<...>] [-output:] [-quiet]");
                    Console.WriteLine("Note: -terms: requires quotes for items with spaces or your search may not work correctly.");
                    Console.WriteLine("");
                    Console.WriteLine("-operation:<type> - Tells the program how to behave.");
                    Console.WriteLine("    Types: Search, List, Uninstall");
                    Console.WriteLine("    Note: Search and Uninstall requires use of the -terms: argument.");
                    Console.WriteLine("-terms: - Single argument that is double quote (::) deliminated with search terms for searching and uninstalling apps.");
                    Console.WriteLine("-output: - Single argument with a fully qualified path with file name to output results to. Output file is in CSV format.");
                    Console.WriteLine("-quiet - Used for uninstall only and will attempt to run a silent uninstall if possible.");
                    //TODO(DB): Will add this at some point.
                    //Console.WriteLine("-force - Dangerous. Will override the installer and manually remove services tied to files inside install directory, delete the files, and remove registry entries.");
                    break;

                case LaunchType.Invalid:
                    Console.WriteLine("");
                    Console.WriteLine("ERROR: Invalid command line arguments. Use -help to see all possible options.");
                    Console.WriteLine("");
                    break;

                case LaunchType.Search:
                    Console.WriteLine("");
                    Console.WriteLine("Application search selected:");
                    Console.WriteLine("");
                    search(args);
                    break;

                case LaunchType.List:
                    Console.WriteLine("");
                    Console.WriteLine("Application full list selected:");
                    Console.WriteLine("");
                    list(args);
                    break;

                case LaunchType.Uninstall:
                    Console.WriteLine("");
                    Console.WriteLine("Application uninstall selected:");
                    Console.WriteLine("");
                    uninstall(args);
                    break;
            }

            Console.WriteLine("Operation completed.");
            //Console.ReadLine();

            int count = 0;
            while(LogWriter.LogQueue.IsEmpty == false && count < 10)
            {
                Thread.Sleep(200);
                count++;
            }
        }

        private static string getProgramPath()
        {
            try
            {
                if (AppDomain.CurrentDomain.BaseDirectory[AppDomain.CurrentDomain.BaseDirectory.Length - 1] == '\\')
                {
                    return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                }
                else
                {
                    return Environment.CurrentDirectory;
                }
            }
            catch
            {
                // Doubt this will ever happen.
                return "";
            }
        }

        private enum LaunchType
        {
            None,
            Invalid,
            Search,
            List,
            Uninstall,
        }

        private static LaunchType readArgs(string[] args)
        {
            //TODO(DB): Add a check for multiple -operation:[option] flags. The way it works now is it priorities based on order of how it is coded below.

            if (args.Length < 1 || args.Contains("-help", StringComparer.OrdinalIgnoreCase))
            {
                return LaunchType.None;
            }
            else if (args.Contains("-operation:search", StringComparer.OrdinalIgnoreCase) || args.Contains("-operation:s", StringComparer.OrdinalIgnoreCase))
            {
                return LaunchType.Search;
            }
            else if (args.Contains("-operation:list", StringComparer.OrdinalIgnoreCase) || args.Contains("-operation:l", StringComparer.OrdinalIgnoreCase))
            {
                return LaunchType.List;
            }
            else if (args.Contains("-operation:uninstall", StringComparer.OrdinalIgnoreCase) || args.Contains("-operation:u", StringComparer.OrdinalIgnoreCase))
            {
                return LaunchType.Uninstall;
            }
            else
            {
                return LaunchType.Invalid;
            }
        }

        private static int getArgIndex(string searchArg, string[] args)
        {
            int index = -1;
            try
            {
                // Checks if there is an argument to send output list to file.
                index = Array.FindIndex(args, a => a.ToLower().StartsWith(searchArg));
            }
            catch (Exception Ex)
            {
                LogWriter.Exception("Error reading arguments for flag: " + searchArg, Ex);
            }

            return index;
        }

        private static void outputAppData(List<AppData> appList, string[] args)
        {
            int index = getArgIndex("-output:", args);
            if (index != -1)
            {
                //TODO(DB): Add code to check if path exists and if the user wants to overwrite the file.
                if (args[index].Length < 9)
                {
                    Console.WriteLine("Invalid Output file defined. Results will not be written to disk.");
                    return;
                }

                string file = args[index].Substring(8);
                writeAppList(file, appList);

                return;
            }

            // Output to file is not selected.
            foreach (AppData appData in appList)
            {
                Console.WriteLine("Display Name: " + appData.DisplayName);
                Console.WriteLine("Display Version: " + appData.DisplayVersion);
                Console.WriteLine("Publisher: " + appData.Publisher);
                Console.WriteLine("Install Location: " + appData.InstallLocation);
                Console.WriteLine("Uninstall String: " + appData.UninstallString);
                Console.WriteLine();
            }
        }

        // Writes App list to a designated file.
        private static void writeAppList(string file, List<AppData> appList)
        {
            List<string> csvFile = new List<string>() { "\"Display Name\",\"Display Version\",\"Publisher\",\"Install Location\",\"Uninstall String\"", };
            foreach (AppData appData in appList)
            {
                csvFile.Add("\"" + appData.DisplayName + "\"," + "\"" + appData.DisplayVersion + "\"," + "\"" + appData.Publisher + "\"," + "\"" + appData.InstallLocation + "\"," + "\"" + appData.UninstallString + "\"");
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllLines(file, csvFile.ToArray());
            }
            catch (Exception Ex)
            {
                LogWriter.Exception("Error outputing file list to csv file: " + file, Ex);
            }
        }

        private static void search(string[] args)
        {
            List<AppData> appMatches = getMatches(args);

            if (appMatches.Count > 0)
            {
                outputAppData(appMatches, args);
            }
        }

        private static List<AppData> getMatches(string[] args)
        {
            List<AppData> appMatches = new List<AppData>();

            int index = getArgIndex("-terms:", args);
            if (index == -1 || args[index].Length < 8)
            {
                Console.WriteLine("ERROR: Search terms either missing or blank. Use -help for more information.");
                Console.WriteLine("");
                return new List<AppData>();
            }

            string searchTermsArg = args[index].Substring(7);
            List<string> searchTerms = searchTermsArg.Split(new string[] { "::" }, StringSplitOptions.None).ToList();
            return AppHandler.FindMatches(searchTerms);
        }

        private static void list(string[] args)
        {
            List<AppData> appList = AppHandler.ReadApps();
            outputAppData(appList, args);
        }

        //TODO(DB): Display progress somehow or show the user this is working.
        private static void uninstall(string[] args)
        {
            List<AppData> appMatches = getMatches(args);

            if (appMatches.Count > 0)
            {
                bool quiet = args.Contains("-quiet", StringComparer.OrdinalIgnoreCase);

                if (quiet)
                {
                    Console.WriteLine("WARNING: Some apps do not support quite uninstall. Only MSI specific installers will operate silently.");
                }

                processApp(appMatches, quiet);
            }
        }

        private static async void processApp(List<AppData> appList, bool quiet)
        {
            await Task.Run(() =>
            {
                AppHandler.Uninstall(appList, quiet);
            });
        }
    }
}
