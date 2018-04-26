using System.Threading.Tasks;
using AzureDataMigrator;

namespace AzureTableAndBlobCopier
{
    /// <summary>
    /// This program copies tables and blobs from a source to target Azure Storage account.
    /// 
    /// This program borrows heavily from the work of Alexandre Brisebois and is really just an exe wrapper around his StorageAccountMigrator and 
    /// TableStorageWriter.  You can see his work at:
    /// https://alexandrebrisebois.wordpress.com
    /// https://github.com/brisebois
    /// https://alexandrebrisebois.wordpress.com/2013/06/20/windows-azure-table-storage-service-migrating-tables-between-storage-accounts/
    /// https://alexandrebrisebois.wordpress.com/2013/03/06/inserting-modifying-large-amounts-of-data-in-windows-azure-table-storage-service/
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            var sourceConnectionString = args[0];
            var targetConnectionString = args[1];
            var migrateTables = args[2].ToLower() == "true";
            var migrateBlobs = args[3].ToLower() == "true";

            var tableList = string.Empty;
            if (args.Length > 4)
                tableList = args[4];
        

            var storageAccountMigrator = new StorageAccountMigrator();
            Task.Run(async () =>
            {
                await storageAccountMigrator.StartAsync(sourceConnectionString, targetConnectionString, migrateTables, migrateBlobs, tableList);
            }).Wait();
        }
    }
}
