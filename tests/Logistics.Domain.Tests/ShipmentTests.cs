using Logistics.Domain.Common;
using Logistics.Domain.Reference;
using Logistics.Domain.Shipments;

namespace Logistics.Domain.Tests;

public class ShipmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly Port Bangkok = new("THLCH", "Laem Chabang", "TH", 13.08, 100.88);
    private static readonly Port Singapore = new("SGSIN", "Singapore", "SG", 1.26, 103.84);
    private static readonly Carrier SeaCarrier = new("MSK", "Fake Maersk Line", TransportMode.Sea);
    private static readonly Customer Acme = new("ACME", "Acme Trading", "ops@acme.test");

    private static Shipment NewShipment(DateTimeOffset? eta = null) =>
        Shipment.Create("SH-10001", Acme, SeaCarrier, Bangkok, Singapore, "Rice, 20 pallets", 12_000m,
            eta ?? Now.AddDays(7), Now);

    [Fact]
    public void Create_starts_booked_with_one_event_at_origin()
    {
        var shipment = NewShipment();

        Assert.Equal(ShipmentStatus.Booked, shipment.Status);
        var booked = Assert.Single(shipment.Events);
        Assert.Equal(ShipmentStatus.Booked, booked.Status);
        Assert.Equal(Bangkok.Latitude, shipment.CurrentLatitude);
        Assert.Equal(TransportMode.Sea, shipment.Mode);
    }

    [Fact]
    public void Create_rejects_same_origin_and_destination()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Shipment.Create("SH-1", Acme, SeaCarrier, Bangkok, Bangkok, "x", 1m, Now.AddDays(1), Now));
        Assert.Contains("different ports", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_rejects_non_positive_weight(decimal weight)
    {
        Assert.Throws<DomainException>(() =>
            Shipment.Create("SH-1", Acme, SeaCarrier, Bangkok, Singapore, "x", weight, Now.AddDays(1), Now));
    }

    [Fact]
    public void Create_rejects_eta_in_the_past()
    {
        Assert.Throws<DomainException>(() => NewShipment(eta: Now.AddMinutes(-1)));
    }

    [Fact]
    public void Full_happy_path_ends_delivered_at_destination()
    {
        var shipment = NewShipment();
        var t = Now;

        shipment.RecordEvent(ShipmentStatus.PickedUp, t = t.AddHours(2));
        shipment.RecordEvent(ShipmentStatus.InTransit, t = t.AddHours(6), "Gulf of Thailand", 10.5, 101.9);
        shipment.RecordEvent(ShipmentStatus.InTransit, t = t.AddHours(12), "South China Sea", 5.1, 103.2);
        shipment.RecordEvent(ShipmentStatus.ArrivedAtPort, t = t.AddDays(2), "Singapore");
        shipment.RecordEvent(ShipmentStatus.CustomsClearance, t = t.AddHours(4));
        shipment.RecordEvent(ShipmentStatus.OutForDelivery, t = t.AddHours(8));
        shipment.RecordEvent(ShipmentStatus.Delivered, t = t.AddHours(3));

        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        Assert.Equal(t, shipment.DeliveredAt);
        Assert.Equal(Singapore.Latitude, shipment.CurrentLatitude);
        Assert.Equal(8, shipment.Events.Count);
        Assert.False(shipment.IsDelayed(Now.AddYears(1)));
    }

    [Fact]
    public void Position_update_moves_current_location()
    {
        var shipment = NewShipment();
        shipment.RecordEvent(ShipmentStatus.PickedUp, Now.AddHours(1));
        shipment.RecordEvent(ShipmentStatus.InTransit, Now.AddHours(2), "At sea", 9.9, 102.2);

        Assert.Equal(9.9, shipment.CurrentLatitude);
        Assert.Equal(102.2, shipment.CurrentLongitude);
    }

    [Theory]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.CustomsClearance)]
    public void Booked_cannot_skip_ahead(ShipmentStatus target)
    {
        var shipment = NewShipment();

        var ex = Assert.Throws<DomainException>(() => shipment.RecordEvent(target, Now.AddHours(1)));
        Assert.Contains("from Booked", ex.Message);
    }

    [Fact]
    public void Cannot_cancel_once_in_transit()
    {
        var shipment = NewShipment();
        shipment.RecordEvent(ShipmentStatus.PickedUp, Now.AddHours(1));
        shipment.RecordEvent(ShipmentStatus.InTransit, Now.AddHours(2));

        Assert.Throws<DomainException>(() => shipment.RecordEvent(ShipmentStatus.Cancelled, Now.AddHours(3)));
    }

    [Fact]
    public void Events_must_be_in_time_order()
    {
        var shipment = NewShipment();

        Assert.Throws<DomainException>(() => shipment.RecordEvent(ShipmentStatus.PickedUp, Now.AddHours(-1)));
    }

    [Fact]
    public void Latitude_without_longitude_is_rejected()
    {
        var shipment = NewShipment();

        Assert.Throws<DomainException>(() =>
            shipment.RecordEvent(ShipmentStatus.PickedUp, Now.AddHours(1), latitude: 13.0));
    }

    [Fact]
    public void Is_delayed_after_eta_until_delivered()
    {
        var shipment = NewShipment(eta: Now.AddDays(1));

        Assert.False(shipment.IsDelayed(Now));
        Assert.True(shipment.IsDelayed(Now.AddDays(2)));
    }

    [Fact]
    public void Revising_eta_adds_a_note_and_clears_delay()
    {
        var shipment = NewShipment(eta: Now.AddDays(1));
        var later = Now.AddDays(2);

        var note = shipment.ReviseEstimatedArrival(later.AddDays(3), later, "Port congestion");

        Assert.False(shipment.IsDelayed(later));
        Assert.Equal(ShipmentStatus.Booked, note.Status);
        Assert.Contains("Port congestion", note.Note);
    }

    [Fact]
    public void Cannot_revise_eta_of_a_cancelled_shipment()
    {
        var shipment = NewShipment();
        shipment.RecordEvent(ShipmentStatus.Cancelled, Now.AddHours(1));

        Assert.Throws<DomainException>(() =>
            shipment.ReviseEstimatedArrival(Now.AddDays(10), Now.AddHours(2), "x"));
    }
}
