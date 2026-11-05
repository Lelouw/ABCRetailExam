namespace ABCRetail.Shared;

public static class RetailMessageValidator
{
    public static string? ValidateOrder(OrderMessage? message)
    {
        if (message is null)
        {
            return "Order information was not provided.";
        }

        if (string.IsNullOrWhiteSpace(message.OrderId))
        {
            return "Order id is required.";
        }

        if (string.IsNullOrWhiteSpace(message.CustomerName))
        {
            return "Customer name is required.";
        }

        if (string.IsNullOrWhiteSpace(message.ProductId) || string.IsNullOrWhiteSpace(message.ProductName))
        {
            return "Product is required.";
        }

        if (message.Quantity <= 0)
        {
            return "Quantity must be greater than zero.";
        }

        if (message.UnitPrice < 0)
        {
            return "Unit price cannot be negative.";
        }

        return null;
    }

    public static string? ValidateInventory(InventoryMessage? message)
    {
        if (message is null)
        {
            return "Inventory information was not provided.";
        }

        if (string.IsNullOrWhiteSpace(message.TransactionId))
        {
            return "Transaction id is required.";
        }

        if (string.IsNullOrWhiteSpace(message.ProductId))
        {
            return "Product is required.";
        }

        if (!InventoryOperations.IsKnown(message.Operation))
        {
            return "Operation must be Restock or Reduce.";
        }

        if (message.Quantity <= 0)
        {
            return "Quantity must be greater than zero.";
        }

        return null;
    }

    public static string? ValidateCustomer(CustomerWrite? customer)
    {
        if (customer is null)
        {
            return "Customer information was not provided.";
        }

        if (string.IsNullOrWhiteSpace(customer.FirstName) || string.IsNullOrWhiteSpace(customer.LastName))
        {
            return "First name and surname are required.";
        }

        if (string.IsNullOrWhiteSpace(customer.Email) || !customer.Email.Contains('@'))
        {
            return "A valid email address is required.";
        }

        if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            return "Phone number is required.";
        }

        return null;
    }

    public static string? ValidateActivityLog(ActivityLogWrite? log)
    {
        if (log is null)
        {
            return "Log information was not provided.";
        }

        if (string.IsNullOrWhiteSpace(log.ReferenceNumber)
            || string.IsNullOrWhiteSpace(log.ActivityType)
            || string.IsNullOrWhiteSpace(log.Description))
        {
            return "Reference number, activity type, and description are required.";
        }

        return null;
    }
}
