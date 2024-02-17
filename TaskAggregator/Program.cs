using System.Collections.Concurrent;

var taskAggregator = new TaskAggregator();
var task1 = taskAggregator.GetUsers(Enumerable.Range(1, 10).ToHashSet(), CancellationToken.None);
var task2 = taskAggregator.GetUsers(Enumerable.Range(5, 10).ToHashSet(), CancellationToken.None);
var task3 = taskAggregator.GetUsers(Enumerable.Range(10, 10).ToHashSet(), CancellationToken.None);
var task4 = taskAggregator.GetUsers(Enumerable.Range(15, 10).ToHashSet(), CancellationToken.None);
var task5 = taskAggregator.GetUsers(Enumerable.Range(20, 10).ToHashSet(), CancellationToken.None);

await Task.WhenAll(task1, task2, task3, task4, task5);

var users1 = await task1;
var users2 = await task2;
var users3 = await task3;
var users4 = await task4;
var users5 = await task5;

Console.WriteLine("Hello, World!");


public sealed class TaskAggregator
{
    private readonly Mutex _mutex = new();
    private static readonly ConcurrentDictionary<Guid, TaskContext> Dictionary = new();

    public Task<IReadOnlyCollection<User>> GetUsers(IReadOnlySet<int> userIds, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<IReadOnlyCollection<User>>();
        Dictionary.TryAdd(Guid.NewGuid(), new TaskContext(tcs, userIds));

        if (CanExecute)
        {
            _mutex.WaitOne();
            ExecuteAsync(ct);
            _mutex.ReleaseMutex();
        }

        return tcs.Task;
    }

    private static bool CanExecute => Dictionary.Count == 5;

    private static async Task ExecuteAsync(CancellationToken ct)
    {
        var values = Dictionary.Values;
        var ids = values.SelectMany(value => value.UserIds).ToHashSet();

        var userRepository = new UserRepository();
        var users = await userRepository.GetUsers(ids, ct);

        foreach (var value in values)
        {
            var taskUsers = users.Where(user => value.UserIds.Contains(user.Id)).ToArray();
            value.TaskCompletionSource.SetResult(taskUsers);
        }
        
        Dictionary.Clear();
    }
    
    private readonly record struct TaskContext(
        TaskCompletionSource<IReadOnlyCollection<User>> TaskCompletionSource,
        IReadOnlySet<int> UserIds
    );
}

public sealed class UserRepository
{
    public async Task<IReadOnlyCollection<User>> GetUsers(IReadOnlySet<int> userIds, CancellationToken ct)
    {
        return Users.Where(user => userIds.Contains(user.Id)).ToArray();
    }

    private static readonly IReadOnlyCollection<User> Users = Enumerable
        .Range(1, 100)
        .Select(id => new User(id, $"Name_{id}", $"Surname_{id}"))
        .ToArray();
}

public sealed record User(int Id, string Name, string Surname);