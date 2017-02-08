using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace OrleansSaga.Grains.Model
{
    public class SqlCommandStore : ICommandStore
    {
        string _connectionString;

        public const string InsertCommand = @"INSERT INTO [dbo].[GrainCommand] (QueueId, Created, CommandType, CommandData) VALUES (@QueueId, @Created, @CommandType, @CommandData); SELECT CAST(SCOPE_IDENTITY() as bigint);";
        public const string InsertQueue = @"INSERT INTO [dbo].[GrainCommandQueue] (CommandId, TryCount, StartDate) VALUES (@CommandId, @TryCount, @StartDate)";
        public const string InsertLog = @"INSERT INTO [dbo].[GrainCommandLog] (CommandId, TryCount, CompleteDate, CommandStatus, CommandResult) VALUES (@CommandId, @TryCount, @CompleteDate, @CommandStatus, @CommandResult)";
        public const string SelectById = @"SELECT [CommandId], [QueueId], [Created], [CommandType], [CommandData] FROM [dbo].[GrainCommand] WHERE [CommandId] = @CommandId";
        public const string DeleteQueue = @"DELETE FROM [dbo].[GrainCommandQueue] WHERE [CommandId] = @CommandId";
        public const string SelectByQueueId = @"
SELECT q.[CommandId]
      ,q.[TryCount]
      ,q.[StartDate]
	  ,c.[CommandId]
      ,c.[QueueId]
      ,c.[Created]
      ,c.[CommandType]
      ,c.[CommandData]
FROM [dbo].[GrainCommandQueue] q
INNER JOIN [dbo].[GrainCommand] c ON q.[CommandId] = c.[CommandId]
WHERE c.QueueId = @QueueId";

        public SqlCommandStore()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["SqlCommandStore"].ConnectionString;
        }
        
        public async Task Add(params GrainCommand[] commands)
        {
            var r = await WithConnection(async c =>
            {
                foreach (var cmd in commands)
                {
                    var p = new DynamicParameters(cmd);
                    var commandId = await c.ExecuteScalarAsync<long>(
                        sql: InsertCommand,
                        param: p,
                        commandType: CommandType.Text);
                    cmd.CommandId = commandId;
                }
                return Task.FromResult(0);
            });
        }        

        public async Task<GrainCommandQueue> Enqueue(GrainCommand command, DateTime startDate, int tryCount)
        {
            var q = new GrainCommandQueue
            {
                Command = command,
                CommandId = command.CommandId,
                StartDate = startDate,
                TryCount = tryCount
            };
            var r = await WithConnection(async c =>
            {
                var p = new DynamicParameters(q);
                var rows = await c.ExecuteAsync(
                    sql: InsertQueue,
                    param: p,
                    commandType: CommandType.Text);
                return Task.FromResult(0);
            });
            return q;
        }

        public async Task Complete(GrainCommandQueue commandQueue)
        {
            var log = new GrainCommandLog
            {
                CommandId = commandQueue.CommandId,
                TryCount = commandQueue.TryCount,
                CompleteDate = DateTime.UtcNow,
                CommandStatus = "Completed",
                CommandResult = ""
            };
            await WithConnection(async c =>
            {
                var p = new DynamicParameters(log);
                var cmd = await c.ExecuteAsync(
                    sql: InsertLog,
                    param: p,
                    commandType: CommandType.Text);

                var p2 = new DynamicParameters();
                p2.Add("CommandId", commandQueue.CommandId, DbType.Int64);
                var rows = await c.ExecuteAsync(
                    sql: DeleteQueue,
                    param: p,
                    commandType: CommandType.Text);
                return Task.FromResult(0);
            });
        }

        public async Task Fail(GrainCommandQueue commandQueue, Exception ex)
        {
            var log = new GrainCommandLog
            {
                CommandId = commandQueue.CommandId,
                TryCount = commandQueue.TryCount,
                CompleteDate = DateTime.UtcNow,
                CommandStatus = "Faulted",
                CommandResult = $"{ex}"
            };
            await WithConnection(async c =>
            {
                var p = new DynamicParameters(log);
                var cmd = await c.ExecuteAsync(
                    sql: InsertLog,
                    param: p,
                    commandType: CommandType.Text);

                var p2 = new DynamicParameters();
                p2.Add("CommandId", commandQueue.CommandId, DbType.Int64);
                var rows = await c.ExecuteAsync(
                    sql: DeleteQueue,
                    param: p,
                    commandType: CommandType.Text);
                return Task.FromResult(0);
            });
        }

        public async Task<GrainCommand> Get(long commandId)
        {
            return await WithConnection(async c =>
            {
                var p = new DynamicParameters();
                p.Add("CommandId", commandId, DbType.Int64);
                var cmd = await c.QuerySingleOrDefaultAsync<GrainCommand>(
                    sql: SelectById,
                    param: p,
                    commandType: CommandType.Text);
                return cmd;
            });
        }

        public async Task<IEnumerable<GrainCommandQueue>> GetQueuedCommands(Guid queueId)
        {
            return await WithConnection(async c =>
            {
                var p = new DynamicParameters();
                p.Add("QueueId", queueId, DbType.Guid);
                var commands = await c.QueryAsync<GrainCommandQueue, GrainCommand, GrainCommandQueue>(
                    SelectByQueueId,
                    (q, cmd) =>
                    {
                        q.Command = cmd;
                        return q;
                    },
                    splitOn: "CommandId",
                    param: p,
                    commandType: CommandType.Text);
                return commands;
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
