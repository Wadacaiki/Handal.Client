using System;
namespace Handal.Client.Models;

public class VerificationCodeEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
