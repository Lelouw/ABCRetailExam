namespace ABCRetail.Shared;

public static class OrderCalculator
{
    public static double Total(double unitPrice, int quantity)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice));
        }

        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        return Math.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);
    }
}
