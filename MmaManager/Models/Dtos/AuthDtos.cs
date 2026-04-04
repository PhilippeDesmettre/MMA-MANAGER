using System.ComponentModel.DataAnnotations;

namespace MmaManager.Models.Dtos;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(6)]               string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required]               string Password
);

public record AuthResponse(string Token, string Email);
