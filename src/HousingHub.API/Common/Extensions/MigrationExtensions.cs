using HousingHub.Data.Contexts;

namespace HousingHub.API.Common.Extensions;

public static class MigrationExtensions
{
    /// <summary>
    /// Brings the DynamoDB schema into line with the code, without blocking startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>On by default.</b> This was previously gated behind
    /// <c>Dynamo:AutoCreateTables</c>, off in deployed environments, on the reasoning
    /// that tables belong in an infrastructure definition. That reasoning is sound and
    /// there is no such definition — so in practice the gate meant every schema change
    /// became a manual console task that had to be remembered, and the failure when it
    /// was forgotten was silent: a query against a missing index degrades to a table
    /// scan and says nothing.
    /// </para>
    /// <para>
    /// Automatic-and-slightly-wasteful beats correct-and-not-done. Set
    /// <c>Dynamo:AutoCreateTables</c> to false to switch it off once real IaC exists.
    /// </para>
    /// <para>
    /// <b>Deliberately not awaited.</b> Blocking startup would buy nothing:
    /// CreateTable and UpdateTable are asynchronous on DynamoDB's side, so the table
    /// is still unusable when the call returns. Waiting would delay every cold start
    /// to achieve the same end state a moment later. The task is fire-and-forget with
    /// its own exception handling, so a schema problem degrades the app rather than
    /// preventing it from starting.
    /// </para>
    /// <para>
    /// <b>IAM.</b> The execution role needs <c>dynamodb:ListTables</c>,
    /// <c>DescribeTable</c>, <c>CreateTable</c> and <c>UpdateTable</c>. That is a
    /// genuine widening of what the API can do — the trade for not having to remember
    /// a console step. Revisit it when the schema moves into infrastructure code.
    /// </para>
    /// </remarks>
    public static void InitializeDynamoDb(this IApplicationBuilder app, IConfiguration configuration)
    {
        if (!configuration.GetValue("Dynamo:AutoCreateTables", defaultValue: true))
            return;

        _ = Task.Run(async () =>
        {
            using var scope = app.ApplicationServices.CreateScope();

            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(MigrationExtensions));

            try
            {
                var initializer = scope.ServiceProvider.GetRequiredService<DynamoDbTableInitializer>();
                await initializer.InitializeAsync();
            }
            catch (Exception ex)
            {
                // InitializeAsync already guards each table, so reaching here means
                // something structural failed — resolution, credentials, region. Must
                // still be caught: an unobserved exception in a fire-and-forget task
                // is a process-level risk.
                logger.LogError(ex, "DynamoDB schema initialisation failed to run");
            }
        });
    }
}
