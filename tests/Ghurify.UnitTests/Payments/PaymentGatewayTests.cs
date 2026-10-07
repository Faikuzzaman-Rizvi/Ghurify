using System.Security.Cryptography;
using System.Text;
using Ghurify.Application.Payments;
using Ghurify.Infrastructure.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghurify.UnitTests.Payments;

/// <summary>Callback signatures (SSLCommerz and the sandbox gateway) and the service fee.</summary>
public sealed class PaymentGatewayTests
{
    private const string StorePassword = "store-password-for-tests";

    [Fact]
    public void SslCommerz_ACallbackSignedAsDocumented_IsAccepted()
    {
        using var gateway = SslCommerz();

        var callback = gateway.ReadCallback(Signed(SampleFields()));

        Assert.NotNull(callback);
        Assert.Equal("GHR123", callback.TransactionRef);
        Assert.Equal(GatewayOutcome.Succeeded, callback.Outcome);
        Assert.Equal(6120m, callback.Amount);
    }

    // What the sandbox actually posts back when a card is declined or the traveller closes the page:
    // signed, with no val_id, so the payment is closed without asking the validation API.
    [Theory]
    [InlineData("FAILED", "Invalid CVV", GatewayOutcome.Failed)]
    [InlineData("CANCELLED", "Cancelled by User", GatewayOutcome.Cancelled)]
    public void SslCommerz_ADeclinedOrCancelledReturn_IsReadAsSuch(string status, string error, GatewayOutcome expected)
    {
        using var gateway = SslCommerz();
        var fields = SampleFields();
        fields.Remove("val_id");
        fields["status"] = status;
        fields["error"] = error;

        var callback = gateway.ReadCallback(Signed(fields));

        Assert.NotNull(callback);
        Assert.Equal(expected, callback.Outcome);
        Assert.Null(callback.ValidationId);
        Assert.Equal(error, callback.Reason);
    }

    [Theory]
    [InlineData(PaymentsOptions.FakeProvider, true, PaymentMode.Pretend)]
    [InlineData(PaymentsOptions.SslCommerzProvider, true, PaymentMode.Sandbox)]
    [InlineData(PaymentsOptions.SslCommerzProvider, false, PaymentMode.Live)]
    public void Mode_FollowsTheProviderAndItsSandboxSwitch(string provider, bool sandbox, PaymentMode expected)
    {
        var options = new PaymentsOptions { Provider = provider, SslCommerz = new SslCommerzOptions { Sandbox = sandbox } };

        Assert.Equal(expected, options.Mode);
    }

    [Fact]
    public void SslCommerz_ACallbackWhoseAmountWasChangedAfterSigning_IsRejected()
    {
        using var gateway = SslCommerz();
        var fields = Signed(SampleFields());
        fields["amount"] = "10.00";

        Assert.Null(gateway.ReadCallback(fields));
    }

    [Fact]
    public void SslCommerz_ACallbackWithoutASignature_IsRejected()
    {
        using var gateway = SslCommerz();
        var fields = SampleFields();

        Assert.Null(gateway.ReadCallback(fields));
    }

    [Fact]
    public void SslCommerz_ACallbackSignedWithAnotherStorePassword_IsRejected()
    {
        using var gateway = SslCommerz();

        Assert.Null(gateway.ReadCallback(Signed(SampleFields(), password: "someone-elses-password")));
    }

    [Fact]
    public void Sandbox_ACallbackItSigned_IsAccepted_AndATamperedOneIsNot()
    {
        var gateway = new FakePaymentGateway(Options.Create(new PaymentsOptions()));
        var fields = gateway.SignedCallback("GHR1", succeeded: true, 6120m);

        Assert.NotNull(gateway.ReadCallback(fields));

        fields["amount"] = "1.00";
        Assert.Null(gateway.ReadCallback(fields));
    }

    [Fact]
    public void Sandbox_ACallbackSignedByAnotherProcess_IsRejected()
    {
        var mine = new FakePaymentGateway(Options.Create(new PaymentsOptions()));
        var theirs = new FakePaymentGateway(Options.Create(new PaymentsOptions()));

        Assert.Null(mine.ReadCallback(theirs.SignedCallback("GHR1", succeeded: true, 100m)));
    }

    [Theory]
    [InlineData(6000, 2, 120)]
    [InlineData(4550, 2, 91)]
    [InlineData(1234.56, 2.5, 30.86)]
    [InlineData(10.10, 2.5, 0.25)]
    [InlineData(5000, 0, 0)]
    public void Fee_IsRoundedToThePaisaHalfAwayFromZero(decimal amount, decimal percent, decimal expected)
    {
        Assert.Equal(expected, PaymentFees.FeeFor(amount, percent));
    }

    private static SslCommerzGateway SslCommerz() => new(
        Options.Create(new PaymentsOptions
        {
            Provider = PaymentsOptions.SslCommerzProvider,
            SslCommerz = new SslCommerzOptions { StoreId = "test", StorePassword = StorePassword },
        }),
        NullLogger<SslCommerzGateway>.Instance);

    private static Dictionary<string, string> SampleFields() => new(StringComparer.Ordinal)
    {
        ["tran_id"] = "GHR123",
        ["val_id"] = "2410051234567abc",
        ["amount"] = "6120.00",
        ["currency"] = "BDT",
        ["status"] = "VALID",
        ["bank_tran_id"] = "241005123456",
    };

    /// <summary>
    /// Signs fields the way SSLCommerz documents it, independently of the gateway's own code:
    /// the listed keys plus md5(store password) as store_passwd, sorted, k=v joined by &amp;, MD5.
    /// </summary>
    private static Dictionary<string, string> Signed(Dictionary<string, string> fields, string password = StorePassword)
    {
        var keys = string.Join(",", fields.Keys);
        var values = new SortedDictionary<string, string>(fields, StringComparer.Ordinal)
        {
            ["store_passwd"] = Md5(password),
        };

        var signature = Md5(string.Join("&", values.Select(pair => $"{pair.Key}={pair.Value}")));
        return new Dictionary<string, string>(fields, StringComparer.Ordinal)
        {
            ["verify_key"] = keys,
            ["verify_sign"] = signature,
        };
    }

#pragma warning disable CA5351 // Reproducing the gateway's documented MD5 signature.
    private static string Md5(string value) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
#pragma warning restore CA5351
}
