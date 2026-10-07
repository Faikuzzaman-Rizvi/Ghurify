using Ghurify.Domain.Chat;
using Ghurify.Domain.Payments;

namespace Ghurify.UnitTests.Payments;

/// <summary>The refund rules table, and hiding numbers in chat.</summary>
public sealed class RefundPolicyTests
{
    [Theory]
    [InlineData(60, 6000, 6000)]
    [InlineData(14, 6000, 6000)]
    [InlineData(13, 6000, 3000)]
    [InlineData(7, 6000, 3000)]
    [InlineData(7, 4555, 2277.50)]
    [InlineData(6, 6000, 0)]
    [InlineData(1, 6000, 0)]
    public void TravellerCancellation_FollowsTheTable(int daysBefore, decimal price, decimal expected)
    {
        Assert.Equal(expected, RefundPolicy.ForTravelerCancellation(price, daysBefore));
    }

    [Fact]
    public void CancellationsBeyondTheTravellersControl_RefundEverythingIncludingTheFee()
    {
        Assert.Equal(6120m, RefundPolicy.ForCancellationBeyondTravelerControl(6000m, 120m));
    }

    [Theory]
    [InlineData("Call me 01712345678")]
    [InlineData("bKash: 01712-345678")]
    [InlineData("+880 1712 345678")]
    [InlineData("8801712345678 is my number")]
    [InlineData("নম্বর ০১৭১২৩৪৫৬৭৮")]
    [InlineData("Rocket 017123456789")]
    public void ContactMasker_HidesPhoneAndWalletNumbers(string text)
    {
        var (masked, wasMasked) = ContactMasker.Apply(text);

        Assert.True(wasMasked);
        Assert.DoesNotContain("345678", masked, StringComparison.Ordinal);
        Assert.DoesNotContain("৩৪৫৬৭৮", masked, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("We leave on 2026-10-14 at 21:00")]
    [InlineData("The cost is Tk 6,800 per person")]
    [InlineData("Room 1204, bus seat 12")]
    public void ContactMasker_LeavesOrdinaryMessagesAlone(string text)
    {
        var (masked, wasMasked) = ContactMasker.Apply(text);

        Assert.False(wasMasked);
        Assert.Equal(text, masked);
    }
}
