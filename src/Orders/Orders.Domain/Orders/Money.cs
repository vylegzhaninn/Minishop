using MiniShop.SharedKernel;

namespace MiniShop.Orders.Domain.Orders;

public sealed record Money(long Minor, string Currency)
{
    public static Money Zero(string currency) => new(0, currency);

    public Result<Money> TryAdd(Money other)
    {
        if (string.IsNullOrWhiteSpace(other.Currency))
            return Result<Money>.Fail(Error.Validation("currency is empty"));
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
            return Result<Money>.Fail(Error.Validation($"Currency mismatch: {Currency} vs {other.Currency}"));
        return Result<Money>.Ok(this with { Minor = checked(Minor + other.Minor) });
    }
}
