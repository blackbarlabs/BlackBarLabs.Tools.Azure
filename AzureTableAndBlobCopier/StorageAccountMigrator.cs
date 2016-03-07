using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzureDataMigrator
{
    public class StorageAccountMigrator
    {
        private CloudStorageAccount sourceAccount;
        private CloudStorageAccount targetAccount;
        private bool migrateTables;
        private bool migrateBlobs;

        public Task<string> StartAsync(string sourceConnection, string targetConnection, bool doTableMigration, bool doBlobMigration)
        {
            sourceAccount = CloudStorageAccount.Parse(sourceConnection);
            targetAccount = CloudStorageAccount.Parse(targetConnection);
            migrateTables = doTableMigration;
            migrateBlobs = doBlobMigration;
            return ExecuteMigrationAsync();
        }

        private async Task<string> ExecuteMigrationAsync()
        {
            //await DeleteExistingTargetDataAsync();

            //MSDN says the table will take about 40 seconds to delete.  Trying to access it before that time will cause a 409 (conflict) to be returned.
            //so, wait about a minute before proceeding.  MSDN article: https://msdn.microsoft.com/library/azure/dd179387.aspx
            //await Task.Delay(TimeSpan.FromMinutes(1));  

            if (migrateBlobs) MigrateBlobContainers().Wait();
            if (migrateTables) MigrateTableStorage();
            return "done";
        }

        private async Task DeleteExistingTargetDataAsync()
        {
            Console.WriteLine("Deleting data from target storage...");
            var tableClient = targetAccount.CreateCloudTableClient();
            var tables = tableClient.ListTables();
            var tasks = tables.Select(x => x.DeleteIfExistsAsync());
            await Task.WhenAll(tasks);

            var blobClient = targetAccount.CreateCloudBlobClient();
            var containers = blobClient.ListContainers();
            tasks = containers.Select(x => x.DeleteIfExistsAsync());
            await Task.WhenAll(tasks);
        }

        private void MigrateTableStorage()
        {
            CopyTableStorageFromSource();
        }

        private void CopyTableStorageFromSource()
        {
            var source = sourceAccount.CreateCloudTableClient();

            var cloudTables = source.ListTables()
            .OrderBy(c => c.Name)
            .ToList();

            foreach (var table in cloudTables)
                CopyTables(table);
        }

        private void CopyTables(CloudTable table)
        {
            var target = targetAccount.CreateCloudTableClient();
            var targetTable = target.GetTableReference(table.Name);
            targetTable.CreateIfNotExists();
            targetTable.SetPermissions(table.GetPermissions());
            Console.WriteLine("Created Storage Table:" + table.Name);
            CopyData(table);
        }
        
        readonly Dictionary<string, long> retrieved
            = new Dictionary<string, long>();

        readonly TableQuery<DynamicTableEntity> query
            = new TableQuery<DynamicTableEntity>();

        private void CopyData(CloudTable table)
        {
            TableContinuationToken token = null;
            TableRequestOptions reqOptions = new TableRequestOptions();
            var ctx = new OperationContext { ClientRequestID = "StorageMigrator" };
            while (true)
            {
                ManualResetEvent evt = new ManualResetEvent(false);
                var result = table.BeginExecuteQuerySegmented(query, token, reqOptions, ctx, (o) =>
                {
                    var cloudTable = o.AsyncState as CloudTable;
                    var response = cloudTable.EndExecuteQuerySegmented<DynamicTableEntity>(o);
                    token = response.ContinuationToken;
                    var retrieved = response.Count();
                    if (retrieved > 0) WriteToTarget(cloudTable, response);
                    UpdateCount(cloudTable, retrieved);
                    Console.WriteLine("Table " +
                                    cloudTable.Name +
                                    " |> Records = " +
                                    retrieved +
                                    " | Total Records = " +
                                    this.retrieved[cloudTable.Name]);
                    evt.Set();
                }, table);
                evt.WaitOne();
                if (token == null) break;
            }
        }
        
        private void UpdateCount(CloudTable cloudTable, int recordsRetrieved)
        {
            if (!retrieved.ContainsKey(cloudTable.Name))
                retrieved.Add(cloudTable.Name, recordsRetrieved);
            else
                retrieved[cloudTable.Name] += recordsRetrieved;
        }

        private void WriteToTarget(CloudTable cloudTable,
                                            IEnumerable<DynamicTableEntity> response)
        {
            var writer = new TableStorageWriter(cloudTable.Name, targetAccount);
            foreach (var entity in response)
            {
                writer.InsertOrReplace(entity);
            }
            writer.Execute();
        }

        public Task<string> MigrateBlobContainers()
        {
            return Task.Run(() =>
            {
                CopyBlobContainersFromSource();
                return "done";
            });
        }

        private void CopyBlobContainersFromSource()
        {
            var source = sourceAccount.CreateCloudBlobClient();

            var cloudBlobContainers = source.ListContainers()
                .OrderBy(c => c.Name)
                .ToList();

            foreach (var cloudBlobContainer in cloudBlobContainers)
                CopyBlobContainer(cloudBlobContainer);
        }

        private void CopyBlobContainer(CloudBlobContainer sourceContainer)
        {
            var targetContainer = MakeContainer(sourceContainer);

            var targetBlobs = targetContainer.ListBlobs(null,
                                                        true,
                                                        BlobListingDetails.All)
                                                .Select(b => (ICloudBlob)b)
                                                .ToList();

            Trace.WriteLine(sourceContainer.Name + " Created");

            Trace.WriteLine(sourceContainer.Name + " List all blobs");

            var sourceBlobs = sourceContainer
                                .ListBlobs(null,
                                            true,
                                            BlobListingDetails.All)
                                .Select(b => (ICloudBlob)b)
                                .ToList();

            var missingBlobTask = Task.Run(() =>
            {
                AddMissingBlobs(sourceContainer,
                                sourceBlobs,
                                targetBlobs,
                                targetContainer);
            });

            var updateBlobs = Task.Run(() => UpdateBlobs(sourceContainer,
                                                            sourceBlobs,
                                                            targetBlobs,
                                                            targetContainer));

            Task.WaitAll(new[] { missingBlobTask, updateBlobs });

        }

        private void UpdateBlobs(CloudBlobContainer sourceContainer,
                                    IEnumerable<ICloudBlob> sourceBlobs,
                                    IEnumerable<ICloudBlob> targetBlobs,
                                    CloudBlobContainer targetContainer)
        {
            var updatedBlobs = sourceBlobs
                .AsParallel()
                .Select(sb =>
                {
                    var tb = targetBlobs.FirstOrDefault(b => b.Name == sb.Name);
                    if (tb == null)
                        return new
                        {
                            Source = sb,
                            Target = sb,
                        };

                    if (tb.Properties.LastModified < sb.Properties.LastModified)
                        return new
                        {
                            Source = sb,
                            Target = tb,
                        };

                    return new
                    {
                        Source = sb,
                        Target = sb,
                    };
                })
                .Where(b => b.Source != b.Target)
                .ToList();

            Console.WriteLine(targetContainer.Name + " |> " +
                                "Updating :" +
                                updatedBlobs.Count +
                                " blobs");

            Trace.WriteLine(sourceContainer.Name + " Start update all blobs");

            Parallel.ForEach(updatedBlobs, blob =>
            {
                TryCopyBlobToTargetContainer(blob.Source,
                                            targetContainer,
                                            sourceContainer);
            });

            Trace.WriteLine(sourceContainer.Name + " End update all blobs");
        }

        private void AddMissingBlobs(CloudBlobContainer sourceContainer,
                                        IEnumerable<ICloudBlob> sourceBlobs,
                                        IEnumerable<ICloudBlob> targetBlobs,
                                        CloudBlobContainer targetContainer)
        {
            var missingBlobs = sourceBlobs.AsParallel()
                                            .Where(b => NotExists(targetBlobs, b))
                                            .ToList();

            Console.WriteLine(targetContainer.Name +
                                " |> " +
                                "Adding missing :" +
                                missingBlobs.Count +
                                " blobs");

            Trace.WriteLine(sourceContainer.Name + " Start copy missing blobs");

            Parallel.ForEach(missingBlobs, blob =>
            {
                TryCopyBlobToTargetContainer(blob,
                                            targetContainer,
                                            sourceContainer);
            });

            Trace.WriteLine(sourceContainer.Name + " End copy missing blobs");
        }

        private static bool NotExists(IEnumerable<ICloudBlob> targetBlobs,
                                        ICloudBlob b)
        {
            return targetBlobs.All(tb => tb.Name != b.Name);
        }

        private CloudBlobContainer MakeContainer(CloudBlobContainer sourceContainer)
        {
            var target = targetAccount.CreateCloudBlobClient();
            var targetContainer = target.GetContainerReference(sourceContainer.Name);

            Trace.WriteLine(sourceContainer.Name + " Started");

            targetContainer.CreateIfNotExists();

            var blobContainerPermissions = sourceContainer.GetPermissions();

            if (blobContainerPermissions != null)
                targetContainer.SetPermissions(blobContainerPermissions);

            Trace.WriteLine(sourceContainer.Name + " Set Permissions");

            foreach (var meta in sourceContainer.Metadata)
                targetContainer.Metadata.Add(meta);

            targetContainer.SetMetadata();

            Trace.WriteLine(sourceContainer.Name + " Set Metadata");

            return targetContainer;
        }

        private void TryCopyBlobToTargetContainer(ICloudBlob item,
                                                    CloudBlobContainer targetContainer,
                                                    CloudBlobContainer sourceContainer)
        {
            try
            {
                var blob = (CloudBlockBlob)item;
                var blobRef = targetContainer.GetBlockBlobReference(blob.Name);

                var source = new Uri(GetShareAccessUri(blob.Name,
                                                        360,
                                                        sourceContainer));
                var result = blobRef.StartCopyFromBlob(source);
                Trace.WriteLine(blob.Properties.LastModified.ToString() +
                                " |>" +
                                blob.Name +
                                " :" +
                                result);
            }
            catch (StorageException ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private string GetShareAccessUri(string blobname,
                                        int validityPeriodInMinutes,
                                        CloudBlobContainer container)
        {
            var toDateTime = DateTime.Now.AddMinutes(validityPeriodInMinutes);

            var policy = new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessStartTime = null,
                SharedAccessExpiryTime = new DateTimeOffset(toDateTime)
            };

            var blob = container.GetBlockBlobReference(blobname);
            var sas = blob.GetSharedAccessSignature(policy);
            return blob.Uri.AbsoluteUri + sas;
        }
    }
}
