using System.Linq.Expressions;
using HousingHub.Model.Entities;
using HousingHub.Repository.Queries;

namespace HousingHub.Test.Repository;

/// <summary>
/// The extractor decides whether a read can be narrowed to an index.
/// </summary>
/// <remarks>
/// The failure mode that matters is <b>over</b>-extraction: treating a condition as
/// necessary when it is not — an OR branch, say — would narrow the read to a subset
/// and silently drop rows. Under-extraction only costs a scan.
///
/// So the important tests here are the ones asserting that nothing is returned.
/// </remarks>
public class EqualityPredicateExtractorTests
{
    private static IReadOnlyList<EqualityPredicateExtractor.Candidate> Extract(
        Expression<Func<Customer, bool>> predicate) =>
        EqualityPredicateExtractor.Extract(predicate);

    // ── Should extract ───────────────────────────────────────────

    [Fact]
    public void SimpleEquality_IsExtracted()
    {
        var email = "john@test.com";
        var found = Extract(x => x.Email == email);

        var candidate = Assert.Single(found);
        Assert.Equal("Email", candidate.PropertyName);
        Assert.Equal(email, candidate.Value);
    }

    [Fact]
    public void ReversedOperands_AreExtracted()
    {
        var email = "john@test.com";
        var found = Extract(x => email == x.Email);

        var candidate = Assert.Single(found);
        Assert.Equal("Email", candidate.PropertyName);
    }

    [Fact]
    public void BothSidesOfAnAnd_AreExtracted()
    {
        // Both conditions must hold, so narrowing on either is sound.
        var id = Guid.NewGuid();
        var email = "john@test.com";

        var found = Extract(x => x.Id == id && x.Email == email);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, c => c.PropertyName == "Id");
        Assert.Contains(found, c => c.PropertyName == "Email");
    }

    [Fact]
    public void LiteralValue_IsExtracted()
    {
        var found = Extract(x => x.Email == "literal@test.com");

        var candidate = Assert.Single(found);
        Assert.Equal("literal@test.com", candidate.Value);
    }

    // ── Must NOT extract — these would lose rows ─────────────────

    [Fact]
    public void Or_ExtractsNothing()
    {
        // Narrowing to Email would drop every row that matched only on PhoneNumber.
        var value = "john@test.com";
        var found = Extract(x => x.Email == value || x.PhoneNumber == value);

        Assert.Empty(found);
    }

    [Fact]
    public void OrNestedInsideAnd_ExtractsOnlyTheGuaranteedSide()
    {
        var id = Guid.NewGuid();
        var value = "x";

        var found = Extract(x => x.Id == id && (x.Email == value || x.PhoneNumber == value));

        // Id is necessary; neither side of the OR is.
        var candidate = Assert.Single(found);
        Assert.Equal("Id", candidate.PropertyName);
    }

    [Fact]
    public void Negation_ExtractsNothing()
    {
        var email = "john@test.com";
        var found = Extract(x => !(x.Email == email));

        Assert.Empty(found);
    }

    [Fact]
    public void Inequality_ExtractsNothing()
    {
        var email = "john@test.com";
        var found = Extract(x => x.Email != email);

        Assert.Empty(found);
    }

    [Fact]
    public void MethodCall_ExtractsNothing()
    {
        var found = Extract(x => x.Email.StartsWith("john"));

        Assert.Empty(found);
    }

    [Fact]
    public void BareBooleanProperty_ExtractsNothing()
    {
        var found = Extract(x => x.EmailVerified);

        Assert.Empty(found);
    }

    [Fact]
    public void ComparisonBetweenTwoColumns_ExtractsNothing()
    {
        // Neither side is a standalone value, so there is nothing to query by.
        var found = Extract(x => x.Email == x.PhoneNumber);

        Assert.Empty(found);
    }

    [Fact]
    public void NullPredicate_ExtractsNothing()
    {
        Assert.Empty(EqualityPredicateExtractor.Extract<Customer>(null));
    }
}
