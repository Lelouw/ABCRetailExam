# ABC Retail exam app

ASP.NET Core MVC app plus Azure Functions for the ABC Retail cloud practical.

The website stores customers, products, and orders in Azure. Orders and inventory changes are queue messages. Functions process those queues, update stock, and write a log file.

## Projects

- `src/ABCRetail.Web` is the website.
- `src/ABCRetail.Functions` is the function app.
- `src/ABCRetail.Shared` holds the shared models and validation rules.

## Run it locally

1. Install Azurite and start it in a separate terminal:

```powershell
npm install -g azurite
azurite --silent
```

2. Start the website:

```powershell
dotnet run --project src/ABCRetail.Web --launch-profile https
```

Open `https://localhost:7188`.

3. Install [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) v4, then start the functions:

```powershell
cd src/ABCRetail.Functions
func start
```

Place an order in the website, then refresh Orders. The status changes from Queued to Processed, stock drops, and a file appears under Logs.

The Development configuration uses `UseDevelopmentStorage=true` for tables, blobs, and queues. Azurite does not support Azure Files, so contracts and logs are written under `%LOCALAPPDATA%\ABCRetailExam` until you use a real storage account. Do not put a real storage key in `appsettings.json`.

To point the website at a real storage account:

```powershell
dotnet user-secrets init --project src/ABCRetail.Web
dotnet user-secrets set "AzureStorage:ConnectionString" "<storage-connection-string>" --project src/ABCRetail.Web
```

For the functions, put that same connection string in `src/ABCRetail.Functions/local.settings.json` as `AzureWebJobsStorage`.

## What each screen proves

| Screen | Azure service |
| --- | --- |
| Customers | Table Storage |
| Products | Table Storage and Blob Storage |
| Orders | Queue Storage, then the ProcessOrder function |
| Inventory | Queue Storage, then the ProcessInventory function |
| Contracts | Azure Files (`contracts`) |
| Logs | Azure Files (`logs`) |

HTTP functions, used when the question asks for a function URL:

- `GET /api/health`
- `POST /api/customers`
- `POST /api/product-images`
- `POST /api/orders`
- `POST /api/inventory`
- `POST /api/activity-logs`

The POST functions use a function key once they are deployed. Health is anonymous.

## Azure setup

Use one resource group, one storage account, one App Service app, and one Function App. Keep them in the same region.

Suggested names, with your student number on the end so they are unique:

- Resource group: `rg-abcretail-exam`
- Storage account: `stabcretail` plus your student number, lowercase, no hyphens
- Web app: `abcretail-web-` plus your student number
- Function app: `abcretail-func-` plus your student number

### 1. Resource group

Azure portal, Resource groups, Create. Pick the region your subscription allows. South Africa North is the usual choice when it is available.

### 2. Storage account

Create a Storage account in that resource group.

- Performance: Standard
- Redundancy: LRS
- Leave blob public access disabled. The website streams images itself.

After it is created, open Security + networking, Access keys, and copy the connection string from key1. Keep it out of source control.

You do not create the tables, container, queues, or file shares by hand. The website creates them on startup:

- Tables: `Customers`, `Products`, `Orders`
- Container: `product-images`
- Queues: `order-processing`, `inventory-processing`
- File shares: `contracts`, `logs`

Confirm them later in Storage browser, after the site has run once against this account.

### 3. Web app

Create a Web App.

- Publish: Code
- Runtime: .NET 10
- Operating system: Windows or Linux
- Plan: a free or basic plan is enough for the practical

When the app exists, open Environment variables and add:

- Name: `AzureStorage__ConnectionString`
- Value: the storage connection string

The double underscore maps to the `AzureStorage:ConnectionString` setting. Leave `ASPNETCORE_ENVIRONMENT` as Production so Azure does not use the local emulator.

Deploy from the `src/ABCRetail.Web` folder. In Visual Studio, right-click the project, Publish, Azure, App Service. Or from a terminal:

```powershell
dotnet publish src/ABCRetail.Web -c Release -o publish
Compress-Archive -Path publish\* -DestinationPath web.zip -Force
az webapp deploy --resource-group rg-abcretail-exam --name <web-app-name> --src-path web.zip --type zip
```

Open the site URL and add a customer. Then open the storage account, Storage browser, and screenshot the Customers table.

### 4. Function app

Create a Function App in the same resource group and region.

- Runtime: .NET, isolated worker
- Version: 10
- Hosting: Consumption is the usual practical choice
- Storage account: the same account created above

Creating it sets `AzureWebJobsStorage` for you. Also add these application settings. The queue names are required, because the triggers read them:

- `FUNCTIONS_WORKER_RUNTIME` = `dotnet-isolated`
- `OrderQueueName` = `order-processing`
- `InventoryQueueName` = `inventory-processing`
- `CustomerTableName` = `Customers`
- `ProductTableName` = `Products`
- `OrderTableName` = `Orders`
- `ImageContainerName` = `product-images`
- `LogShareName` = `logs`

Deploy `src/ABCRetail.Functions` with Visual Studio Publish, or:

```powershell
func azure functionapp publish <function-app-name>
```

Run that command from `src/ABCRetail.Functions` after `az login`.

Open the function app, Functions, and confirm `ProcessOrder`, `ProcessInventory`, `Health`, `SaveCustomer`, `ProductMedia`, `QueueOrder`, `QueueInventory`, and `SaveActivityLog` are listed.

### 5. Prove the queue flow

1. On the website, add a customer and a product with stock of at least 5.
2. Place an order for 1 of that product.
3. Refresh Orders. Status should become Processed within a few seconds.
4. Open the product again. Stock should be one lower.
5. Open Logs and download `order-<order id>.txt`.
6. In the portal, open the function, Monitor, and screenshot the successful invocation.
7. Repeat with Inventory, using Restock, and screenshot `inventory-<transaction id>.txt` plus the queue in Storage browser.

If an order stays Queued, the function app is not reading the queue. Check that `AzureWebJobsStorage` is the same account the website uses, and that `OrderQueueName` is `order-processing`.

### 6. Screenshots to keep

- Resource group overview
- Storage account overview
- Storage browser for the table, blob, queue, and file share
- Web app overview and the Environment variables name, with the secret value hidden
- The live website on each module
- Function app overview, the function list, and one successful monitor trace

Do not screenshot the full connection string or function keys.
