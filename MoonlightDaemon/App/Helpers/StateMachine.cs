using MoonlightDaemon.App.Exceptions;

namespace MoonlightDaemon.App.Helpers;

public class StateMachine<T> where T : struct
{
    public T State { get; private set; }
    public readonly List<StateMachineTransition> Transitions = new();

    public SmartEventHandler<T> OnTransitioned { get; set; } = new();

    private readonly object Lock = new();
 
    public StateMachine(T entryState)
    {
        State = entryState;
    }

    public Task<bool> CanTransitionTo(T to)
    {
        lock (Lock)
        {
            var transition = Transitions.FirstOrDefault(x =>
                x.From.ToString() == State.ToString() &&
                x.To.ToString() == to.ToString());

            if (transition == null)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }
    }

    public Task TransitionTo(T to)
    {
        lock (Lock)
        {
            var transition = Transitions.FirstOrDefault(x =>
                x.From.ToString() == State.ToString() &&
                x.To.ToString() == to.ToString());
        
            if (transition == null)
                throw new IllegalStateException($"Cannot transition to {to} from state {State}");

            State = transition.To;
        
            OnTransitioned.Invoke(State);
        }
        
        return Task.CompletedTask;
    }

    public Task SetState(T to)
    {
        State = to;
        
        return Task.CompletedTask;
    }

    public Task AddTransition(T from, T to)
    {
        lock (Transitions)
        {
            Transitions.Add(new()
            {
                From = from,
                To = to,
            });
        }
        
        return Task.CompletedTask;
    }
    
    public class StateMachineTransition
    {
        public T From { get; set; }
        public T To { get; set; }
    }
}