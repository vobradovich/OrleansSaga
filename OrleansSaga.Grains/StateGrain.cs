using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using OrleansSaga.Grains.Model;

namespace OrleansSaga.Grains
{
    /// <summary>
    /// Grain implementation class Grain1.
    /// </summary>
    public abstract class StateGrain<TGrainState> : Grain, IStateGrain, IRemindable where TGrainState : class, new()
    {
        protected Queue<Func<TGrainState, TGrainState>> Tasks = new Queue<Func<TGrainState, TGrainState>>();

        protected List<GrainEvent> Events = new List<GrainEvent>();

        protected List<StateCommand> Commands = new List<StateCommand>();

        protected TGrainState CurrentState { get; set; }

        protected IStateGrain Parent { get; set; }

        public override Task OnActivateAsync()
        {
            foreach(var command in Commands)
            {
                if (command.ExecuteAt > DateTime.UtcNow)
                {
                    RegisterOrUpdateReminder(command.Name, command.ExecuteAt - DateTime.UtcNow, TimeSpan.Zero);
                }
                else
                {
                    RegisterTimer((o) => Do(), command, TimeSpan.Zero, TimeSpan.Zero);
                }
            }
            return base.OnActivateAsync();
        }

        public Task Enqueue<TEvent>(Func<TEvent, Task> eventAction)
        {
            throw new NotImplementedException();
        }

        public Task Raise(GrainEvent<TGrainState> ev)
        {
            Events.Add(ev);
            CurrentState = ev.Data;
            return TaskDone.Done;
        }

        public Task OnEvent<TEvent>(Func<TEvent, Task> eventAction)
        {
            return TaskDone.Done;
        }

        public async Task Do()
        {
            try
            {
                //Commands.Remove();
            }
            catch(Exception ex)
            {

            }
            //var result = await action(CurrentState);
            //return result;
        }

        public async Task<TResult> Comensate<TResult>(Func<TGrainState, Task<TResult>> action)
        {
            var result = await action(CurrentState);
            return result;

        }

        public Task ReceiveReminder(string reminderName, TickStatus status)
        {
            throw new NotImplementedException();
        }

        public Task Create()
        {
            return TaskDone.Done;
        }

        public Task Cancel(string reason)
        {
            return TaskDone.Done;
        }
    }
}
