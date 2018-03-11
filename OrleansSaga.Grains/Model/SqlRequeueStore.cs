using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace OrleansSaga.Grains.Model
{
    public class SqlRequeueStore : RequeueStore
    {
        string _connectionString;

        public const string SelectScheduledSql = @"SELECT * FROM [dbo].[Scheduled] WHERE QueueId = @QueueId";
        public const string SelectEnqueuedSql = @"SELECT * FROM [dbo].[Enqueued] WHERE QueueId = @QueueId ORDER BY Enqueued";
        public const string SelectAssignedSql = @"SELECT * FROM [dbo].[Assigned] WHERE QueueId = @QueueId";
        public const string SelectFinishedSql = @"SELECT TOP (10000) * FROM [dbo].[Finished] WHERE QueueId = @QueueId";
        
        public const string InsertScheduledSql = @"INSERT INTO [dbo].[Scheduled] (QueueId, CommandId, TryCount, Scheduled, RunAt) VALUES (@QueueId, @CommandId, @TryCount, @Scheduled, @RunAt); SELECT CAST(SCOPE_IDENTITY() as bigint);";
        public const string InsertEnqueuedSql = @"INSERT INTO [dbo].[Enqueued] (QueueId, CommandId, TryCount, Enqueued) VALUES (@QueueId, @CommandId, @TryCount, @Enqueued); SELECT CAST(SCOPE_IDENTITY() as bigint);";
        public const string InsertAssignedSql = @"INSERT INTO [dbo].[Assigned] (QueueId, CommandId, TryCount, WorkerId, Assigned) VALUES (@QueueId, @CommandId, @TryCount, @WorkerId, @Assigned); SELECT CAST(SCOPE_IDENTITY() as bigint);";
        public const string InsertFinishedSql = @"INSERT INTO [dbo].[Finished] (QueueId, CommandId, TryCount, Finished, Status, Reason) VALUES (@QueueId, @CommandId, @TryCount, @Finished, @Status, @Reason); SELECT CAST(SCOPE_IDENTITY() as bigint);";

        //public const string InsertQueue = @"INSERT INTO [dbo].[GrainCommandQueue] (CommandId, TryCount, StartDate) VALUES (@CommandId, @TryCount, @StartDate)";
        //public const string InsertLog = @"INSERT INTO [dbo].[GrainCommandLog] (CommandId, TryCount, CompleteDate, CommandStatus, CommandResult) VALUES (@CommandId, @TryCount, @CompleteDate, @CommandStatus, @CommandResult)";
        //public const string SelectById = @"SELECT [CommandId], [QueueId], [Created], [CommandType], [CommandData] FROM [dbo].[GrainCommand] WHERE [CommandId] = @CommandId";
        public const string DeleteEnqueuedSql = @"DELETE FROM [dbo].[Enqueued] WHERE QueueId = @QueueId AND [CommandId] = @CommandId";
        public const string DeleteScheduledSql = @"DELETE FROM [dbo].[Scheduled] WHERE QueueId = @QueueId AND [CommandId] = @CommandId";
        public const string DeleteAssignedSql = @"DELETE FROM [dbo].[Assigned] WHERE QueueId = @QueueId AND [CommandId] = @CommandId";
        public const string DeleteAssignedByWorkerIdSql = @"DELETE FROM [dbo].[Assigned] WHERE QueueId = @QueueId AND [WorkerId] = @WorkerId";

        public SqlRequeueStore(string queueId) : base(queueId)
        {
            _connectionString = ConfigurationManager.ConnectionStrings["SqlRequeueStore"].ConnectionString;
        }

        public override async Task Load()
        {
            await WithConnection(async (c, t) =>
            {
                var p = new DynamicParameters();
                p.Add(nameof(QueueId), QueueId, DbType.String);

                var assigned = await c.QueryAsync<RequeueCommandAssigned>(
                    sql: SelectAssignedSql,
                    param: p,
                    transaction: t,
                    commandType: CommandType.Text);
                Assigned.AddRange(assigned);

                var enqueued = await c.QueryAsync<RequeueCommandEnqueued>(
                    sql: SelectEnqueuedSql,
                    param: p,
                    transaction: t,
                    commandType: CommandType.Text);
                enqueued.ToList().ForEach(Queued.Enqueue);                

                var scheduled = await c.QueryAsync<RequeueCommandScheduled>(
                    sql: SelectScheduledSql,
                    param: p,
                    transaction: t,
                    commandType: CommandType.Text);
                Scheduled.AddRange(scheduled);

                return 0;
            });            
        }

        private static async Task<long> InsertEnqueued(SqlConnection c, SqlTransaction t, RequeueCommandEnqueued enqueued)
        {
            var p2 = new DynamicParameters(enqueued);
            var id = await c.ExecuteScalarAsync<long>(
                    sql: InsertEnqueuedSql,
                    param: p2,
                    transaction: t,
                    commandType: CommandType.Text);
            return id;
        }

        private static async Task<long> InsertAssigned(SqlConnection c, SqlTransaction t, RequeueCommandAssigned assigned)
        {
            var p = new DynamicParameters(assigned);
            var id = await c.ExecuteScalarAsync<long>(
                sql: InsertAssignedSql,
                param: p,
                transaction: t,
                commandType: CommandType.Text);
            return id;
        }

        private static async Task<long> InsertScheduled(SqlConnection c, SqlTransaction t, RequeueCommandScheduled scheduled)
        {
            var p = new DynamicParameters(scheduled);
            var id = await c.ExecuteScalarAsync<long>(
                sql: InsertScheduledSql,
                param: p,
                transaction: t,
                commandType: CommandType.Text);
            return id;
        }

        private static async Task<long> InsertFinished(SqlConnection c, SqlTransaction t, RequeueCommandFinished finished)
        {
            var p = new DynamicParameters(finished);
            var id = await c.ExecuteScalarAsync<long>(
                sql: InsertFinishedSql,
                param: p,
                transaction: t,
                commandType: CommandType.Text);
            return id;
        }

        private async Task DeleteEnqueued(SqlConnection c, SqlTransaction t, long commandId)
        {
            var p = new DynamicParameters();
            p.Add(nameof(QueueId), QueueId, DbType.String);
            p.Add("CommandId", commandId, DbType.Int64);
            await c.ExecuteAsync(
                sql: DeleteEnqueuedSql,
                param: p,
                transaction: t,
                commandType: CommandType.Text);
        }

        private async Task DeleteAssigned(SqlConnection c, SqlTransaction t, long commandId)
        {
            var p = new DynamicParameters();
            p.Add(nameof(QueueId), QueueId, DbType.String);
            p.Add("CommandId", commandId, DbType.Int64);
            await c.ExecuteAsync(
                sql: DeleteAssignedSql,
                param: p,
                transaction: t,
                commandType: CommandType.Text);
        }

        private async Task DeleteAssignedByWorkerId(SqlConnection c, SqlTransaction t, long workerId)
        {
            var p = new DynamicParameters();
            p.Add(nameof(QueueId), QueueId, DbType.String);
            p.Add("WorkerId", workerId, DbType.Int64);
            await c.ExecuteAsync(
                sql: DeleteAssignedByWorkerIdSql,
                param: p,
                transaction: t,
                commandType: CommandType.Text);
        }

        private async Task DeleteScheduled(SqlConnection c, SqlTransaction t, long commandId)
        {
            var p = new DynamicParameters();
            p.Add(nameof(QueueId), QueueId, DbType.String);
            p.Add("CommandId", commandId, DbType.Int64);
            await c.ExecuteAsync(
                    sql: DeleteScheduledSql,
                    param: p,
                    transaction: t,
                    commandType: CommandType.Text);
        }

        public override async Task<RequeueCommandAssigned> Assign(long workerId)
        {
            return await WithConnection(async (c, t) =>
            {
                await DeleteAssignedByWorkerId(c, t, workerId);
                Assigned.RemoveAll(a => a.WorkerId == workerId);

                if (Queued.Count == 0)
                {
                    return null as RequeueCommandAssigned;
                }

                var command = Queued.Dequeue();
                await DeleteEnqueued(c, t, command.CommandId);

                var assigned = new RequeueCommandAssigned { QueueId = QueueId, CommandId = command.CommandId, WorkerId = workerId, TryCount = command.TryCount };
                assigned.Id = await InsertAssigned(c, t, assigned);
                Assigned.Add(assigned);
                return assigned;
            });
        }

        public override async Task Enqueue(params long[] commandIds)
        {
            await WithConnection(async (c, t) =>
            {
                foreach (var commandId in commandIds)
                {
                    var scheduled = Scheduled.Find(a => a.CommandId == commandId);

                    if (scheduled != null)
                    {
                        await DeleteScheduled(c, t, commandId);
                        Scheduled.RemoveAll(a => a.CommandId == commandId);
                    }

                    var enqueued = new RequeueCommandEnqueued { QueueId = QueueId, CommandId = commandId, TryCount = scheduled?.TryCount ?? 0 };
                    enqueued.Id = await InsertEnqueued(c, t, enqueued);
                    Queued.Enqueue(enqueued);
                }
                return 0;
            });

        }

        public override async Task Schedule(DateTimeOffset dateTime, params long[] commandIds)
        {
            await WithConnection(async (c, t) =>
            {
                foreach (var commandId in commandIds)
                {
                    var assigned = Assigned.Find(a => a.CommandId == commandId);

                    if (assigned != null)
                    {
                        await DeleteAssigned(c, t, commandId);
                        Assigned.RemoveAll(a => a.CommandId == commandId);
                    }

                    var scheduled = new RequeueCommandScheduled { QueueId = QueueId, CommandId = commandId, RunAt = dateTime, TryCount = assigned?.TryCount + 1 ?? 0 };
                    scheduled.Id = await InsertScheduled(c, t, scheduled);
                    Scheduled.Add(scheduled);
                }
                return 0;
            });
        }

        public override async Task Complete(long commandId, long workerId)
        {
            await WithConnection(async (c, t) =>
            {
                var assigned = Assigned.Find(a => a.CommandId == commandId);

                await DeleteAssigned(c, t, commandId);
                Assigned.RemoveAll(a => a.CommandId == commandId);

                var finished = new RequeueCommandFinished { QueueId = QueueId, CommandId = commandId, Status = "Complete", TryCount = assigned?.TryCount ?? 0 };
                finished.Id = await InsertFinished(c, t, finished);
                Finished.Add(finished);
                return 0;
            });
        }

        public override async Task Fail(long commandId, long workerId, string reason)
        {
            await WithConnection(async (c, t) =>
            {
                var assigned = Assigned.Find(a => a.CommandId == commandId);

                await DeleteAssigned(c, t, commandId);
                Assigned.RemoveAll(a => a.CommandId == commandId);

                var finished = new RequeueCommandFinished { QueueId = QueueId, CommandId = commandId, Status = "Failed", TryCount = assigned?.TryCount ?? 0, Reason = reason };
                finished.Id = await InsertFinished(c, t, finished);
                Finished.Add(finished);
                return 0;
            });
        }

        protected async Task<T> WithConnection<T>(Func<SqlConnection, SqlTransaction, Task<T>> getData)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync(); // Asynchronously open a connection to the database
                    using (var transaction = connection.BeginTransaction())
                    {
                        var result = await getData(connection, transaction); // Asynchronously execute getData, which has been passed in as a Func<IDBConnection, Task<T>>
                        transaction.Commit();
                        return result;
                    }
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
