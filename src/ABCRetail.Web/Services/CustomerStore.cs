using ABCRetail.Shared;
using ABCRetail.Web.Infrastructure;
using Azure.Data.Tables;

namespace ABCRetail.Web.Services;

public interface ICustomerStore
{
    Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken);

    Task<Customer?> GetAsync(string rowKey, CancellationToken cancellationToken);

    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    Task UpdateAsync(Customer customer, CancellationToken cancellationToken);

    Task DeleteAsync(string rowKey, CancellationToken cancellationToken);
}

public sealed class CustomerStore : ICustomerStore
{
    private readonly AzureStorageContext _storage;

    public CustomerStore(AzureStorageContext storage)
    {
        _storage = storage;
    }

    public async Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken)
    {
        var customers = new List<Customer>();
        var table = _storage.GetTable(_storage.Options.CustomerTableName);

        await foreach (var customer in table.QueryAsync<Customer>(
            item => item.PartitionKey == StorageResourceNames.CustomerPartition,
            cancellationToken: cancellationToken))
        {
            customers.Add(customer);
        }

        customers.Sort((left, right) => string.Compare(left.LastName, right.LastName, StringComparison.OrdinalIgnoreCase));
        return customers;
    }

    public async Task<Customer?> GetAsync(string rowKey, CancellationToken cancellationToken)
    {
        var response = await _storage.GetTable(_storage.Options.CustomerTableName)
            .GetEntityIfExistsAsync<Customer>(StorageResourceNames.CustomerPartition, rowKey, cancellationToken: cancellationToken);

        return response.HasValue ? response.Value : null;
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        customer.PartitionKey = StorageResourceNames.CustomerPartition;
        if (string.IsNullOrWhiteSpace(customer.RowKey))
        {
            customer.RowKey = Guid.NewGuid().ToString("N");
        }

        await _storage.GetTable(_storage.Options.CustomerTableName).AddEntityAsync(customer, cancellationToken);
    }

    public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken)
    {
        customer.PartitionKey = StorageResourceNames.CustomerPartition;
        await _storage.GetTable(_storage.Options.CustomerTableName)
            .UpdateEntityAsync(customer, Azure.ETag.All, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task DeleteAsync(string rowKey, CancellationToken cancellationToken)
    {
        await _storage.GetTable(_storage.Options.CustomerTableName)
            .DeleteEntityAsync(StorageResourceNames.CustomerPartition, rowKey, Azure.ETag.All, cancellationToken);
    }
}
