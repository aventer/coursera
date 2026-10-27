// Storage abstraction for Users; swap the registered implementation (e.g. for a database-backed one) without touching the service layer
public interface IUserRepository
{
    IReadOnlyList<User> GetAll();
    User? GetById(string id);
    User? GetByEmail(string email);

    // Returns false when another User already has the same Email (acts like a unique constraint)
    bool TryAdd(User user);

    // Persists changes to a User; previousId is the key it was stored under, which differs from the current Id after an Email change
    bool Update(string previousId, User user);

    bool Remove(string id);
}

// In-memory store keyed by the base64url encoded User Id; data is lost when the app restarts
public class InMemoryUserRepository : IUserRepository
{
    // Ordinal: base64url is case-sensitive
    private readonly Dictionary<string, User> _users = new(StringComparer.Ordinal);
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

    public bool TryAdd(User user)
    {
        lock (_lock)
        {
            if (_users.Values.Any(u => u.Email == user.Email))
            {
                return false;
            }

            _users[KeyOf(user)] = user;
            return true;
        }
    }

    public bool Update(string previousId, User user)
    {
        lock (_lock)
        {
            if (!_users.Remove(previousId))
            {
                return false;
            }

            // Re-key in case the Email (and therefore the Id) changed
            _users[KeyOf(user)] = user;
            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (_lock)
        {
            return _users.Remove(id);
        }
    }

    private static string KeyOf(User user) => user.EncodedId;
}
