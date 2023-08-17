using core.Interfaces;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace core.Business
{
    public class FileAccess : IFileAccess
    {
        private readonly CloudBlobClient _blobClient;
        private readonly int maxThreads = 6;
        private readonly int partSizeInBytes = 5242880;
        private readonly string _blobConnStr = ConfigurationManager.ConnectionStrings["BlobConnStr"]?.ToString();

        public FileAccess ()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var storageAccount = CloudStorageAccount.Parse(_blobConnStr);
            _blobClient = storageAccount.CreateCloudBlobClient();
        }

        public FileAccess(string blobConnStr)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var storageAccount = CloudStorageAccount.Parse(blobConnStr);
            _blobClient = storageAccount.CreateCloudBlobClient();
        }

        public bool CheckExistsContainer(string bucketName)
        {
            try
            {
                return _blobClient.GetContainerReference(bucketName).Exists();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public string CreateContainer(string bucketName)
        {
            string response = string.Empty;
            try
            {
                var newBlobContainer = _blobClient.GetContainerReference(bucketName);
                newBlobContainer.CreateIfNotExists();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return response;
        }

        public string DeleteContainer(string bucketName)
        {
            string response = string.Empty;

            try
            {
                var containerToDelete = _blobClient.GetContainerReference(bucketName);
                containerToDelete.DeleteIfExists();
            }

            catch (Exception ex)
            {
                throw ex;
            }

            return response;
        }

        public bool DeleteFileTemp(string bucketName, DateTime currentDate, ref string error)
        {
            try
            {
                var container = _blobClient.GetContainerReference(bucketName);
                BlobContinuationToken continuationToken = null;

                do
                {
                    var blobs = container.ListBlobsSegmented(null, true, BlobListingDetails.Metadata, null, continuationToken, null, null);

                    foreach (var blobItem in blobs.Results.OfType<CloudBlob>().Where(b => b.Properties.LastModified != null &&
                    b.Properties.LastModified.Value.Date < currentDate.Date))
                    {
                        blobItem.Delete();
                    }

                    continuationToken = blobs.ContinuationToken;
                } while (continuationToken != null);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool DeletingAnObject(string bucketName, string fileName)
        {
            try
            {
                var container = _blobClient.GetContainerReference(bucketName);
                CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
                return blob.DeleteIfExists();
            }
            catch (Exception ex)
            {
                throw ex;
            };
        }

        public string GetSASUrl(string bucketName, string fileName, int expiredInDays)
        {
            try
            {
                var container = _blobClient.GetContainerReference(bucketName);
                var blob = container.GetBlockBlobReference(fileName);

                SharedAccessBlobPolicy policy = new SharedAccessBlobPolicy
                {
                    SharedAccessExpiryTime = DateTime.UtcNow.AddDays(expiredInDays),
                    Permissions = SharedAccessBlobPermissions.Read
                };

                string sasToken = blob.GetSharedAccessSignature(policy);
                string urlWithSas = blob.Uri + sasToken;

                return urlWithSas;
            }

            catch (Exception ex)
            {
                throw ex;
            }
        }

        public byte[] ReadingAnObject(string bucketName, string fileName)
        {
            try
            {
                var container = _blobClient.GetContainerReference(bucketName);
                var blob = container.GetBlockBlobReference(fileName);

                using (MemoryStream stream = new MemoryStream())
                {
                    blob.DownloadToStream(stream);
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public byte[] ReadingAnObjectMultiPart(string bucketName, string fileName, string filePath)
        {
            try
            {
                var container = _blobClient.GetContainerReference(bucketName);
                var blob = container.GetBlockBlobReference(fileName);

                blob.FetchAttributes();

                long fileSize = blob.Properties.Length;
                List<byte[]> parts = new List<byte[]>();

                int numThreads = (int)Math.Ceiling((double)fileSize / partSizeInBytes);
                var tasks = new List<Task>();

                using (var memoryStream = new MemoryStream())
                {
                    // Create an array of AutoResetEvent to limit the number of threads
                    AutoResetEvent[] waitHandles = new AutoResetEvent[maxThreads];
                    for (int i = 0; i < maxThreads; i++)
                    {
                        waitHandles[i] = new AutoResetEvent(false);
                    }

                    for (int i = 0; i < numThreads; i++)
                    {
                        int partIndex = i;
                        // Queue a task to the ThreadPool to download data parts from the blob
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            using (var partStream = new MemoryStream())
                            {
                                long startOffset = partIndex * partSizeInBytes;
                                long endOffset = Math.Min(startOffset + partSizeInBytes - 1, fileSize - 1);

                                // Download a portion of the blob into the stream
                                blob.DownloadRangeToStream(partStream, startOffset, endOffset);
                                parts.Add(partStream.ToArray());
                            }

                            // Set the AutoResetEvent to signal task completion
                            waitHandles[partIndex % maxThreads].Set();
                        });
                    }

                    // Wait for all AutoResetEvents to signal completion
                    WaitHandle.WaitAll(waitHandles);

                    foreach (var part in parts)
                    {
                        // Write the parts into memoryStream to create a complete byte array
                        memoryStream.Write(part, 0, part.Length);
                    }

                    // Return the downloaded byte array
                    return memoryStream.ToArray();
                }
            }
            catch (Exception)
            {
                return ReadingAnObject(bucketName, fileName);
            }
        }

        public bool RenameFile(string bucketName, string oldKeyFile, string newKeyFile)
        {
            try
            {
                var container = _blobClient.GetContainerReference(bucketName);
                var oldBlob = container.GetBlockBlobReference(oldKeyFile);
                var newBlob = container.GetBlockBlobReference(newKeyFile);

                if (oldBlob.Exists())
                {
                    newBlob.StartCopy(oldBlob);
                    oldBlob.DeleteIfExists();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public bool UploadFile(string bucketName, string fileName, string filePath)
        {
            try
            {
                var container = _blobClient.GetContainerReference(bucketName);
                var blob = container.GetBlockBlobReference(fileName);

                using (FileStream fileStream = File.OpenRead(filePath))
                {
                    blob.UploadFromStream(fileStream);
                }
                return true;

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public bool WritingAnObject(string bucketName, string fileName, byte[] fileBytes, int size)
        {
            try
            {
                var container = _blobClient.GetContainerReference(bucketName);
                var blob = container.GetBlockBlobReference(fileName);

                using (var stream = new MemoryStream(fileBytes))
                {
                    blob.UploadFromStream(stream);
                }

                // Check if the uploaded blob size matches the expected size
                blob.FetchAttributes();
                long uploadedSize = blob.Properties.Length;
                if (uploadedSize == size)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
