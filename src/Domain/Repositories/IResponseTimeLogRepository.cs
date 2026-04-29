#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using DDDExample.Domain.Entities;

namespace DDDExample.Domain.Repositories
{
    public interface IResponseTimeLogRepository
    {
        Task AddAsync(ResponseTimeLog log);
        Task<IEnumerable<ResponseTimeLog>> GetLogsAsync(
            string? path = null, 
            string? method = null, 
            int? minDurationMs = null, 
            DateTime? startDate = null, 
            DateTime? endDate = null,
            int limit = 100);
    }
}