namespace TinyCrm.Api.Dtos;

public record LoginRequest(string Username, string Password);

public record UserDto(int Id, string Username, string DisplayName);
