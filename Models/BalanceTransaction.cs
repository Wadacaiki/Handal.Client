using System;
namespace Handal.Client.Models;

public enum BalanceTransactionType
{
    Deposit,
    Debit,
    Reserve,
    Release
}

public class BalanceTransaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public BalanceTransactionType Type { get; set; }
    public string? RelatedAuctionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? Description { get; set; }
}
