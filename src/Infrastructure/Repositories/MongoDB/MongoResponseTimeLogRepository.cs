#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DDDExample.Domain.Entities;
using DDDExample.Domain.Repositories;
using DDDExample.Infrastructure.Persistence.MongoDB;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace DDDExample.Infrastructure.Repositories.MongoDB
{
    public class MongoResponseTimeLogRepository : IResponseTimeLogRepository
    {
        private readonly IMongoCollection<ResponseTimeLog> _logsCollection;

        public MongoResponseTimeLogRepository(IOptions<MongoDbSettings> settings)
        {
            if (settings?.Value == null)
            {
                throw new ArgumentNullException(nameof(settings), "MongoDB settings are not configured.");
            }

            if (string.IsNullOrEmpty(settings.Value.ConnectionString))
            {
                throw new ArgumentException("MongoDB connection string is not configured.", nameof(settings));
            }

            if (string.IsNullOrEmpty(settings.Value.DatabaseName))
            {
                throw new ArgumentException("MongoDB database name is not configured.", nameof(settings));
            }

            var client = new MongoClient(settings.Value.ConnectionString);
            var database = client.GetDatabase(settings.Value.DatabaseName);
            _logsCollection = database.GetCollection<ResponseTimeLog>("responseTimeLogs");
            
            var indexKeysDefinition = Builders<ResponseTimeLog>.IndexKeys.Descending(x => x.Timestamp);
            _logsCollection.Indexes.CreateOne(new CreateIndexModel<ResponseTimeLog>(indexKeysDefinition));
        }

        public async Task AddAsync(ResponseTimeLog log)
        {
            await _logsCollection.InsertOneAsync(log);
        }

        public async Task<IEnumerable<ResponseTimeLog>> GetLogsAsync(
            string? path = null, 
            string? method = null, 
            int? minDurationMs = null, 
            DateTime? startDate = null, 
            DateTime? endDate = null,
            int limit = 100)
        {
            var query = _logsCollection.AsQueryable();

            if (!string.IsNullOrEmpty(path))
                query = query.Where(x => x.Path != null && x.Path.Contains(path));
                
            if (!string.IsNullOrEmpty(method))
                query = query.Where(x => x.Method != null && x.Method.Equals(method, StringComparison.OrdinalIgnoreCase));
                
            if (minDurationMs.HasValue)
                query = query.Where(x => x.DurationMs >= minDurationMs.Value);
                
            if (startDate.HasValue)
                query = query.Where(x => x.Timestamp >= startDate.Value);
                
            if (endDate.HasValue)
                query = query.Where(x => x.Timestamp <= endDate.Value);

            return await query
                .OrderByDescending(x => x.Timestamp)
                .Take(limit)
                .ToListAsync();
        }
    }
}