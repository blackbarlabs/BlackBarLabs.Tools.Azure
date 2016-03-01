:: Backup.bat
:: Script used to backup tables and blobs from an Azure Storage account
:: Keith Holloway 
:: keith@eastfive.com
:: 3/1/2016
::
:: To Use:
:: Change YourStorageToBeBackedup to your storage account
:: Change YourBackupStorage to the storage account where you want backup data stored 
:: Change YourKey to the Source and Destination location keys
:: Update the tablelist.txt file to contain a list of the tables to be backed up
:: Update the containerlist.txt file to contain a list of the containers to be backed up
::
:: Remarks - This batch file will cause AZCopy.exe to pull the table and blob data to local
:: storage.  It will then push the data up to blob storage in the specified backup storage 
:: location.  You can use the restore.bat file to restore data.
:: See: https://azure.microsoft.com/en-us/documentation/articles/storage-use-azcopy/#copy-entities-in-an-azure-table-with-azcopy
:: to get the latest version of AZCopy, which is required and to read more about how AZCopy works.

cls

echo off 
set hour=%time:~0,2%
if "%hour:~0,1%" == " " set hour=0%hour:~1,1%
set min=%time:~3,2%
if "%min:~0,1%" == " " set min=0%min:~1,1%

echo on
set backuptime=%date:~10,4%%date:~4,2%%date:~7,2%%hour%%min%


::Backup all tables listed in the tablelist.txt file
for /F "tokens=*" %%A in (tablelist.txt) do "C:\Program Files (x86)\Microsoft SDKs\Azure\AzCopy\AzCopy.exe" /Source:https://YourStorageToBeBackedup.table.core.windows.net/%%A/ /Dest:https://YourBackupStorage.blob.core.windows.net/%backuptime%tables/ /SourceKey:YourKey /Destkey:YourKey

:: Backup all blobs listed in the containerlist.txt file
for /F "tokens=*" %%A in (containerlist.txt) do "C:\Program Files (x86)\Microsoft SDKs\Azure\AzCopy\AzCopy.exe" /Source:https://YourStorageToBeBackedup.blob.core.windows.net/%%A /Dest:https://YourBackupStorage.blob.core.windows.net/%backuptime%%%A/ /SourceKey:YourKey /Destkey:YourKey /s