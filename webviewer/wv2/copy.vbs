Sub Publish(zSrc, zDst)
    'Dim fso As FileSystemObject
    'Dim tFile As File
    'Dim tFolder As folder
    '//crt folder
    '//copy file
    '//zip
    Set fso = CreateObject("Scripting.FileSystemObject")
    Set stdout = fso.GetStandardStream(1)
    Set stderr = fso.GetStandardStream(2)
    zSrc = fso.GetAbsolutePathName(zSrc)
    zDst = fso.GetAbsolutePathName(zDst)
    
    '//arr = Split("search\bin\x64\Release", "\")
    '//zPath = zPub
    '//For Each zTxt In arr
    '//    zPath = zPath & "\" & zTxt
    '//    If Not fso.FolderExists(zPath) Then
    '//        fso.CreateFolder zPath
    '//    End If
    '//Next
    
    '//mycopy zRls, zTgt
    Set copiedLst = CreateObject("Scripting.Dictionary")
    Set tLst = CreateObject("Scripting.Dictionary")
    tLst.Add 0, Array(zSrc, zDst)
    For i = 0 To 1000
        If tLst.Count = i Then Exit For
        
        rec = tLst(i)
        
        zSrc = rec(0)
        zDst = rec(1)
        For Each tFile In fso.GetFolder(zSrc).Files
            zTxt = zDst & "\" & tFile.Name
            ret = copyFile(tFile.Path, zTxt)
            If ret Then copiedLst.Add copiedLst.Count, tFile.Path
        Next
        For Each tFolder In fso.GetFolder(zSrc).SubFolders
            zTxt = zDst & "\" & tFolder.Name
            If Not fso.FolderExists(zTxt) Then
                fso.CreateFolder zTxt
            End If
            tLst.Add tLst.Count, Array(tFolder.Path, zTxt)
        Next
    Next
    If (tLst.Count > 1000) Then
        stderr.WriteLine "[err] too long"
    End If
    
    stdout.WriteLine "Copied: " & copiedLst.Count & " files"
    
    '//copy search_gecko.html, Search.ico, templ.html
    '//others = Array("search_gecko.html", "Search.ico", "templ.html")
    '//For Each zFile in others
    '//    zSrc = zProj & "\" & zFile
    '//    zDst = zPub & "\search\" & zFile
    '//    copyFile zSrc, zDst
    '//Next
    
    'zip zTgt
    '//ArchiveFolder zPub & "\search"
End Sub

Sub ArchiveFolder(oFile)

    Set oShell = CreateObject("WScript.Shell")
    oShell.run "%comspec% /c ""C:\Program Files\7-Zip\7z.exe"" a " & oFile & ".zip " & oFile & " -tzip", , True

End Sub

Function copyFile(zSrc, zDst)
    copyFile = False
    Set fso = CreateObject("Scripting.FileSystemObject")
    If fso.FileExists(zDst) Then
        If compareFile(zSrc, zDst) Then
            '//same
        Else
            fso.copyFile zSrc, zDst, True
            copyFile = True
        End If
    Else
        fso.copyFile zSrc, zDst
        
        copyFile = True
    End If
End Function

'if same return true
Function compareFile(zFile1, zFile2)
    'Dim tFile1 As File
    'Dim tFile2 As File
    'Dim fso As FileSystemObject
    
    'get file modify date time
    Set fso = CreateObject("Scripting.FileSystemObject")
    Set tFile1 = fso.GetFile(zFile1)
    Set tFile2 = fso.GetFile(zFile2)
    
    'check
    compareFile = tFile1.DateLastModified = tFile2.DateLastModified
    if (compareFile) then 
        compareFile = tFile1.Size = tFile2.Size
    end if
    
End Function

src = WScript.Arguments(0)
dst = WScript.Arguments(1)
Publish src, dst