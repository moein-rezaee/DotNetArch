namespace IdentityService.Application.Features.Identity.Models;

public record SendCodeRequest(string PhoneNumber);
public record SendCodeResponse(bool Accepted, string Delivery);
public record VerifyCodeRequest(string PhoneNumber, string Code);
public record TokenResponse(string AccessToken, string RefreshToken);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
