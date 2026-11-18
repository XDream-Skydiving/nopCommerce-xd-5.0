using System.Text.RegularExpressions;
using Nop.Core.Domain.Shipping;

namespace Nop.Services.Shipping.Tracking;

/// <summary>
/// Represents the default shipment tracker
/// </summary>
public partial class DefaultShipmentTracker : IShipmentTracker
{
    /// <summary>
    /// Get URL for a page to show tracking info (third party tracking page)
    /// </summary>
    /// <param name="trackingNumber">The tracking number to track</param>
    /// <param name="shipment">Shipment; pass null if the tracking number is not associated with a specific shipment</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the URL of a tracking page
    /// </returns>
    public Task<string> GetUrlAsync(string trackingNumber, Shipment shipment = null)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            return Task.FromResult<string>(null);

        var trackingNumberTrimmed = trackingNumber.Trim().ToUpperInvariant();
        var trackingNumberUrl = string.Empty;
        var encodedTrackingNumber = Uri.EscapeDataString(trackingNumberTrimmed);
        var isDigits = Regex.IsMatch(trackingNumberTrimmed, @"^[0-9]+$");

        if (trackingNumberTrimmed.EndsWith("GB", StringComparison.Ordinal))
        {
            trackingNumberUrl = string.Format("http://www.royalmail.com/portal/rm/track?trackNumber={0}", encodedTrackingNumber);
        }
        else if (trackingNumberTrimmed.StartsWith("1Z", StringComparison.Ordinal))
        {
            trackingNumberUrl = string.Format("https://www.ups.com/WebTracking/processInputRequest?tracknum={0}", encodedTrackingNumber);
        }
        else if (Regex.IsMatch(trackingNumberTrimmed, @"^[0-9]{14}[A-Z]$"))
        {
            trackingNumberUrl = string.Format("https://track.dpd.co.uk/search?reference={0}", encodedTrackingNumber);
        }
        else if (trackingNumberTrimmed.Length == 12 && isDigits)
        {
            trackingNumberUrl = string.Format("https://www.fedex.com/fedextrack/?trknbr={0}", encodedTrackingNumber);
        }
        else if (trackingNumberTrimmed.Length == 14 && isDigits)
        {
            trackingNumberUrl = string.Format("https://www.dhl.com/gb-en/home/tracking.html?tracking-id={0}", encodedTrackingNumber);
        }

        return Task.FromResult<string>(trackingNumberUrl);
    }

    /// <summary>
    /// Get all shipment events
    /// </summary>
    /// <param name="trackingNumber">The tracking number to track</param>
    /// <param name="shipment">Shipment; pass null if the tracking number is not associated with a specific shipment</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list of shipment events
    /// </returns>
    public Task<IList<ShipmentStatusEvent>> GetShipmentEventsAsync(string trackingNumber, Shipment shipment = null)
    {
        return Task.FromResult<IList<ShipmentStatusEvent>>(new List<ShipmentStatusEvent>());
    }
}
