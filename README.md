# CMDUninstallerUtility
Command line tool to view or uninstall apps from a Windows system.

# CMDUninstallerUtility Options Guide:

## Usage: 
```bash
CMDUninstallerUtility [-operation:<type>] [-terms:<search1>::<search2>::<search3>::<...>] [-output:<file>] [-quiet]
```
  - Note: -terms: requires quotes for items with spaces or your search may not work correctly.
```bash
-operation:<type>
```
  - Tells the program how to behave.
  - Types: Search, List, Uninstall
  - Note: Search and Uninstall requires use of the -terms: argument.
```bash
-terms:<...>
```
  - Single argument that is double quote (::) deliminated with search terms for searching and uninstalling apps.
```bash
-output:<file>
```
  - Single argument with a fully qualified path with file name to output results to. Output file is in CSV format.
```bash
-quiet
```
  - Used for uninstall only and will attempt to run a silent uninstall if possible.
