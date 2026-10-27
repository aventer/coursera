public enum UserOperationResult
{
    Success,
    NotFound,
    EmailTaken
}

public interface IUserService
{
    IReadOnlyList<User> GetAll();
    User? GetById(string id);
    User? GetByEmail(string email);
    (UserOperationResult Result, User? User) Create(string name, string email, string password);
    (UserOperationResult Result, User? User) Update(string id, string? name, string? email, string? password);
    UserOperationResult Delete(string id);
}

// In-memory store keyed by the hex string of the User Id; data is lost when the app restarts
public class InMemoryUserService : IUserService
{
    private readonly Dictionary<string, User> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    public IReadOnlyList<User> GetAll()
    {
        lock (_lock)
        {
            return [.. _users.Values];
        }
    }

    public User? GetById(string id)
    {
        lock (_lock)
        {
            return _users.GetValueOrDefault(id);
        }
    }

    public User? GetByEmail(string email)
    {
        string normalized = email.Trim().ToLowerInvariant();
        lock (_lock)
        {
            return _users.Values.FirstOrDefault(u => u.Email == normalized);
        }
    }

    public (UserOperationResult Result, User? User) Create(string name, string email, string password)
    {
        // Hash outside the lock; BCrypt is deliberately slow
        var user = new User(name, email, password);

        lock (_lock)
        {
            if (_users.Values.Any(u => u.Email == user.Email))
            {
                return (UserOperationResult.EmailTaken, null);
            }

            _users[KeyOf(user)] = user;
            return (UserOperationResult.Success, user);
        }
    }

    public (UserOperationResult Result, User? User) Update(string id, string? name, string? email, string? password)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(id, out var user))
            {
                return (UserOperationResult.NotFound, null);
            }

            if (email is not null)
            {
                string normalized = email.Trim().ToLowerInvariant();
                if (_users.Values.Any(u => u != user && u.Email == normalized))
                {
                    return (UserOperationResult.EmailTaken, null);
                }

                // Changing the Email re-derives the Id, so the User has to be re-keyed
                _users.Remove(id);
                user.Email = normalized;
                _users[KeyOf(user)] = user;
            }

            if (name is not null)
            {
                user.Name = name.Trim();
            }

            if (password is not null)
            {
                user.ChangePassword(password);
            }

            return (UserOperationResult.Success, user);
        }
    }

    public UserOperationResult Delete(string id)
    {
        lock (_lock)
        {
            return _users.Remove(id) ? UserOperationResult.Success : UserOperationResult.NotFound;
        }
    }

    private static string KeyOf(User user) => Convert.ToHexString(user.Id);
}
