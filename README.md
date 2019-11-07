# CMDUninstallerUtility
Command line tool to view or uninstall apps from a Windows system.

CMDUninstallerUtility Options Guide:

Usage: CMDUninstallerUtility [-operation:<type>] [-terms:<search1>::<search2>::<search3>::<...>] [-output:] [-quiet]
Note: -terms: requires quotes for items with spaces or your search may not work correctly.

-operation:<type> - Tells the program how to behave.
    Types: Search, List, Uninstall
    Note: Search and Uninstall requires use of the -terms: argument.
-terms: - Single argument that is double quote (::) deliminated with search terms for searching and uninstalling apps.
-output: - Single argument with a fully qualified path with file name to output results to. Output file is in CSV format.
-quiet - Used for uninstall only and will attempt to run a silent uninstall if possible.
Operation completed.