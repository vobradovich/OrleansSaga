using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;

namespace OrleansSaga.Grains.Model
{
    public class SqlEventStore : IEventStore
    {
        public const string SelectById = @"SELECT [GrainId], [EventType], [Data], [TaskStatus], [Created] FROM [dbo].[GrainEventStore] WHERE [GrainId] = @GrainId";

        public const string Insert = @"INSERT INTO [dbo].[GrainEventStore] (GrainId, EventType, Data, TaskStatus, Created) VALUES (@GrainId, @EventType, @Data, @TaskStatus, @Created)";

        string _connectionString;
        public SqlEventStore()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["GrainEventStore"].ConnectionString;
        }

        public async Task AddEvents(params GrainEvent[] events)
        {
            var r = await WithConnection(async c =>
            {
                foreach (var e in events)
                {
                    var p = new DynamicParameters(e);
                    //p.Add("CorrelationId", correlationId, DbType.String);
                    var rows = await c.ExecuteAsync(
                        sql: Insert,
                        param: p,
                        commandType: CommandType.Text);
                }
                return Task.FromResult(0);
            });
        }

        public async Task<IEnumerable<GrainEvent>> LoadEvents(long grainId)
        {
            return await WithConnection(async c =>
            {
                var p = new DynamicParameters();
                p.Add("GrainId", grainId, DbType.Int64);
                var events = await c.QueryAsync<GrainEvent>(
                    sql: SelectById,
                    param: p,
                    commandType: CommandType.Text);
                return events;
            });
        }

        protected async Task<T> WithConnection<T>(Func<SqlConnection, Task<T>> getData)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync(); // Asynchronously open a connection to the database
                    return await getData(connection); // Asynchronously execute getData, which has been passed in as a Func<IDBConnection, Task<T>>
                }
            }
            catch (TimeoutException ex)
            {
                throw new Exception($"{GetType().FullName}.WithConnection() experienced a SQL timeout", ex);
            }
            catch (SqlException ex)
            {
                throw new Exception($"{GetType().FullName}.WithConnection() experienced a SQL exception (not a timeout)", ex);
            }
        }
    }
}
