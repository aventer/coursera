using System.ComponentModel.DataAnnotations;

// Request body for registering a new User
public record CreateUserRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, EmailAddress, StringLength(254)] string Email,
    // BCrypt only uses the first 72 bytes of a password
    [Required, StringLength(72, MinimumLength = 8)] string Password);

// Request body for updating a User; null fields are left unchanged
public record UpdateUserRequest(
    [StringLength(100, MinimumLength = 1)] string? Name,
    [EmailAddress, StringLength(254)] string? Email,
    [StringLength(72, MinimumLength = 8)] string? Password);

// Request body for logging in
public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

// Response returned after a successful login
public record LoginResponse(string Token, DateTime ExpiresAt);

// Public view of a User; never exposes the password hash
public record UserResponse(string Id, string Name, string Email)
{
    // Id is returned base64url encoded so it can be used safely in URLs
    public static UserResponse From(User user) => new(user.EncodedId, user.Name, user.Email);
}
