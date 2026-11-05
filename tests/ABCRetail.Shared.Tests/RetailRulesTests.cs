using ABCRetail.Shared;

namespace ABCRetail.Shared.Tests;

public class RetailRulesTests
{
    [Fact]
    public void OrderTotal_MultipliesPriceAndQuantity()
    {
        var total = OrderCalculator.Total(20, 3);

        Assert.Equal(60, total);
    }

    [Fact]
    public void OrderValidator_RejectsZeroQuantity()
    {
        var error = RetailMessageValidator.ValidateOrder(new OrderMessage
        {
            OrderId = "ORD-1",
            CustomerName = "Ada",
            ProductId = "p1",
            ProductName = "Tea",
            Quantity = 0,
            UnitPrice = 10
        });

        Assert.Equal("Quantity must be greater than zero.", error);
    }

    [Theory]
    [InlineData(10, InventoryOperations.Restock, 4, 14)]
    [InlineData(10, InventoryOperations.Reduce, 4, 6)]
    public void InventoryApply_UpdatesStock(int current, string operation, int quantity, int expected)
    {
        Assert.Equal(expected, InventoryOperations.Apply(current, operation, quantity));
    }

    [Fact]
    public void InventoryApply_RejectsStockBelowZero()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            InventoryOperations.Apply(2, InventoryOperations.Reduce, 5));

        Assert.Contains("Not enough stock", error.Message);
    }

    [Fact]
    public void FileNameSanitizer_RemovesPathCharacters()
    {
        var cleaned = FileNameSanitizer.Sanitize(@"..\secret\invoice.pdf");

        Assert.Equal("invoice.pdf", cleaned);
        Assert.False(FileNameSanitizer.IsSafeFileName(@"..\invoice.pdf"));
        Assert.True(FileNameSanitizer.IsSafeFileName("invoice.pdf"));
    }

    [Fact]
    public void LocalFileShare_DetectsTheEmulatorConnectionString()
    {
        Assert.True(LocalFileShare.IsEmulator("UseDevelopmentStorage=true"));
        Assert.False(LocalFileShare.IsEmulator("DefaultEndpointsProtocol=https;AccountName=examstore;AccountKey=secret;"));
    }

    [Fact]
    public void ImageRules_RejectLargeFiles()
    {
        var error = ImageUploadRules.Validate("image/png", ImageUploadRules.MaxBytes + 1);

        Assert.Equal("The image must be 5 MB or smaller.", error);
    }
}
