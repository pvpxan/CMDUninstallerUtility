using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMDUninstallerUtility
{
    // This type will be used if the data is going to be output to csv file.
    public class AppData
    {
        public string DisplayName { get; set; } = "";
        public string DisplayVersion { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string InstallLocation { get; set; } = "";
        public string UninstallString { get; set; } = "";
    }

    public static class AppHandler
    {
        public static void Uninstall(List<AppData> appList, bool quiet)
        {
            foreach (AppData appData in appList)
            {
                string[] uninstallerParts = appData.UninstallString.Split(new string[] { ".exe" }, StringSplitOptions.None);
                string uninstaller = uninstallerParts[0].TrimStart('"') + ".exe";
                string args = uninstallerParts[1].TrimStart('"').Trim();
                // TODO(DB): Not likely, but some arg might have a .exe in it some place and we just totally screwed up this uninstall.
                // Best way to fix this later is to find the index of the first occurance of .exe and then do 2 substrings.

                if (uninstaller.ToLower().Contains("msiexec.exe"))
                {
                    if (quiet)
                    {
                        args += @" /quiet";
                    }
                }

                Process process = new Process();
                bool error = false;
                try
                {
                    process.StartInfo.FileName = uninstaller;
                    process.StartInfo.Arguments = args;
                    process.Start();
                    process.WaitForExit();
                }
                catch (Exception Ex)
                {
                    LogWriter.Exception("Error running uninstaller process: " + appData.UninstallString, Ex);
                    error = true;
                }
                finally
                {
                    if (process != null)
                    {
                        process.Dispose();
                    }
                }

                if (error)
                {
                    continue;
                }

                List<string> remainingItems = getDirItems(appData.InstallLocation);
                if (remainingItems.Count < 1)
                {
                    continue;
                }

                string log = "Files and directories found after uninstall of: " + appData.DisplayName;
                foreach (string item in remainingItems)
                {
                    log += Environment.NewLine + item;
                }
                LogWriter.LogEntry(log);
            }
        }

        private static List<string> getDirItems(string path)
        {
            List<string> dirItems = new List<string>();

            if (string.IsNullOrEmpty(path))
            {
                LogWriter.LogEntry("Unable to search path that is null or empty.");
                return dirItems;
            }
     
            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(path);

                foreach (FileInfo fi in directoryInfo.GetFiles())
                {
                    dirItems.Add(fi.FullName);
                }

                foreach (DirectoryInfo di in directoryInfo.GetDirectories())
                {
                    dirItems.Add(di.FullName);

                    foreach (string result in getDirItems(di.FullName))
                    {
                        dirItems.Add(result);
                    }
                }
            }
            catch (Exception Ex)
            {
                LogWriter.Exception("Error scanning directory for items.", Ex);
            }

            return dirItems;
        }

        public static List<AppData> ReadApps()
        {
            string registryKeyString = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            string registryKeyString32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

            Dictionary<string, AppData> appsList = new Dictionary<string, AppData>();

            RegistryKey registryKey = null;
            try
            {
                registryKey = Registry.LocalMachine.OpenSubKey(registryKeyString);
                if (registryKey != null)
                {
                    appsList = readAppsSubKey(registryKey);
                }

                registryKey = Registry.CurrentUser.OpenSubKey(registryKeyString);
                if (registryKey != null)
                {
                    appsList = combineLists(appsList, readAppsSubKey(registryKey));
                }

                registryKey = Registry.LocalMachine.OpenSubKey(registryKeyString32);
                if (registryKey != null)
                {
                    appsList = combineLists(appsList, readAppsSubKey(registryKey));
                }

                registryKey = Registry.CurrentUser.OpenSubKey(registryKeyString32);
                if (registryKey != null)
                {
                    appsList = combineLists(appsList, readAppsSubKey(registryKey));
                }

                // TODO(DB): Add a check to make sure possible duplicates from HKEY_CURRENT_USER are not added to the returned list.
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.ToString());

                return appsList.Values.ToList();
            }
            finally
            {
                if (registryKey != null)
                {
                    registryKey.Dispose();
                }
            }

            return appsList.Values.ToList();
        }

        // TODO(DB): This should really be done using a GUID comparison. That requires some minor re-engineering of the class.
        private static Dictionary<string, AppData> combineLists(Dictionary<string, AppData> first, Dictionary<string, AppData> second)
        {
            foreach (var item in second)
            {
                first[item.Value.DisplayName.ToLower()] = item.Value;
            }

            return first;
        }

        private static List<AppData> getLocalSystemApps()
        {
            List<AppData> appsList = new List<AppData>();

            string localSystemApps = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products";
            RegistryKey registryKey = null;
            try
            {
                registryKey = Registry.LocalMachine.OpenSubKey(localSystemApps);
                if (registryKey != null)
                {
                    RegistryKey appSubKey = null;
                    foreach (string subKey in registryKey.GetSubKeyNames())
                    {
                        appSubKey = registryKey.OpenSubKey(subKey + @"\InstallProperties");

                        AppData appData = new AppData()
                        {
                            DisplayName = appSubKey.GetValue("DisplayName") as string,
                            DisplayVersion = appSubKey.GetValue("DisplayVersion") as string,
                            Publisher = appSubKey.GetValue("Publisher") as string,
                            InstallLocation = appSubKey.GetValue("InstallLocation") as string,
                            UninstallString = appSubKey.GetValue("UninstallString") as string,
                        };

                        if (string.IsNullOrEmpty(appData.DisplayName) == false && string.IsNullOrEmpty(appData.UninstallString) == false)
                        {
                            appsList.Add(appData);
                        }
                    }
                }
            }
            catch (Exception Ex)
            {
                LogWriter.Exception("Error parsing registry for installed applications. Key: " + localSystemApps, Ex);
                return null;
            }
            finally
            {
                if (registryKey != null)
                {
                    registryKey.Dispose();
                }
            }

            return appsList;
        }

        // This is designed to only look at SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall from either HKEY_LOCAL_MACHINE or HKEY_CURRENT_USER
        private static Dictionary<string, AppData> readAppsSubKey(RegistryKey registryKey)
        {
            Dictionary<string, AppData> appsList = new Dictionary<string, AppData>();

            string[] subKeyArr = null;
            try
            {
                subKeyArr = registryKey.GetSubKeyNames();
            }
            catch (Exception Ex)
            {
                LogWriter.Exception("Error getting subkey names. Key: " + registryKey.Name, Ex);
                return appsList;
            }

            if (subKeyArr == null)
            {
                return appsList;
            }

            RegistryKey appSubKey = null;
            foreach (string subkey in subKeyArr)
            {
                try
                {
                    appSubKey = registryKey.OpenSubKey(subkey);
                    AppData appData = new AppData()
                    {
                        DisplayName = appSubKey.GetValue("DisplayName") as string,
                        DisplayVersion = appSubKey.GetValue("DisplayVersion") as string,
                        Publisher = appSubKey.GetValue("Publisher") as string,
                        InstallLocation = appSubKey.GetValue("InstallLocation") as string,
                        UninstallString = appSubKey.GetValue("UninstallString") as string,
                    };

                    if (string.IsNullOrEmpty(appData.DisplayName) == false && string.IsNullOrEmpty(appData.UninstallString) == false)
                    {
                        appsList[appData.DisplayName.ToLower()] = appData;
                    }
                }
                catch (Exception Ex)
                {
                    LogWriter.Exception("Error reading subkey information. Key: " + subkey, Ex);
                }
            }

            if (appSubKey != null)
            {
                appSubKey.Dispose();
            }

            return appsList;
        }

        public static List<AppData> FindMatches(List<string> searchTerms)
        {
            List<AppData> appList = ReadApps();
            List<AppData> appMatches = new List<AppData>();

            foreach (string term in searchTerms)
            {
                // TODO(DB): This feels sloppy and can be optimized. Could benefit with allowing the use of the '?' wildcard.
                if (term.StartsWith("*") && term.EndsWith("*"))
                {
                    appMatches.AddRange(appList.Where(a => a.DisplayName.ToLower().Contains(term.ToLower().Trim('*'))));
                }
                else if (term.StartsWith("*") && term.EndsWith("*") == false)
                {
                    appMatches.AddRange(appList.Where(a => a.DisplayName.ToLower().EndsWith(term.ToLower().Trim('*'))));
                }
                else if (term.StartsWith("*") == false && term.EndsWith("*"))
                {
                    appMatches.AddRange(appList.Where(a => a.DisplayName.ToLower().StartsWith(term.ToLower().Trim('*'))));
                }
                else
                {
                    appMatches.AddRange(appList.Where(a => a.DisplayName.ToLower().Equals(term.ToLower())));
                }
            }

            return appMatches;
        }
    }
}
