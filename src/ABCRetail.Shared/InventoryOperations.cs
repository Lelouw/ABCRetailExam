namespace ABCRetail.Shared;

public static class InventoryOperations
{
    public const string Restock = "Restock";
    public const string Reduce = "Reduce";

    public static bool IsKnown(string? operation) =>
        operation is Restock or Reduce;

    public static int Apply(int currentStock, string operation, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (operation == Restock)
        {
            return checked(currentStock + quantity);
        }

        if (operation == Reduce)
        {
            if (quantity > currentStock)
            {
                throw new InvalidOperationException($"Not enough stock. Available: {currentStock}.");
            }

            return currentStock - quantity;
        }

        throw new InvalidOperationException("Operation must be Restock or Reduce.");
    }
}
