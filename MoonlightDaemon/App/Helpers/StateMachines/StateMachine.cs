namespace MoonlightDaemon.App.Helpers.StateMachines;

public class StateMachine<T> where T : struct
{
    public T State { get; private set; }
    public readonly List<StateMachineTransition> Transitions = new();
    
    public EventHandler<T> OnTransitioning { get; set; }
    public EventHandler<T> OnTransitioned { get; set; }

    private readonly object Lock = new();
 
    public StateMachine(T entryState)
    {
        State = entryState;
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

            OnTransitioning?.Invoke(this, to);
        
            if(transition.OnTransitioning != null)
                transition.OnTransitioning.Invoke().Wait(); 

            State = transition.To;
        
            OnTransitioned?.Invoke(this, State);
        }
        
        return Task.CompletedTask;
    }

    public Task AddTransition(T from, T to, Func<Task>? action = null)
    {
        Transitions.Add(new()
        {
            From = from,
            To = to,
            OnTransitioning = action
        });
        
        return Task.CompletedTask;
    }
    
    public class StateMachineTransition
    {
        public T From { get; set; }
        public T To { get; set; }
        public Func<Task>? OnTransitioning { get; set; } 
    }
}