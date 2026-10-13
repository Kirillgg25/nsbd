Dim shell
Dim scriptFolder
Dim batchFile

Set shell = CreateObject("WScript.Shell")
scriptFolder = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\"))
batchFile = scriptFolder & "start-nsbd-desktop.bat"

shell.Run "cmd.exe /c """ & batchFile & """", 0, False
