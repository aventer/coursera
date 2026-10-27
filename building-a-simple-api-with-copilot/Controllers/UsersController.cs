using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(IUserService users, ITokenService tokens) : ControllerBase
{
    // GET api/users
    [HttpGet]
    public ActionResult<IEnumerable<UserResponse>> GetAll()
    {
        return Ok(users.GetAll().Select(UserResponse.From));
    }

    // GET api/users/{id}
    [HttpGet("{id}")]
    public ActionResult<UserResponse> GetById(string id)
    {
        var user = users.GetById(id);
        return user is null ? NotFound() : Ok(UserResponse.From(user));
    }

    // POST api/users — registration is open to anonymous callers
    [HttpPost]
    [AllowAnonymous]
    public ActionResult<UserResponse> Create(CreateUserRequest request)
    {
        var (result, user) = users.Create(request.Name, request.Email, request.Password);
        if (result == UserOperationResult.EmailTaken)
        {
            return Conflict(new { message = "A user with that email already exists." });
        }

        var response = UserResponse.From(user!);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    // POST api/users/login — exchanges an email and password for a JWT
    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var user = users.GetByEmail(request.Email);
        if (user is null || !user.VerifyPassword(request.Password))
        {
            return Unauthorized();
        }

        return Ok(tokens.CreateToken(user));
    }

    // PUT api/users/{id} — users may only update themselves
    [HttpPut("{id}")]
    public ActionResult<UserResponse> Update(string id, UpdateUserRequest request)
    {
        if (!IsCurrentUser(id))
        {
            return Forbid();
        }

        var (result, user) = users.Update(id, request.Name, request.Email, request.Password);
        return result switch
        {
            UserOperationResult.NotFound => NotFound(),
            UserOperationResult.EmailTaken => Conflict(new { message = "A user with that email already exists." }),
            _ => Ok(UserResponse.From(user!))
        };
    }

    // DELETE api/users/{id} — users may only delete themselves
    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        if (!IsCurrentUser(id))
        {
            return Forbid();
        }

        return users.Delete(id) == UserOperationResult.NotFound ? NotFound() : NoContent();
    }

    private bool IsCurrentUser(string id)
    {
        string? subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return string.Equals(subject, id, StringComparison.Ordinal);
    }
}
