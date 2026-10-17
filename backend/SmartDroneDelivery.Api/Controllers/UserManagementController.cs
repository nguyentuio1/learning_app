using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UserManagementController : ControllerBase
{
    private readonly AppDbContext _context;

    public UserManagementController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _context.Users
            .Select(u => new UserResponse(u.Id, u.Email, u.Role != null ? u.Role.Name : string.Empty, u.CreatedAt))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
        if (emailExists) return BadRequest("Email is already registered.");

        var targetRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == request.Role);
        if (targetRole == null) return BadRequest("Specified role does not exist.");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = request.Password,
            Role = targetRole
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAllUsers), new { id = user.Id }, new UserResponse(user.Id, user.Email, user.Role.Name, user.CreatedAt));
    }

    [HttpPut("{id}/role")]
    public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound("User not found.");

        var targetRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == request.Role);
        if (targetRole == null) return BadRequest("Specified role does not exist.");

        user.Role = targetRole;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
