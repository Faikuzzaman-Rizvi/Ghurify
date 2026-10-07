using System.ComponentModel.DataAnnotations;

namespace Ghurify.Application.Payments;

/// <summary>
/// Payment settings. The gateway is chosen by <see cref="Provider"/>: "fake" (development, tests,
/// demos; never production) or "sslcommerz" (sandbox or live per <see cref="SslCommerzOptions.Sandbox"/>).
/// </summary>
public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    public const string FakeProvider = "fake";
    public const string SslCommerzProvider = "sslcommerz";

    [Required(AllowEmptyStrings = false)]
    public string Provider { get; set; } = FakeProvider;

    /// <summary>
    /// The service fee added to the trip price at checkout, in percent. Shown before paying, held in
    /// escrow with the price, released to the platform when the trip starts.
    /// </summary>
    [Range(0, 10)]
    public decimal ServiceFeePercent { get; set; } = 2m;

    /// <summary>This API's public base URL, for the gateway's return and IPN callbacks.</summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string ApiBaseUrl { get; set; } = "http://localhost:5199";

    /// <summary>The web app's public base URL, where travellers land after paying.</summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string WebBaseUrl { get; set; } = "http://localhost:5173";

    public SslCommerzOptions SslCommerz { get; set; } = new();

    /// <summary>Whether real money moves, from the provider and its sandbox switch.</summary>
    public PaymentMode Mode =>
        string.Equals(Provider, FakeProvider, StringComparison.OrdinalIgnoreCase) ? PaymentMode.Pretend
        : SslCommerz.Sandbox ? PaymentMode.Sandbox
        : PaymentMode.Live;
}

/// <summary>
/// Pretend: the built-in page, no gateway at all. Sandbox: the gateway's test environment, with its
/// test cards. Live: real money.
/// </summary>
public enum PaymentMode
{
    Live = 1,
    Sandbox = 2,
    Pretend = 3,
}

/// <summary>SSLCommerz store credentials. The password is a secret: .env / Key Vault only.</summary>
public sealed class SslCommerzOptions
{
    public string StoreId { get; set; } = string.Empty;

    public string StorePassword { get; set; } = string.Empty;

    /// <summary>Sandbox (sandbox.sslcommerz.com) unless explicitly turned off for production.</summary>
    public bool Sandbox { get; set; } = true;

    /// <summary>How long to wait for SSLCommerz before treating a call as failed.</summary>
    [Range(3, 60)]
    public int TimeoutSeconds { get; set; } = 15;
}
