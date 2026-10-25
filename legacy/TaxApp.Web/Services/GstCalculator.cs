using System;
using TaxApp.Web.Models;

namespace TaxApp.Web.Services
{
    public interface IGstCalculator
    {
        GstResult Calculate(decimal amount, GstMode mode);
    }

    public class GstCalculator : IGstCalculator
    {
        private readonly decimal _rate;

        public GstCalculator() : this(AppSettings.GstRate)
        {
        }

        public GstCalculator(decimal rate)
        {
            _rate = rate;
        }

        public GstResult Calculate(decimal amount, GstMode mode)
        {
            if (amount < 0m)
            {
                throw new ArgumentOutOfRangeException("amount", "Amount cannot be negative.");
            }

            decimal gst;
            decimal exclusive;
            decimal inclusive;

            if (mode == GstMode.AddGst)
            {
                exclusive = amount;
                gst = Round(amount * _rate);
                inclusive = exclusive + gst;
            }
            else
            {
                inclusive = amount;
                gst = Round(amount * _rate / (1m + _rate));
                exclusive = inclusive - gst;
            }

            return new GstResult
            {
                Mode = mode,
                Amount = amount,
                GstAmount = gst,
                AmountExclGst = exclusive,
                AmountInclGst = inclusive
            };
        }

        private static decimal Round(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
