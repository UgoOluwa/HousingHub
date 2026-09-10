using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HousingHub.Data.Contexts;

public class DynamoDbTableInitializer
{
    private readonly IAmazonDynamoDB _client;
    private readonly ILogger<DynamoDbTableInitializer> _logger;

    /// <summary>
    /// Prepended to every name in <see cref="TableDefinitions"/> before it reaches
    /// DynamoDB. Must be the same value the <c>IDynamoDBContext</c> was built with
    /// — see <see cref="DynamoDbNaming"/> for why both read it through one helper.
    /// </summary>
    private readonly string _tablePrefix;

    private static readonly Dictionary<string, (string HashKey, List<GlobalSecondaryIndex>? GSIs)> TableDefinitions = new()
    {
        ["Admins"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("Email-index", "Email"),
        }),
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
            // Sparse by design — only published listings carry the attribute, so this
            // index contains exactly the rows the public site asks for.
            CreateGsi("PublishedStatus-index", "PublishedStatus"),
        }),
        ["PropertyFiles"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("PropertyId-index", "PropertyId"),
        }),
        ["PropertyReports"] = ("Id", new List<GlobalSecondaryIndex>
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
        ["Conversations"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("ParticipantOneId-index", "ParticipantOneId"),
            CreateGsi("ParticipantTwoId-index", "ParticipantTwoId"),
        }),
        ["ChatMessages"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("ConversationId-index", "ConversationId"),
        }),
        ["RefreshTokens"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("TokenHash-index", "TokenHash"),
            CreateGsi("CustomerId-index", "CustomerId"),
        }),
        ["AdminRefreshTokens"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("TokenHash-index", "TokenHash"),
            CreateGsi("AdminId-index", "AdminId"),
        }),
        ["PropertyAlertPreferences"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("CustomerId-index", "CustomerId"),
        }),
        ["VerificationCases"] = ("Id", new List<GlobalSecondaryIndex>
        {
            // "Everything about this business / this property."
            CreateGsi("SubjectId-index", "SubjectId"),
            // "My verification submissions."
            CreateGsi("SubmittedByCustomerId-index", "SubmittedByCustomerId"),
            // Sparse: only Submitted and UnderReview cases carry the attribute, so
            // the admin review queue reads exactly the outstanding work rather than
            // scanning past every case ever decided. See
            // VerificationCase.ReviewQueueStatus.
            CreateGsi("ReviewQueueStatus-index", "ReviewQueueStatus"),
            // Also sparse: only approved cases that carry an expiry. The nightly
            // sweep reads this rather than every case ever decided — most approved
            // cases never expire, since a Certificate of Occupancy does not lapse.
            CreateGsi("ExpiryWatch-index", "ExpiryWatch"),
        }),
        ["VerificationDocuments"] = ("Id", new List<GlobalSecondaryIndex>
        {
            CreateGsi("VerificationCaseId-index", "VerificationCaseId"),
        }),
        ["Payments"] = ("Id", new List<GlobalSecondaryIndex>
        {
            // The gateway only ever knows our reference, so a webhook arrives with
            // nothing else to find the row by.
            CreateGsi("Reference-index", "Reference"),
            // "My receipts."
            CreateGsi("CustomerId-index", "CustomerId"),
            // "Has this verification case been paid for." Asked on every submission,
            // so it must not be a scan of every payment ever taken.
            CreateGsi("SubjectId-index", "SubjectId"),
            // Sparse: only flagged payments carry the attribute, so the admin's
            // "needs a human" queue reads exactly those rather than scanning past
            // every successful payment. See Payment.FlagWatch.
            CreateGsi("FlagWatch-index", "FlagWatch"),
        }),
    };

    public DynamoDbTableInitializer(
        IAmazonDynamoDB client,
        IConfiguration configuration,
        ILogger<DynamoDbTableInitializer> logger)
    {
        _client = client;
        _logger = logger;
        _tablePrefix = DynamoDbNaming.TablePrefix(configuration);
    }

    /// <summary>
    /// Brings DynamoDB into line with the table definitions above.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Creates missing tables, and — the part that used to be absent — adds indexes
    /// declared in code that the deployed table does not have. Before this, an index
    /// added to an existing table was invisible to the initializer, because it
    /// skipped any table that already existed. That produced a specific and silent
    /// failure: the code queries an index that is not there, the repository quietly
    /// degrades to a table scan, and nothing ever says so.
    /// </para>
    /// <para>
    /// <b>Never throws.</b> Every step is guarded individually. Schema convergence is
    /// a background concern and must not stop the API accepting traffic — an app
    /// refusing to start because one index is missing is a worse outcome than the
    /// missing index.
    /// </para>
    /// </remarks>
    public async Task InitializeAsync()
    {
        HashSet<string> existing;

        try
        {
            // ListTables returns at most 100 names per page. With two environments
            // sharing an account that is 32 tables, so a single page is still
            // comfortable — but this is the call that would need paginating first
            // if a third environment ever lands here.
            var listed = await _client.ListTablesAsync();
            existing = listed.TableNames.ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not list DynamoDB tables; skipping schema initialisation");
            return;
        }

        // Logged once per start, and worth the line. If the prefix is wrong, every
        // symptom that follows is silent: tables get created under names nothing
        // reads, and queries return empty rather than failing. This is the only
        // place that states which set of tables this process is about to touch.
        _logger.LogInformation(
            "Reconciling DynamoDB schema for {Count} tables with prefix '{TablePrefix}'",
            TableDefinitions.Count,
            _tablePrefix);

        foreach (var (logicalName, definition) in TableDefinitions)
        {
            // The dictionary holds logical names; DynamoDB is addressed by physical
            // ones. Converting here rather than in the definitions keeps the
            // declarations readable and the prefix applied in exactly one place.
            var tableName = _tablePrefix + logicalName;

            try
            {
                if (existing.Contains(tableName))
                {
                    await AddMissingIndexesAsync(tableName, definition.GSIs);
                }
                else
                {
                    await CreateTableAsync(tableName, definition);
                }
            }
            catch (Exception ex)
            {
                // One bad table must not stop the others. A failure here means that
                // table is out of date, not that the app cannot run.
                _logger.LogError(ex, "Could not reconcile DynamoDB table '{TableName}'", tableName);
            }
        }
    }

    private async Task CreateTableAsync(
        string tableName, (string HashKey, List<GlobalSecondaryIndex>? GSIs) definition)
    {
        _logger.LogInformation("Creating DynamoDB table '{TableName}'...", tableName);

        var attributeDefinitions = new List<AttributeDefinition>
        {
            new(definition.HashKey, ScalarAttributeType.S)
        };

        foreach (var gsi in definition.GSIs ?? [])
        {
            var keyAttribute = gsi.KeySchema.First().AttributeName;
            if (attributeDefinitions.All(a => a.AttributeName != keyAttribute))
            {
                attributeDefinitions.Add(new AttributeDefinition(keyAttribute, ScalarAttributeType.S));
            }
        }

        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = [new KeySchemaElement(definition.HashKey, KeyType.HASH)],
            AttributeDefinitions = attributeDefinitions,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            GlobalSecondaryIndexes = definition.GSIs,
        });

        _logger.LogInformation(
            "DynamoDB table '{TableName}' created. It becomes queryable once AWS finishes "
            + "provisioning it, which takes a short while.",
            tableName);
    }

    /// <summary>
    /// Adds indexes the code declares but the deployed table lacks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One index per call, by necessity.</b> DynamoDB permits only a single GSI in
    /// CREATING state per table, and rejects a second while one is building. So this
    /// adds the first missing index it finds and returns; the next startup picks up
    /// the one after. A table gaining several indexes converges over a few deploys
    /// rather than in one pass.
    /// </para>
    /// <para>
    /// A table that is not ACTIVE is left alone. It is either still being created or
    /// already updating, and either way it will not accept an index change now.
    /// </para>
    /// </remarks>
    private async Task AddMissingIndexesAsync(string tableName, List<GlobalSecondaryIndex>? declared)
    {
        if (declared is null || declared.Count == 0) return;

        var description = (await _client.DescribeTableAsync(tableName)).Table;

        if (description.TableStatus != TableStatus.ACTIVE)
        {
            _logger.LogInformation(
                "DynamoDB table '{TableName}' is {Status}; leaving indexes until it settles",
                tableName, description.TableStatus);
            return;
        }

        var live = (description.GlobalSecondaryIndexes ?? [])
            .Select(i => i.IndexName)
            .ToHashSet(StringComparer.Ordinal);

        var missing = declared.FirstOrDefault(g => !live.Contains(g.IndexName));
        if (missing is null) return;

        var keyAttribute = missing.KeySchema.First().AttributeName;

        _logger.LogWarning(
            "DynamoDB table '{TableName}' is missing index '{IndexName}'. Adding it — "
            + "queries relying on it fall back to a table scan until it finishes building.",
            tableName, missing.IndexName);

        await _client.UpdateTableAsync(new UpdateTableRequest
        {
            TableName = tableName,
            // Only the attributes referenced by the new index are declared here;
            // DynamoDB rejects definitions for attributes no key uses.
            AttributeDefinitions = [new AttributeDefinition(keyAttribute, ScalarAttributeType.S)],
            GlobalSecondaryIndexUpdates =
            [
                new GlobalSecondaryIndexUpdate
                {
                    Create = new CreateGlobalSecondaryIndexAction
                    {
                        IndexName = missing.IndexName,
                        KeySchema = missing.KeySchema,
                        Projection = missing.Projection,
                    },
                },
            ],
        });

        var remaining = declared.Count(g => !live.Contains(g.IndexName)) - 1;
        if (remaining > 0)
        {
            _logger.LogInformation(
                "'{TableName}' has {Remaining} more index(es) to add. DynamoDB builds one at a "
                + "time, so they will be added on subsequent startups.",
                tableName, remaining);
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
