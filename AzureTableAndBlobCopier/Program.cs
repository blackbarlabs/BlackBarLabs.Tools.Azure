using System;

namespace AzureTableAndBlobCopier
{
    /// <summary>
    /// This program copies tables and blobs from a source to target Azure Storage account.
    /// 
    /// This program borrows heavily from the work of Alexandre Brisebois.  You can see his work at:
    /// https://alexandrebrisebois.wordpress.com
    /// https://github.com/brisebois
    /// https://alexandrebrisebois.wordpress.com/2013/06/20/windows-azure-table-storage-service-migrating-tables-between-storage-accounts/
    /// https://alexandrebrisebois.wordpress.com/2013/03/06/inserting-modifying-large-amounts-of-data-in-windows-azure-table-storage-service/
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            var storageAccountMigrator = new StorageAccountMigrator();
            storageAccountMigrator.Start().Wait();
            Console.WriteLine("Finished copy operation.  Press any key to close...");
            Console.ReadKey();
        }
    }
}
