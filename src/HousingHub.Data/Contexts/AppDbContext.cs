using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;

namespace HousingHub.Data.Contexts;

public class DynamoDbTableInitializer
{
    private readonly IAmazonDynamoDB _client;
    private readonly ILogger<DynamoDbTableInitializer> _logger;

    private static readonly Dictionary<string, (string HashKey, List<GlobalSecondaryIndex>? GSIs)> TableDefinitions = new()
    {
        ["Customers"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("Email-index", "Email"),
            CreateGsi("PhoneNumber-index", "PhoneNumber"),
            CreateGsi("GoogleId-index", "GoogleId"),
        }),
        ["Properties"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("OwnerId-index", "OwnerId"),
            CreateGsi("PropertyId-index", "PropertyId"),
        }),
        ["PropertyFiles"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("PropertyId-index", "PropertyId"),
        }),
        ["PropertyAddresses"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("PropertyId-index", "PropertyId"),
        }),
        ["CustomerAddresses"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("CustomerId-index", "CustomerId"),
        }),
        ["PropertyInspections"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("InspectionId-index", "InspectionId"),
            CreateGsi("CustomerId-index", "CustomerId"),
            CreateGsi("PropertyId-index", "PropertyId"),
        }),
        ["Notifications"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("RecipientId-index", "RecipientId"),
        }),
    };

    public DynamoDbTableInitializer(IAmazonDynamoDB client, ILogger<DynamoDbTableInitializer> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        var existingTables = await _client.ListTablesAsync();

        foreach (var (tableName, definition) in TableDefinitions)
        {
            if (existingTables.TableNames.Contains(tableName))
            {
                _logger.LogInformation("DynamoDB table '{TableName}' already exists", tableName);
                continue;
            }

            _logger.LogInformation("Creating DynamoDB table '{TableName}'...", tableName);

            var attributeDefinitions = new List<AttributeDefinition>
            {
                new(definition.HashKey, ScalarAttributeType.S)
            };

            if (definition.GSIs != null)
            {
                foreach (var gsi in definition.GSIs)
                {
                    var gsiKeyAttr = gsi.KeySchema.First().AttributeName;
                    if (attributeDefinitions.All(a => a.AttributeName != gsiKeyAttr))
                    {
                        attributeDefinitions.Add(new AttributeDefinition(gsiKeyAttr, ScalarAttributeType.S));
                    }
                }
            }

            var request = new CreateTableRequest
            {
                TableName = tableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new(definition.HashKey, KeyType.HASH)
                },
                AttributeDefinitions = attributeDefinitions,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                GlobalSecondaryIndexes = definition.GSIs
            };

            await _client.CreateTableAsync(request);
            _logger.LogInformation("DynamoDB table '{TableName}' created successfully", tableName);
        }
    }

    private static GlobalSecondaryIndex CreateGsi(string indexName, string hashKeyAttribute)
    {
        return new GlobalSecondaryIndex
        {
            IndexName = indexName,
            KeySchema = new List<KeySchemaElement>
            {
                new(hashKeyAttribute, KeyType.HASH)
            },
            Projection = new Projection { ProjectionType = ProjectionType.ALL }
        };
    }
}
