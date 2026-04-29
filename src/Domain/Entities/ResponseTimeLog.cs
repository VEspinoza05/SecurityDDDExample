#nullable enable
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DDDExample.Domain.Entities
{
    public class ResponseTimeLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        
        public string? Path { get; set; }
        public string? Method { get; set; }
        public string? QueryString { get; set; }
        public long DurationMs { get; set; }
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? ClientIp { get; set; }
        public string? UserAgent { get; set; }
        public bool IsSlowRequest { get; set; }
    }
}