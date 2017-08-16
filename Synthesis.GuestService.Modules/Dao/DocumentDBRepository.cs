using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Synthesis.GuestService.Modules.Dao.Interfaces;

namespace Synthesis.GuestService.Modules.Dao
{
    public class DocumentDbRepository<T> : IBaseRepository<T> where T : class
    {
        private readonly string _databaseId = ConfigurationManager.AppSettings["DocumentDb.DatabaseId"];
        private readonly string _collectionId;
        private DocumentClient _client;

        public DocumentDbRepository()
        {
            Type typeParameterType = typeof(T);
            _collectionId = typeParameterType.FullName.Substring(typeParameterType.FullName.LastIndexOf(".", StringComparison.Ordinal) + 1); // Collection name based on Model Name
            InitializeAsync().GetAwaiter().GetResult();
        }

        public async Task<T> GetItemAsync(string id)
        {
            try
            {
                Document document = await _client.ReadDocumentAsync(UriFactory.CreateDocumentUri(_databaseId, _collectionId, id));
                return (T)(dynamic)document;
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }
        }

        public async Task<IEnumerable<T>> GetItemsAsync(Expression<Func<T, bool>> predicate)
        {
            IDocumentQuery<T> query = _client.CreateDocumentQuery<T>(
                                                                     UriFactory.CreateDocumentCollectionUri(_databaseId, _collectionId),
                                                                     new FeedOptions { MaxItemCount = -1 })
                                             .Where(predicate)
                                             .AsDocumentQuery();

            List<T> results = new List<T>();
            while (query.HasMoreResults)
            {
                results.AddRange(await query.ExecuteNextAsync<T>());
            }

            return results;
        }

        public async Task<Document> CreateItemAsync(T item)
        {
            return await _client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, _collectionId), item);
        }

        public async Task<Document> UpdateItemAsync(string id, T item)
        {
            return await _client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(_databaseId, _collectionId, id), item);
        }

        public async Task DeleteItemAsync(string id)
        {
            await _client.DeleteDocumentAsync(UriFactory.CreateDocumentUri(_databaseId, _collectionId, id));
        }

        public async Task InitializeAsync()
        {
            var endpoint = ConfigurationManager.AppSettings["DocumentDb.Endpoint"];
            var authKey = ConfigurationManager.AppSettings["DocumentDb.AuthKey"];

            _client = new DocumentClient(new Uri(endpoint), authKey, new ConnectionPolicy { EnableEndpointDiscovery = false });
            await CreateDatabaseIfNotExistsAsync();
            await CreateCollectionIfNotExistsAsync();
        }

        private async Task CreateDatabaseIfNotExistsAsync()
        {
            try
            {
                await _client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(_databaseId));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await _client.CreateDatabaseAsync(new Database { Id = _databaseId });
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task CreateCollectionIfNotExistsAsync()
        {
            try
            {
                await _client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, _collectionId));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await _client.CreateDocumentCollectionAsync(
                                                                UriFactory.CreateDatabaseUri(_databaseId),
                                                                new DocumentCollection { Id = _collectionId },
                                                                new RequestOptions { OfferThroughput = 1000 });
                }
                else
                {
                    throw;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposeManagedObjects)
        {
            if (disposeManagedObjects)
            {
                CleanUp();
            }
        }

        ~DocumentDbRepository()
        {
            Dispose(false);
        }

        private void CleanUp()
        {
        }
    }
}
