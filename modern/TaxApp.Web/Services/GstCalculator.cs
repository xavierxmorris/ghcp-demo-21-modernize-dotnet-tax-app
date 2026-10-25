using Microsoft.Extensions.Options;
using TaxApp.Web.Models;

namespace TaxApp.Web.Services;

public interface IGstCalculator
{
    GstResult Calculate(decimal amount, GstMode mode);
}

public sealed class GstCalculator : IGstCalculator
{
    private readonly decimal _rate;

    public GstCalculator(IOptions<TaxOptions> options)
        : this(options.Value.GstRate)
    {
    }

    public GstCalculator(decimal rate)
    {
        _rate = rate;
    }

    public GstResult Calculate(decimal amount, GstMode mode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (mode == GstMode.AddGst)
        {
            var gst = Round(amount * _rate);
            return new GstResult(mode, amount, gst, amount, amount + gst);
        }

        var extracted = Round(amount * _rate / (1m + _rate));
        return new GstResult(mode, amount, extracted, amount - extracted, amount);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
