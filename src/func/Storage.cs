using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace WNBAScorigami
{
    class Storage
    {
        private string blobConnectionString;
        private BlobServiceClient serviceClient;
        private BlobContainerClient containerClient;

        public Storage(string secret, string container)
        {
            blobConnectionString = Environment.GetEnvironmentVariable(secret);
            serviceClient = new BlobServiceClient(blobConnectionString);
            containerClient = serviceClient.GetBlobContainerClient(container);
        }

        public async Task UploadJson(string json, string name)
        {
            var blob = containerClient.GetBlobClient(name);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            await blob.UploadAsync(stream, new BlobHttpHeaders
            {
                ContentType = "application/json"
            });
        }

        public async Task<string> DownloadJson(string name)
        {
            var blob = containerClient.GetBlobClient(name);
            BlobDownloadInfo jsonBytes = await blob.DownloadAsync();
            using var ms = new MemoryStream();
            await jsonBytes.Content.CopyToAsync(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}