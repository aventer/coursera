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

// Business rules for Users; persistence is delegated to whichever IUserRepository is registered
public class UserService(IUserRepository repository) : IUserService
{
    public IReadOnlyList<User> GetAll() => repository.GetAll();

    public User? GetById(string id) => repository.GetById(id);

    public User? GetByEmail(string email) => repository.GetByEmail(email);

    public (UserOperationResult Result, User? User) Create(string name, string email, string password)
    {
        var user = new User(name, email, password);
        return repository.TryAdd(user)
            ? (UserOperationResult.Success, user)
            : (UserOperationResult.EmailTaken, null);
    }

    public (UserOperationResult Result, User? User) Update(string id, string? name, string? email, string? password)
    {
        var user = repository.GetById(id);
        if (user is null)
        {
            return (UserOperationResult.NotFound, null);
        }

        // Checked before anything is changed so a rejected update leaves the User untouched
        if (email is not null)
        {
            var existing = repository.GetByEmail(email);
            if (existing is not null && existing != user)
            {
                return (UserOperationResult.EmailTaken, null);
            }

            // Changing the Email re-derives the Id
            user.Email = email;
        }

        if (name is not null)
        {
            user.Name = name.Trim();
        }

        if (password is not null)
        {
            user.ChangePassword(password);
        }

        return repository.Update(id, user)
            ? (UserOperationResult.Success, user)
            : (UserOperationResult.NotFound, null);
    }

    public UserOperationResult Delete(string id)
    {
        return repository.Remove(id) ? UserOperationResult.Success : UserOperationResult.NotFound;
    }
}
