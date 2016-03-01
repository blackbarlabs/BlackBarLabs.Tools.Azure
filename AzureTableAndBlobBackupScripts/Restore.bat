:: Restore.bat
:: Script used to restore tables and blobs backedup from an Azure Storage account to another account
:: Keith Holloway 
:: keith@eastfive.com
:: 3/1/2016
::
:: To Use:
:: Change YourBackupStorage to the storage account where your backup data is stored 
:: Change YourDestinationStorage to the storage account where you want backup data restored
:: Change YourKey to the Source and Destination location keys
:: Update the containerlist.txt file to contain a list of the containers to be backed up
:: Important - You will have to copy and repeat the line to upload tables for each table.  The 
:: reason for this inconenience is that the manifest files all have different times.
::
:: Remarks - This batch file will cause AZCopy.exe to pull the table and blob data that was 
:: backed up to local storage.  It will then push the data up to blob storage in the specified 
:: destination storage for data restore.
:: See: https://azure.microsoft.com/en-us/documentation/articles/storage-use-azcopy/#copy-entities-in-an-azure-table-with-azcopy
:: to get the latest version of AZCopy, which is required, and to read more about how AZCopy works.
cls
IF %1.==. GOTO Missing1

rmdir /s /q c:\temp\restore

:: Pull all of the table files from the table backup container to c:\temp\restore\tables
"C:\Program Files (x86)\Microsoft SDKs\Azure\AzCopy\AzCopy.exe" /Source:https://YourBackupStorage.blob.core.windows.net/%1tables /Dest:C:\temp\restore\tables /SourceKey:YourKey /s

:: Pull all of the blob files from their respective backup containers to c:\temp\restore\blob<containername>
for /F "tokens=*" %%A in (containerlist.txt) do "C:\Program Files (x86)\Microsoft SDKs\Azure\AzCopy\AzCopy.exe" /Source:https://YourBackupStorage.blob.core.windows.net/%1%%A /Dest:C:\temp\restore\blobs\%%A /SourceKey:YourKey /s

:: Upload tables from local storage to destination storage
"C:\Program Files (x86)\Microsoft SDKs\Azure\AzCopy\AzCopy.exe" /Source:C:\temp\restore\tables\ /Dest:https://YourDestinationStorage.table.core.windows.net/table1/ ::/DestKey:YourKey /Manifest:"myaccount_mytable_20140103T112020.manifest" /b /EntityOperation:InsertOrReplace 
:: YOU WILL HAVE TO REPEAT THE ABOVE LINE FOR EACH TABLE/MANIFEST FILE



:: Upload blobs from local storage to destination storage
for /F "tokens=*" %%A in (containerlist.txt) do "C:\Program Files (x86)\Microsoft SDKs\Azure\AzCopy\AzCopy.exe"  /Source:C:\temp\restore\blobs\%%A /Dest:https://YourDestinationStorage.blob.core.windows.net/%%A /DestKey:YourKey /s


GOTO End1



:Missing1
  ECHO Backup date parameter is required
GOTO End1

:End1